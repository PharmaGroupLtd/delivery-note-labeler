namespace DeliveryNoteLabeler.Core.Models;

public sealed class LabelJob
{
    public required string DeliveryNoteNo { get; init; }
    public required string CustomerOrderNo { get; init; }
    public required string DrawingNo { get; set; }
    public required int PartQuantity { get; set; }
    public required int LabelQuantity { get; set; }
    public required string Description { get; set; }
    public required int LineNo { get; init; }

    public static int CountLabelsToPrint(IEnumerable<LabelJob> jobs) =>
        jobs.Sum(job => job.LabelQuantity);
}
