namespace DeliveryNoteLabeler.Core.Models;

public sealed class LabelJob
{
    public required string DeliveryNoteNo { get; init; }
    public required string CustomerOrderNo { get; init; }
    public required string DrawingNo { get; set; }
    public required int LineQuantity { get; set; }
    public required int CopyIndex { get; set; }
    public required string Description { get; set; }
    public required int LineNo { get; init; }

    public string CopyLabel => $"{CopyIndex}/{LineQuantity}";
}
