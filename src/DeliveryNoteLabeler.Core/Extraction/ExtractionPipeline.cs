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
        var path = ValidatePdfPath(pdfPath);

        onStatus?.Invoke(PdfTextParser.StatusScanningNormal);

        try
        {
            return PdfExtractor.ExtractDeliveryNote(path);
        }
        catch (ExtractionException exc) when (ShouldAttemptGeminiFallback(exc))
        {
            return FallbackToGemini(path, exc, onStatus);
        }
        catch (Exception exc)
        {
            return FallbackToGemini(
                path,
                new ExtractionException($"Normal scan failed: {exc.Message}", exc),
                onStatus);
        }
    }

    public DeliveryNote ExtractDeliveryNoteWithAi(
        string pdfPath,
        Action<string>? onStatus = null)
    {
        var path = ValidatePdfPath(pdfPath);
        onStatus?.Invoke(PdfTextParser.StatusScanningAi);
        return RequireGeminiAndExtract(path);
    }

    public (DeliveryNote Note, List<LabelJob> Jobs) ExtractLabelJobs(
        string pdfPath,
        Action<string>? onStatus = null)
    {
        var note = ExtractDeliveryNote(pdfPath, onStatus);
        return (note, LabelJobExpander.ExpandToLabelJobs(note));
    }

    public (DeliveryNote Note, List<LabelJob> Jobs) ExtractLabelJobsWithAi(
        string pdfPath,
        Action<string>? onStatus = null)
    {
        var note = ExtractDeliveryNoteWithAi(pdfPath, onStatus);
        return (note, LabelJobExpander.ExpandToLabelJobs(note));
    }

    private DeliveryNote FallbackToGemini(
        string path,
        ExtractionException normalScanFailure,
        Action<string>? onStatus)
    {
        onStatus?.Invoke(PdfTextParser.StatusScanningAi);

        if (!AppConfig.GeminiFallbackAvailable())
        {
            throw new ExtractionException(
                BuildMissingGeminiKeyMessage(path, normalScanFailure),
                normalScanFailure);
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

    private DeliveryNote RequireGeminiAndExtract(string path)
    {
        if (!AppConfig.GeminiFallbackAvailable())
        {
            throw new ExtractionException(
                "Gemini API key not configured. Add a key in Settings to scan with AI.");
        }

        return _geminiExtractor.ExtractDeliveryNote(path);
    }

    private static string ValidatePdfPath(string pdfPath)
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

        return path;
    }

    internal static bool ShouldAttemptGeminiFallback(ExtractionException exc)
    {
        var message = exc.Message.ToLowerInvariant();
        return !message.Contains("file not found")
            && !message.Contains("file must be a pdf")
            && !message.Contains("pdf has no pages");
    }

    private static string BuildMissingGeminiKeyMessage(string pdfPath, ExtractionException normalScanFailure)
    {
        if (PdfTextExtractor.DocumentContainsExtractableText(pdfPath))
        {
            return normalScanFailure.Message
                + Environment.NewLine + Environment.NewLine
                + "Add a Gemini API key in Settings to automatically try AI scan when normal mode cannot read a PDF, "
                + "then use Scan with AI to retry.";
        }

        return normalScanFailure.Message
            + Environment.NewLine + Environment.NewLine
            + "This PDF looks like a scan or image with no readable text. "
            + "Add a Gemini API key in Settings to try AI scan, then use Scan with AI to retry.";
    }
}
