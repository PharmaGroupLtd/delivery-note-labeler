using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Extraction;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Tests;

public class ExtractorTests
{
    private const string SamplePdf =
        @"c:\Users\brook\Documents\Delivery Note Scan\deliverynote004223 rev 1.pdf";

    private const string PageOneLines = """
        1. PSM-001976 BRACKET, T-SENSOR, KNOB, BIN FULL, SS 30745655 REV A 1 ea
        2. PSM-001977 BRACKET, T-REFLECTOR, RCU, SS 30745668 REV A 2 ea
        3. PSM-001540 BEACON PILLAR 410 SS B/BLAST 30832788 REV A 1 ea
        """;

    private const string PageTwoLines = """
        Line UPR Description Drawing No. Quantity
        6. PSM-001978 BRACKET, T-SENSOR, RCU, SS 30745662 REV A 1 ea
        7. PSM-001675 BRACKET HDS, VHS30 SS 304 BB 30824693 REV A 2 ea
        -- 2 of 2 --
        """;

    [Fact]
    public void SamplePdf_HeaderAndLines()
    {
        if (!File.Exists(SamplePdf))
        {
            return;
        }

        var note = PdfExtractor.ExtractDeliveryNote(SamplePdf);

        Assert.Equal("004223 rev 1", note.DeliveryNoteNo);
        Assert.Equal("4507425575", note.CustomerOrderNo);
        Assert.Equal(5, note.LineItems.Count);
        Assert.Equal("30745655 REV A", note.LineItems[0].DrawingNo);
        Assert.Equal("PSM-001976", note.LineItems[0].PartNo);
        Assert.Equal(2, note.LineItems[1].Quantity);
    }

    [Fact]
    public void SamplePdf_LabelExpansion()
    {
        if (!File.Exists(SamplePdf))
        {
            return;
        }

        var note = PdfExtractor.ExtractDeliveryNote(SamplePdf);
        var jobs = LabelJobExpander.ExpandToLabelJobs(note);

        Assert.Equal(7, jobs.Count);
        var drawingJobs = jobs.Where(job => job.DrawingNo == "30745668 REV A").ToList();
        Assert.Equal(2, drawingJobs.Count);
        Assert.Equal([1, 2], drawingJobs.Select(job => job.CopyIndex));
    }

    [Fact]
    public void MultipageLineMerge()
    {
        var pageOneItems = PdfTextParser.ParseLineItems(PageOneLines);
        var pageTwoItems = PdfTextParser.ParseLineItems(PageTwoLines);
        var merged = pageOneItems.Concat(pageTwoItems).OrderBy(item => item.LineNo).ToList();

        Assert.Equal(5, merged.Count);
        Assert.Equal([1, 2, 3, 6, 7], merged.Select(item => item.LineNo));
        Assert.Equal("PSM-001675", merged[^1].PartNo);
    }

    [Fact]
    public void InvalidFile_Raises()
    {
        Assert.Throws<ExtractionException>(() => PdfExtractor.ExtractDeliveryNote("missing.pdf"));
    }

    [Fact]
    public void NoLineItemsInText_ReturnsEmpty()
    {
        var items = PdfTextParser.ParseLineItems("Delivery Note No. 123\nCustomer Order No: ABC");
        Assert.Empty(items);
    }
}
