using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Tests;

public class LabelJobExpanderTests
{
    [Fact]
    public void ExpandToLabelJobs_CreatesOneRowPerLineItem()
    {
        var note = new DeliveryNote
        {
            DeliveryNoteNo = "004223 rev 1",
            CustomerOrderNo = "4507425575",
            SourcePath = "test.pdf",
            LineItems =
            [
                new LineItem
                {
                    LineNo = 1,
                    PartNo = "PSM-001976",
                    Description = "BRACKET A",
                    DrawingNo = "30745655 REV A",
                    Quantity = 1,
                },
                new LineItem
                {
                    LineNo = 2,
                    PartNo = "PSM-001977",
                    Description = "BRACKET B",
                    DrawingNo = "30745668 REV A",
                    Quantity = 2,
                },
            ],
        };

        var jobs = LabelJobExpander.ExpandToLabelJobs(note);

        Assert.Equal(2, jobs.Count);
        Assert.Equal(1, jobs[0].PartQuantity);
        Assert.Equal(1, jobs[0].LabelQuantity);
        Assert.Equal(2, jobs[1].PartQuantity);
        Assert.Equal(2, jobs[1].LabelQuantity);
    }

    [Fact]
    public void CreateManualLine_UsesMatchingDefaultQuantities()
    {
        var note = new DeliveryNote
        {
            DeliveryNoteNo = "004223 rev 1",
            CustomerOrderNo = "4507425575",
            SourcePath = "test.pdf",
            LineItems = [],
        };

        var job = LabelJobExpander.CreateManualLine(note, lineNo: 1, defaultQuantity: 3);

        Assert.Equal(3, job.PartQuantity);
        Assert.Equal(3, job.LabelQuantity);
    }

    [Fact]
    public void CountLabelsToPrint_SumsLabelQuantity()
    {
        var jobs = new[]
        {
            new LabelJob
            {
                DeliveryNoteNo = "1",
                CustomerOrderNo = "1",
                DrawingNo = "A",
                PartQuantity = 2,
                LabelQuantity = 1,
                Description = "A",
                LineNo = 1,
            },
            new LabelJob
            {
                DeliveryNoteNo = "1",
                CustomerOrderNo = "1",
                DrawingNo = "B",
                PartQuantity = 4,
                LabelQuantity = 3,
                Description = "B",
                LineNo = 2,
            },
        };

        Assert.Equal(4, LabelJob.CountLabelsToPrint(jobs));
    }
}
