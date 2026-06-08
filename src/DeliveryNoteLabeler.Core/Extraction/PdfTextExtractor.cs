using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DeliveryNoteLabeler.Core.Extraction;

internal static class PdfTextExtractor
{
    private const double LineBucketSize = 4.0;

    public static bool DocumentContainsExtractableText(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        foreach (var page in document.GetPages())
        {
            if (PageContainsExtractableText(page))
            {
                return true;
            }
        }

        return false;
    }

    public static string ExtractPageText(Page page)
    {
        var wordText = ExtractFromWords(page);
        if (LooksUsable(wordText))
        {
            return wordText;
        }

        var letterText = ExtractFromLetters(page);
        return LooksUsable(letterText) ? letterText : wordText;
    }

    internal static bool PageContainsExtractableText(Page page)
    {
        if (page.Letters.Count > 0 || page.GetWords().Any())
        {
            return true;
        }

        return false;
    }

    private static bool LooksUsable(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("PSM-", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Delivery Note", StringComparison.OrdinalIgnoreCase)
            || text.Length > 80;
    }

    private static string ExtractFromWords(Page page)
    {
        var lines = page.GetWords()
            .GroupBy(word => Math.Round(word.BoundingBox.Bottom / LineBucketSize) * LineBucketSize)
            .OrderByDescending(group => group.Key)
            .Select(group => string.Join(
                " ",
                group.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text)));

        return string.Join('\n', lines);
    }

    private static string ExtractFromLetters(Page page)
    {
        if (page.Letters.Count == 0)
        {
            return string.Empty;
        }

        var lines = page.Letters
            .GroupBy(letter => Math.Round(letter.GlyphRectangle.Bottom / LineBucketSize) * LineBucketSize)
            .OrderByDescending(group => group.Key)
            .Select(group => string.Concat(group.OrderBy(letter => letter.GlyphRectangle.Left).Select(letter => letter.Value)));

        return string.Join('\n', lines);
    }
}
