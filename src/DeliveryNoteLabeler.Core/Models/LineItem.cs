namespace DeliveryNoteLabeler.Core.Models;

public sealed class LineItem
{
    public required int LineNo { get; init; }
    public required string PartNo { get; init; }
    public required string Description { get; init; }
    public required string DrawingNo { get; init; }
    public required int Quantity { get; init; }
}
