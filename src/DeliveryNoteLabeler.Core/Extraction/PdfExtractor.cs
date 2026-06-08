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

        var allLineItems = new List<LineItem>();
        PdfTextParser.HeaderFields? header = null;

        using var document = PdfDocument.Open(path);
        var pages = document.GetPages().ToList();
        if (pages.Count == 0)
        {
            throw new ExtractionException("PDF has no pages.");
        }

        for (var index = 0; index < pages.Count; index++)
        {
            var text = PdfTextExtractor.ExtractPageText(pages[index]);
            if (index == 0)
            {
                header = PdfTextParser.ParseHeader(text);
            }

            allLineItems.AddRange(PdfTextParser.ParseLineItems(text));
        }

        if (header is null)
        {
            throw new ExtractionException("Could not read PDF header.");
        }

        var lineItems = PdfTextParser.DedupeLineItems(allLineItems);
        lineItems.Sort((a, b) => a.LineNo.CompareTo(b.LineNo));

        if (lineItems.Count == 0)
        {
            throw new ExtractionException("No line items found in normal scan.");
        }

        return new DeliveryNote
        {
            DeliveryNoteNo = header.DeliveryNoteNo,
            CustomerOrderNo = header.CustomerOrderNo,
            SalesOrderNo = header.SalesOrderNo,
            CustomerReference = header.CustomerReference,
            Date = header.Date,
            LineItems = lineItems,
            SourcePath = path,
            ExtractionMethod = ExtractionMethod.Standard,
        };
    }
}
