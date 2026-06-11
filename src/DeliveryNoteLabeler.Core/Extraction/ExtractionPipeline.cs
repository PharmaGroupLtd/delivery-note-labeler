using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Extraction;

public sealed class ExtractionPipeline
{
    public const string StatusScanning = "Scanning with AI…";

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
        onStatus?.Invoke(StatusScanning);

        if (!AppConfig.GeminiFallbackAvailable())
        {
            throw new ExtractionException(
                "Gemini API key not configured. Add a key in Settings to scan delivery notes.");
        }

        return _geminiExtractor.ExtractDeliveryNote(path);
    }

    public (DeliveryNote Note, List<LabelJob> Jobs) ExtractLabelJobs(
        string pdfPath,
        Action<string>? onStatus = null)
    {
        var note = ExtractDeliveryNote(pdfPath, onStatus);
        return (note, LabelJobExpander.ExpandToLabelJobs(note));
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
}
