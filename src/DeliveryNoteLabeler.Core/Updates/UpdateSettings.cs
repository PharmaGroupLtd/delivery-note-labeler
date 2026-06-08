using System.Reflection;

namespace DeliveryNoteLabeler.Core.Updates;

public static class UpdateSettings
{
    public const string ManifestUrlEnvironmentVariable = "DELIVERY_NOTE_LABELER_UPDATE_URL";

    public static string? GetManifestUrl()
    {
        var environmentUrl = Environment.GetEnvironmentVariable(ManifestUrlEnvironmentVariable)?.Trim();
        if (!string.IsNullOrWhiteSpace(environmentUrl))
        {
            return environmentUrl;
        }

        var configUrl = Configuration.AppConfig.GetUpdateManifestUrl()?.Trim();
        if (!string.IsNullOrWhiteSpace(configUrl))
        {
            return configUrl;
        }

        return ReadEmbeddedManifestUrl();
    }

    private static string? ReadEmbeddedManifestUrl()
    {
        var assembly = typeof(UpdateSettings).Assembly;
        using var stream = assembly.GetManifestResourceStream("DeliveryNoteLabeler.UpdateManifestUrl");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        var url = reader.ReadToEnd().Trim();
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return url;
    }
}
