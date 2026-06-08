using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DeliveryNoteLabeler.Core.Extraction;

public static class PdfExtractor
{
    public static DeliveryNote ExtractDeliveryNote(string pdfPath)
    {
        var path = Path.GetFullPath(pdfPath);
        if (!File.Exists(path))
        {
            throw new ExtractionException($"File not found: {path}");
        }

        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ExtractionException("File must be a PDF.");
        }

        using var document = PdfDocument.Open(path);
        var pages = document.GetPages().ToList();
        if (pages.Count == 0)
        {
            throw new ExtractionException("PDF has no pages.");
        }

        DeliveryNote? bestNote = null;
        var bestLineCount = -1;
        string? lastError = null;

        foreach (var pageTexts in BuildPageTextVariants(pages))
        {
            if (TryParseDeliveryNote(path, pageTexts, out var note, out var error))
            {
                if (note.LineItems.Count > bestLineCount)
                {
                    bestNote = note;
                    bestLineCount = note.LineItems.Count;
                }
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                lastError = error;
            }
        }

        if (bestNote is not null && bestLineCount > 0)
        {
            return bestNote;
        }

        throw new ExtractionException(
            lastError ?? "No line items found in normal scan.");
    }

    private static IEnumerable<List<string>> BuildPageTextVariants(IReadOnlyList<Page> pages)
    {
        yield return pages.Select(PdfTextExtractor.ExtractPageText).ToList();
        yield return pages.Select(PdfTextExtractor.ExtractFromWords).ToList();
        yield return pages.Select(PdfTextExtractor.ExtractFromLettersWithSpacing).ToList();
        yield return pages.Select(page => page.Text).ToList();
    }

    private static bool TryParseDeliveryNote(
        string sourcePath,
        IReadOnlyList<string> pageTexts,
        out DeliveryNote note,
        out string? error)
    {
        note = null!;
        error = null;

        PdfTextParser.HeaderFields? header = null;
        foreach (var pageText in pageTexts)
        {
            try
            {
                header = PdfTextParser.ParseHeader(pageText);
                break;
            }
            catch (ExtractionException)
            {
                // Try the next page for header fields.
            }
        }

        if (header is null)
        {
            error = "Could not find delivery note header fields in the PDF text.";
            return false;
        }

        var allLineItems = new List<LineItem>();
        foreach (var pageText in pageTexts)
        {
            allLineItems.AddRange(PdfTextParser.ParseLineItems(pageText));
        }

        var lineItems = PdfTextParser.DedupeLineItems(allLineItems);
        lineItems.Sort((a, b) => a.LineNo.CompareTo(b.LineNo));

        if (lineItems.Count == 0)
        {
            error = "No line items found in normal scan.";
            return false;
        }

        note = new DeliveryNote
        {
            DeliveryNoteNo = header.DeliveryNoteNo,
            CustomerOrderNo = header.CustomerOrderNo,
            SalesOrderNo = header.SalesOrderNo,
            CustomerReference = header.CustomerReference,
            Date = header.Date,
            LineItems = lineItems,
            SourcePath = sourcePath,
            ExtractionMethod = ExtractionMethod.Standard,
        };

        return true;
    }
}
