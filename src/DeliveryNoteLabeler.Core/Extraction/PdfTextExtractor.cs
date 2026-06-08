using UglyToad.PdfPig.Content;

namespace DeliveryNoteLabeler.Core.Extraction;

internal static class PdfTextExtractor
{
    private const double LineBucketSize = 4.0;
    private const double LetterSpaceGap = 1.5;

    public static bool DocumentContainsExtractableText(string pdfPath)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        foreach (var page in document.GetPages())
        {
            if (PageContainsExtractableText(page))
            {
                return true;
            }
        }

        return false;
    }

    public static string ExtractPageText(Page page) => SelectBestPageText(page);

    internal static IReadOnlyList<string> GetPageTextVariants(Page page) =>
    [
        ExtractFromWords(page),
        ExtractFromLettersWithSpacing(page),
        page.Text,
    ];

    internal static bool PageContainsExtractableText(Page page) =>
        page.Letters.Count > 0 || page.GetWords().Any();

    internal static string ExtractFromWords(Page page)
    {
        var lines = page.GetWords()
            .GroupBy(word => Math.Round(word.BoundingBox.Bottom / LineBucketSize) * LineBucketSize)
            .OrderByDescending(group => group.Key)
            .Select(group => string.Join(
                " ",
                group.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text)));

        return string.Join('\n', lines);
    }

    internal static string ExtractFromLettersWithSpacing(Page page)
    {
        if (page.Letters.Count == 0)
        {
            return string.Empty;
        }

        var lines = page.Letters
            .GroupBy(letter => Math.Round(letter.GlyphRectangle.Bottom / LineBucketSize) * LineBucketSize)
            .OrderByDescending(group => group.Key)
            .Select(group =>
            {
                var ordered = group.OrderBy(letter => letter.GlyphRectangle.Left).ToList();
                if (ordered.Count == 0)
                {
                    return string.Empty;
                }

                var builder = new System.Text.StringBuilder();
                var previous = ordered[0];
                builder.Append(previous.Value);

                for (var index = 1; index < ordered.Count; index++)
                {
                    var current = ordered[index];
                    var gap = current.GlyphRectangle.Left - previous.GlyphRectangle.Right;
                    if (gap > LetterSpaceGap)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(current.Value);
                    previous = current;
                }

                return builder.ToString();
            });

        return string.Join('\n', lines);
    }

    private static string SelectBestPageText(Page page)
    {
        string? best = null;
        var bestScore = int.MinValue;

        foreach (var candidate in GetPageTextVariants(page).Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var score = ScoreExtractedText(candidate);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = candidate;
        }

        return best ?? string.Empty;
    }

    private static int ScoreExtractedText(string text)
    {
        var score = 0;
        if (text.Contains("PSM-", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (text.Contains("Delivery Note", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (text.Contains("Customer Order", StringComparison.OrdinalIgnoreCase))
        {
            score += 3;
        }

        score += PdfTextParser.ParseLineItems(text).Count * 4;

        try
        {
            PdfTextParser.ParseHeader(text);
            score += 6;
        }
        catch (Exceptions.ExtractionException)
        {
            // Ignore header scoring failures for this candidate.
        }

        if (text.Length > 80)
        {
            score += 1;
        }

        return score;
    }
}
