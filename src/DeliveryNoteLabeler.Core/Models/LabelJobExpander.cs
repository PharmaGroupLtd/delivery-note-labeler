namespace DeliveryNoteLabeler.Core.Models;

public static class LabelJobExpander
{
    public static List<LabelJob> ExpandToLabelJobs(DeliveryNote note)
    {
        var jobs = new List<LabelJob>(note.LineItems.Count);
        foreach (var item in note.LineItems)
        {
            jobs.Add(new LabelJob
            {
                DeliveryNoteNo = note.DeliveryNoteNo,
                CustomerOrderNo = note.CustomerOrderNo,
                DrawingNo = item.DrawingNo,
                PartQuantity = item.Quantity,
                LabelQuantity = item.Quantity,
                Description = item.Description,
                LineNo = item.LineNo,
            });
        }

        return jobs;
    }

    public static LabelJob CreateManualLine(DeliveryNote note, int lineNo, int defaultQuantity = 1)
    {
        return new LabelJob
        {
            DeliveryNoteNo = note.DeliveryNoteNo,
            CustomerOrderNo = note.CustomerOrderNo,
            DrawingNo = string.Empty,
            PartQuantity = defaultQuantity,
            LabelQuantity = defaultQuantity,
            Description = string.Empty,
            LineNo = lineNo,
        };
    }
}
