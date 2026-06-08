namespace DeliveryNoteLabeler.Core.Extraction;

public static class GeminiErrorFormatter
{
    public const string GeminiQuotaNotActivatedMessage =
        """
        Your Gemini API key is saved, but Google has not activated free-tier quota for this project (limit: 0).

        This is not because you used the API too much — it usually fails on the very first AI scan when billing is not linked to the Google Cloud project.

        To fix:
        1. Open https://aistudio.google.com/ → Settings → Plan / billing
        2. Link a billing account to the project (normal test usage stays on the free tier)
        3. Create a new API key in that project
        4. Paste the new key in Settings here, then try AI scan again
        """;

    public static string FormatGeminiApiError(
        Exception error,
        IReadOnlyList<string>? triedModels = null)
    {
        return FormatGeminiApiError(error.Message, triedModels);
    }

    public static string FormatGeminiApiError(
        string text,
        IReadOnlyList<string>? triedModels = null)
    {
        var modelsNote = triedModels is { Count: > 0 }
            ? $"\n\nModels tried: {string.Join(", ", triedModels)}"
            : string.Empty;

        if (text.Contains("limit: 0", StringComparison.Ordinal)
            && (text.Contains("free_tier", StringComparison.OrdinalIgnoreCase)
                || text.Contains("resource_exhausted", StringComparison.OrdinalIgnoreCase)
                || text.Contains("429", StringComparison.Ordinal)))
        {
            return GeminiQuotaNotActivatedMessage + modelsNote;
        }

        if (text.Contains("429", StringComparison.Ordinal)
            || text.Contains("RESOURCE_EXHAUSTED", StringComparison.Ordinal))
        {
            return "Gemini rate limit or daily quota reached.\n\n"
                + "Wait a minute and try again, or check usage at https://aistudio.google.com/"
                + modelsNote;
        }

        return $"Gemini extraction failed: {text}{modelsNote}";
    }

    public static bool IsModelAvailabilityError(Exception error)
    {
        return IsModelAvailabilityError(error.Message);
    }

    public static bool IsModelAvailabilityError(string text)
    {
        if (text.Contains("limit: 0", StringComparison.Ordinal))
        {
            return true;
        }

        var lower = text.ToLowerInvariant();
        if (text.Contains("429", StringComparison.Ordinal) || lower.Contains("resource_exhausted"))
        {
            return true;
        }

        if (text.Contains("404", StringComparison.Ordinal) || lower.Contains("not found"))
        {
            return true;
        }

        return lower.Contains("not available")
            || lower.Contains("shut down")
            || lower.Contains("deprecated");
    }
}
