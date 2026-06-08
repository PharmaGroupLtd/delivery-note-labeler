using System.Text.Json;
using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Extraction;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Tests;

public class GeminiAndPipelineTests
{
    private static readonly Dictionary<string, object?> SamplePayload = new()
    {
        ["delivery_note_no"] = "004223 rev 1",
        ["customer_order_no"] = "4507425575",
        ["sales_order_no"] = "SO-001896",
        ["customer_reference"] = "W740",
        ["date"] = "28/05/2026",
        ["line_items"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["line_no"] = 1,
                ["part_no"] = "PSM-001976",
                ["description"] = "BRACKET, T-SENSOR, KNOB, BIN FULL, SS",
                ["drawing_no"] = "30745655 REV A",
                ["quantity"] = 2,
            },
        },
    };

    [Fact]
    public void DeliveryNoteFromPayload()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(SamplePayload));
        var note = GeminiExtractor.DeliveryNoteFromPayload(document.RootElement, "test.pdf");

        Assert.Equal("004223 rev 1", note.DeliveryNoteNo);
        Assert.Equal("30745655 REV A", note.LineItems[0].DrawingNo);
        Assert.Equal(ExtractionMethod.Gemini, note.ExtractionMethod);
    }

    [Fact]
    public void FormatGeminiQuotaNotActivated()
    {
        const string raw =
            "429 RESOURCE_EXHAUSTED. Quota exceeded for metric: "
            + "generativelanguage.googleapis.com/generate_content_free_tier_requests, "
            + "limit: 0, model: gemini-2.5-flash-lite";

        var message = GeminiErrorFormatter.FormatGeminiApiError(raw, ["gemini-2.5-flash-lite"]);

        Assert.Contains("limit: 0", message);
        Assert.Contains("billing", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gemini-2.5-flash-lite", message);
    }

    [Fact]
    public void EnvVarOverridesSavedConfig()
    {
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "env-key");
        try
        {
            Assert.Equal("env-key", AppConfig.GetGeminiApiKey());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        }
    }

    [Theory]
    [InlineData("Could not find delivery note header fields in the PDF text.", true)]
    [InlineData("No line items found in normal scan.", true)]
    [InlineData("Normal scan failed: unexpected parser error", true)]
    [InlineData("File not found: missing.pdf", false)]
    [InlineData("File must be a PDF.", false)]
    [InlineData("PDF has no pages.", false)]
    public void ShouldAttemptGeminiFallback(string message, bool expected)
    {
        Assert.Equal(expected, ExtractionPipeline.ShouldAttemptGeminiFallback(new ExtractionException(message)));
    }

    [Fact]
    public void ExtractDeliveryNoteWithAi_RequiresConfiguredApiKey()
    {
        var originalEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var configPath = AppConfig.ConfigFilePath;
        var hadExistingFile = File.Exists(configPath);
        var originalContents = hadExistingFile ? File.ReadAllText(configPath) : null;
        var pdfPath = File.Exists(SamplePdf)
            ? SamplePdf
            : Path.Combine(Path.GetTempPath(), "delivery-note-labeler-ai-key-test.pdf");

        try
        {
            if (!File.Exists(pdfPath))
            {
                File.WriteAllText(pdfPath, "pdf");
            }

            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            if (hadExistingFile && originalContents is not null)
            {
                File.WriteAllText(configPath, "{}");
            }

            var pipeline = new ExtractionPipeline();
            var exception = Assert.Throws<ExtractionException>(() =>
                pipeline.ExtractDeliveryNoteWithAi(pdfPath));

            Assert.Contains("Gemini API key not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalEnv);

            if (hadExistingFile && originalContents is not null)
            {
                File.WriteAllText(configPath, originalContents);
            }
            else if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            if (pdfPath != SamplePdf && File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }
        }
    }

    private const string SamplePdf =
        @"c:\Users\brook\Documents\Delivery Note Scan\deliverynote004223 rev 1.pdf";

    [Fact]
    public void SaveGeminiApiKey_RoundTripsThroughConfigFile()
    {
        var configPath = AppConfig.ConfigFilePath;
        var hadExistingFile = File.Exists(configPath);
        var originalContents = hadExistingFile ? File.ReadAllText(configPath) : null;
        var originalEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        try
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            AppConfig.SaveGeminiApiKey("test-roundtrip-key-12345");
            Assert.Equal("test-roundtrip-key-12345", AppConfig.GetGeminiApiKey());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalEnv);

            if (hadExistingFile && originalContents is not null)
            {
                File.WriteAllText(configPath, originalContents);
            }
            else if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
    }
}
