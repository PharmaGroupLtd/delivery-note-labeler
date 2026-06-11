using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Extraction;
using DeliveryNoteLabeler.Core.Exceptions;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DiagExtract <pdf-path>");
    return 1;
}

var pdfPath = args[0];
Console.WriteLine($"PDF: {pdfPath}");
Console.WriteLine($"Gemini configured: {AppConfig.GeminiFallbackAvailable()}");
Console.WriteLine();

var pipeline = new ExtractionPipeline();

try
{
    var note = pipeline.ExtractDeliveryNote(
        pdfPath,
        message => Console.WriteLine(message));

    Console.WriteLine($"EXTRACT OK: {note.LineItems.Count} lines, DN {note.DeliveryNoteNo}, order {note.CustomerOrderNo}");
    foreach (var item in note.LineItems)
    {
        Console.WriteLine($"  {item.LineNo}. {item.PartNo} | {item.DrawingNo} | qty {item.Quantity} | {item.Description}");
    }
}
catch (ExtractionException ex)
{
    Console.WriteLine($"EXTRACT FAILED: {ex.Message}");
    return 1;
}

return 0;
