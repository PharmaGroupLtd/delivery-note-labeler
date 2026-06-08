using DeliveryNoteLabeler.Core.Extraction;
using DeliveryNoteLabeler.Core.Exceptions;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DiagExtract <pdf-path>");
    return 1;
}

var pdfPath = args[0];
Console.WriteLine($"PDF: {pdfPath}");
Console.WriteLine($"Has extractable text: {PdfTextExtractor.DocumentContainsExtractableText(pdfPath)}");
Console.WriteLine();

using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
var pageIndex = 0;
foreach (var page in document.GetPages())
{
    pageIndex++;
    var text = PdfTextExtractor.ExtractPageText(page);
    Console.WriteLine($"===== PAGE {pageIndex} ({text.Length} chars) =====");
    Console.WriteLine(text);
    Console.WriteLine();

    try
    {
        if (pageIndex == 1)
        {
            var header = PdfTextParser.ParseHeader(text);
            Console.WriteLine($"Header OK: {header.DeliveryNoteNo} / {header.CustomerOrderNo}");
        }

        var items = PdfTextParser.ParseLineItems(text);
        Console.WriteLine($"Line items on page: {items.Count}");
        foreach (var item in items)
        {
            Console.WriteLine($"  {item.LineNo}. {item.PartNo} | {item.DrawingNo} | qty {item.Quantity}");
        }

        var merged = PdfTextParser.MergeWrappedLineItems(text);
        Console.WriteLine("Merged PSM lines:");
        foreach (var line in merged.Split('\n'))
        {
            if (line.Contains("PSM", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  > {line}");
            }
        }
    }
    catch (ExtractionException ex)
    {
        Console.WriteLine($"Parse error: {ex.Message}");
    }

    Console.WriteLine();
}

try
{
    var note = PdfExtractor.ExtractDeliveryNote(pdfPath);
    Console.WriteLine($"FULL EXTRACT OK: {note.LineItems.Count} lines, DN {note.DeliveryNoteNo}");
}
catch (ExtractionException ex)
{
    Console.WriteLine($"FULL EXTRACT FAILED: {ex.Message}");
}

return 0;
