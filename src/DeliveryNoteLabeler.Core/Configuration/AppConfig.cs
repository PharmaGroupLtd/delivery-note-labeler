using System.Text.Json;
using System.Text.Json.Serialization;
using DeliveryNoteLabeler.Core.Printing;

namespace DeliveryNoteLabeler.Core.Configuration;

public static class AppConfig
{
    public static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".delivery-note-labeler");

    public static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    public static readonly IReadOnlyList<string> GeminiModels =
    [
        "gemini-2.5-flash-lite",
        "gemini-2.0-flash-lite",
        "gemini-1.5-flash",
    ];

    public static string DefaultGeminiModel => GeminiModels[0];

    public static string? GetGeminiApiKey()
    {
        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim();
        if (!string.IsNullOrEmpty(envKey))
        {
            return envKey;
        }

        var config = ReadConfigFile();
        var key = config.GeminiApiKey?.Trim();
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    public static void SaveGeminiApiKey(string apiKey)
    {
        var config = ReadConfigFile();
        var cleaned = apiKey.Trim();
        config.GeminiApiKey = string.IsNullOrEmpty(cleaned) ? null : cleaned;
        WriteConfigFile(config);
    }

    public static string? GetPrinterName()
    {
        var envPrinter = Environment.GetEnvironmentVariable("DELIVERY_NOTE_LABELER_PRINTER")?.Trim();
        if (!string.IsNullOrEmpty(envPrinter))
        {
            return envPrinter;
        }

        var config = ReadConfigFile();
        var name = config.PrinterName?.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public static LabelLayoutOptions GetLabelLayoutOptions()
    {
        var config = ReadConfigFile();
        return new LabelLayoutOptions
        {
            WidthDots = config.LabelWidthDots ?? LabelLayoutOptions.DefaultWidthDots,
            HeightDots = config.LabelHeightDots ?? LabelLayoutOptions.DefaultHeightDots,
        };
    }

    public static void SavePrinterSettings(string? printerName, LabelLayoutOptions? layout = null)
    {
        var config = ReadConfigFile();
        var cleaned = printerName?.Trim();
        config.PrinterName = string.IsNullOrEmpty(cleaned) ? null : cleaned;

        if (layout is not null)
        {
            config.LabelWidthDots = layout.WidthDots;
            config.LabelHeightDots = layout.HeightDots;
        }

        WriteConfigFile(config);
    }

    public static bool IsPrinterConfigured() => !string.IsNullOrWhiteSpace(GetPrinterName());

    public static bool GeminiFallbackAvailable() => GetGeminiApiKey() is not null;

    public static string? GetUpdateManifestUrl()
    {
        var config = ReadConfigFile();
        var url = config.UpdateManifestUrl?.Trim();
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    public static void SaveUpdateManifestUrl(string? manifestUrl)
    {
        var config = ReadConfigFile();
        var cleaned = manifestUrl?.Trim();
        config.UpdateManifestUrl = string.IsNullOrEmpty(cleaned) ? null : cleaned;
        WriteConfigFile(config);
    }

    public static string? GetSkippedUpdateVersion()
    {
        var config = ReadConfigFile();
        var version = config.SkippedUpdateVersion?.Trim();
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    public static void SaveSkippedUpdateVersion(string? version)
    {
        var config = ReadConfigFile();
        var cleaned = version?.Trim();
        config.SkippedUpdateVersion = string.IsNullOrEmpty(cleaned) ? null : cleaned;
        WriteConfigFile(config);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static UserConfig ReadConfigFile()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return new UserConfig();
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<UserConfig>(json, JsonOptions) ?? new UserConfig();
        }
        catch (JsonException)
        {
            return new UserConfig();
        }
        catch (IOException)
        {
            return new UserConfig();
        }
    }

    private static void WriteConfigFile(UserConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    private sealed class UserConfig
    {
        [JsonPropertyName("gemini_api_key")]
        public string? GeminiApiKey { get; set; }

        [JsonPropertyName("printer_name")]
        public string? PrinterName { get; set; }

        [JsonPropertyName("label_width_dots")]
        public int? LabelWidthDots { get; set; }

        [JsonPropertyName("label_height_dots")]
        public int? LabelHeightDots { get; set; }

        [JsonPropertyName("update_manifest_url")]
        public string? UpdateManifestUrl { get; set; }

        [JsonPropertyName("skipped_update_version")]
        public string? SkippedUpdateVersion { get; set; }
    }
}
