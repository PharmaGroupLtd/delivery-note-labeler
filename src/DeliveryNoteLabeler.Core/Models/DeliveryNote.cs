namespace DeliveryNoteLabeler.Core.Models;

public sealed class DeliveryNote
{
    public required string DeliveryNoteNo { get; init; }
    public required string CustomerOrderNo { get; init; }
    public string? SalesOrderNo { get; init; }
    public string? CustomerReference { get; init; }
    public string? Date { get; init; }
    public required IReadOnlyList<LineItem> LineItems { get; init; }
    public string? SourcePath { get; init; }
    public ExtractionMethod ExtractionMethod { get; init; } = ExtractionMethod.Standard;
    public string? GeminiModel { get; init; }

    public int LabelCount => LineItems.Sum(item => item.Quantity);
}
