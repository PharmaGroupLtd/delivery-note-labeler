using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Extraction;

public sealed class ExtractionPipeline
{
    private readonly GeminiExtractor _geminiExtractor;

    public ExtractionPipeline(GeminiExtractor? geminiExtractor = null)
    {
        _geminiExtractor = geminiExtractor ?? new GeminiExtractor();
    }

    public DeliveryNote ExtractDeliveryNote(
        string pdfPath,
        Action<string>? onStatus = null)
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

        onStatus?.Invoke(PdfTextParser.StatusScanningNormal);

        try
        {
            return PdfExtractor.ExtractDeliveryNote(path);
        }
        catch (ExtractionException exc)
        {
            if (PdfTextExtractor.DocumentContainsExtractableText(path))
            {
                throw;
            }

            if (!ShouldFallbackToGemini(exc))
            {
                throw;
            }

            onStatus?.Invoke(PdfTextParser.StatusScanningAi);

            if (!AppConfig.GeminiFallbackAvailable())
            {
                throw new ExtractionException(
                    "This PDF looks like a scan or image with no readable text. "
                    + "Export a text-based PDF from your system, or add a Gemini API key in "
                    + "Settings to automatically try AI scan.");
            }

            try
            {
                return _geminiExtractor.ExtractDeliveryNote(path);
            }
            catch (ExtractionException geminiExc)
            {
                throw new ExtractionException(geminiExc.Message, geminiExc);
            }
        }
    }

    public (DeliveryNote Note, List<LabelJob> Jobs) ExtractLabelJobs(
        string pdfPath,
        Action<string>? onStatus = null)
    {
        var note = ExtractDeliveryNote(pdfPath, onStatus);
        return (note, LabelJobExpander.ExpandToLabelJobs(note));
    }

    private static bool ShouldFallbackToGemini(ExtractionException exc)
    {
        var message = exc.Message.ToLowerInvariant();
        return !message.Contains("pdf has no pages");
    }
}
