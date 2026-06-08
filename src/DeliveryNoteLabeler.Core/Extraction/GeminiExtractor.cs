using System.Text.Json;
using System.Text.RegularExpressions;
using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Extraction;

public sealed class GeminiExtractor
{
    private const int MaxInlinePdfBytes = 20 * 1024 * 1024;

    private const string ExtractionPrompt = """
        You are extracting structured data from a Pharma Sheet Metal Group delivery note PDF.
        The document may be a scan or photocopy. Read all pages.

        Return ONLY valid JSON (no markdown) with this exact shape:
        {
          "delivery_note_no": "004223 rev 1",
          "customer_order_no": "4507425575",
          "sales_order_no": "SO-001896 or null",
          "customer_reference": "W740 or null",
          "date": "28/05/2026 or null",
          "line_items": [
            {
              "line_no": 1,
              "part_no": "PSM-001976",
              "description": "BRACKET, T-SENSOR, KNOB, BIN FULL, SS",
              "drawing_no": "30745655 REV A",
              "quantity": 1
            }
          ]
        }

        Rules:
        - Include every line item row from the table on all pages.
        - part_no is the UPR code (PSM-XXXXXX).
        - drawing_no includes the revision, e.g. "30745655 REV A".
        - quantity is the integer before "ea" (not expanded).
        - Use null for missing optional header fields.
        """;

    private readonly HttpClient _httpClient;

    public GeminiExtractor(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
    }

    public DeliveryNote ExtractDeliveryNote(
        string pdfPath,
        string? apiKey = null,
        string? model = null)
    {
        var key = apiKey ?? AppConfig.GetGeminiApiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ExtractionException(
                "Gemini API key not configured. Set GEMINI_API_KEY or add a key in Settings.");
        }

        var path = Path.GetFullPath(pdfPath);
        if (!File.Exists(path))
        {
            throw new ExtractionException($"File not found: {path}");
        }

        var pdfBytes = File.ReadAllBytes(path);
        if (pdfBytes.Length > MaxInlinePdfBytes)
        {
            throw new ExtractionException(
                "PDF is too large for AI scan (max 20 MB). Try a smaller scan or lower resolution.");
        }

        var pdfBase64 = Convert.ToBase64String(pdfBytes);
        var modelsToTry = model is not null && model != AppConfig.DefaultGeminiModel
            ? new[] { model }
            : AppConfig.GeminiModels.ToArray();

        var attempted = new List<string>();
        Exception? lastError = null;

        foreach (var candidate in modelsToTry)
        {
            attempted.Add(candidate);
            try
            {
                var responseText = GenerateContentAsync(key, candidate, pdfBase64)
                    .GetAwaiter()
                    .GetResult();

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    throw new ExtractionException("Gemini returned an empty response.");
                }

                var data = ParseJsonResponse(responseText);
                return DeliveryNoteFromPayload(data, path, candidate);
            }
            catch (ExtractionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (candidate != modelsToTry[^1] && GeminiErrorFormatter.IsModelAvailabilityError(ex))
                {
                    continue;
                }

                throw new ExtractionException(
                    GeminiErrorFormatter.FormatGeminiApiError(ex, attempted));
            }
        }

        if (lastError is not null)
        {
            throw new ExtractionException(
                GeminiErrorFormatter.FormatGeminiApiError(lastError, attempted));
        }

        throw new ExtractionException("Gemini extraction failed with no response.");
    }

    public static DeliveryNote DeliveryNoteFromPayload(
        JsonElement data,
        string sourcePath,
        string? geminiModel = null)
    {
        if (!data.TryGetProperty("delivery_note_no", out var deliveryNoteElement)
            || !data.TryGetProperty("customer_order_no", out var customerOrderElement))
        {
            throw new ExtractionException("Gemini JSON is missing required header fields.");
        }

        var deliveryNoteNo = deliveryNoteElement.GetString()?.Trim() ?? string.Empty;
        var customerOrderNo = customerOrderElement.GetString()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(deliveryNoteNo) || string.IsNullOrEmpty(customerOrderNo))
        {
            throw new ExtractionException("Gemini returned empty delivery note or order number.");
        }

        var lineItems = LineItemsFromPayload(data);

        return new DeliveryNote
        {
            DeliveryNoteNo = deliveryNoteNo,
            CustomerOrderNo = customerOrderNo,
            SalesOrderNo = ReadOptionalString(data, "sales_order_no"),
            CustomerReference = ReadOptionalString(data, "customer_reference"),
            Date = ReadOptionalString(data, "date"),
            LineItems = lineItems,
            SourcePath = sourcePath,
            ExtractionMethod = ExtractionMethod.Gemini,
            GeminiModel = geminiModel,
        };
    }

    internal static List<LineItem> LineItemsFromPayload(JsonElement data)
    {
        if (!data.TryGetProperty("line_items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ExtractionException("Gemini did not return any line items.");
        }

        var parsed = new List<LineItem>();
        foreach (var raw in itemsElement.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            try
            {
                parsed.Add(new LineItem
                {
                    LineNo = raw.GetProperty("line_no").GetInt32(),
                    PartNo = raw.GetProperty("part_no").GetString()?.Trim().ToUpperInvariant() ?? string.Empty,
                    Description = raw.TryGetProperty("description", out var desc)
                        ? desc.GetString()?.Trim() ?? string.Empty
                        : string.Empty,
                    DrawingNo = raw.GetProperty("drawing_no").GetString()?.Trim().ToUpperInvariant() ?? string.Empty,
                    Quantity = raw.GetProperty("quantity").GetInt32(),
                });
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
            {
                throw new ExtractionException($"Invalid line item from Gemini: {raw}", ex);
            }
        }

        if (parsed.Count == 0)
        {
            throw new ExtractionException("Gemini did not return any valid line items.");
        }

        parsed.Sort((a, b) => a.LineNo.CompareTo(b.LineNo));
        return parsed;
    }

    internal static JsonElement ParseJsonResponse(string text)
    {
        text = text.Trim();
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            var match = Regex.Match(text, @"\{.*\}", RegexOptions.Singleline);
            if (!match.Success)
            {
                throw new ExtractionException("Gemini returned a response that was not valid JSON.");
            }

            using var document = JsonDocument.Parse(match.Value);
            return document.RootElement.Clone();
        }
    }

    private async Task<string> GenerateContentAsync(string apiKey, string model, string pdfBase64)
    {
        var payload = new Dictionary<string, object>
        {
            ["contents"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["inline_data"] = new Dictionary<string, object>
                            {
                                ["mime_type"] = "application/pdf",
                                ["data"] = pdfBase64,
                            },
                        },
                        new Dictionary<string, object>
                        {
                            ["text"] = ExtractionPrompt,
                        },
                    },
                },
            },
            ["generationConfig"] = new Dictionary<string, object>
            {
                ["responseMimeType"] = "application/json",
                ["temperature"] = 0,
            },
        };

        var json = JsonSerializer.Serialize(payload);
        var requestUri =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(ExtractApiErrorMessage(body), null, response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
        {
            throw new ExtractionException("Gemini returned an empty response.");
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString() ?? string.Empty;
            }
        }

        throw new ExtractionException("Gemini returned an empty response.");
    }

    private static string ExtractApiErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // Fall back to raw body.
        }

        return body;
    }

    private static string? ReadOptionalString(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var value = element.GetString()?.Trim();
        return value is null or "" or "null" ? null : value;
    }
}
