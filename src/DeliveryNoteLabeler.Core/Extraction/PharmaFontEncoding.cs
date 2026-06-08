namespace DeliveryNoteLabeler.Core.Extraction;

/// <summary>
/// Some Pharma delivery note PDFs embed text with each byte shifted down by 31 from ASCII.
/// </summary>
internal static class PharmaFontEncoding
{
    private static readonly string[] ShiftedMarkers =
    [
        "1IBSNB",
        "%FMJWFSZ",
        "$VTUPNFS",
        "%FTDSJQUJPO",
        "14.-",
    ];

    public static string DecodeIfNeeded(string text)
    {
        if (string.IsNullOrEmpty(text) || !LooksLikeShiftedEncoding(text))
        {
            return text;
        }

        return Decode(text);
    }

    internal static bool LooksLikeShiftedEncoding(string text)
    {
        foreach (var marker in ShiftedMarkers)
        {
            if (text.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Decode(string text)
    {
        var chars = new char[text.Length];
        for (var index = 0; index < text.Length; index++)
        {
            chars[index] = DecodeChar(text[index]);
        }

        return new string(chars);
    }

    private static char DecodeChar(char value)
    {
        var code = (int)value;
        if (code is >= 1 and <= 94)
        {
            var decoded = code + 31;
            if (decoded is >= 32 and <= 126)
            {
                return (char)decoded;
            }
        }

        return value;
    }
}
