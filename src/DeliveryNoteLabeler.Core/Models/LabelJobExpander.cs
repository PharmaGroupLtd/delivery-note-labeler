namespace DeliveryNoteLabeler.Core.Models;

public static class LabelJobExpander
{
    public static List<LabelJob> ExpandToLabelJobs(DeliveryNote note)
    {
        var jobs = new List<LabelJob>();
        foreach (var item in note.LineItems)
        {
            for (var copyIndex = 1; copyIndex <= item.Quantity; copyIndex++)
            {
                jobs.Add(new LabelJob
                {
                    DeliveryNoteNo = note.DeliveryNoteNo,
                    CustomerOrderNo = note.CustomerOrderNo,
                    DrawingNo = item.DrawingNo,
                    LineQuantity = item.Quantity,
                    CopyIndex = copyIndex,
                    Description = item.Description,
                    LineNo = item.LineNo,
                });
            }
        }

        return jobs;
    }
}
