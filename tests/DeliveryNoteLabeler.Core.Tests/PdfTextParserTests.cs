using DeliveryNoteLabeler.Core.Extraction;

namespace DeliveryNoteLabeler.Core.Tests;

public class PdfTextParserTests
{
    [Fact]
    public void ParseLineItems_SupportsHyphenatedDrawingNumbers()
    {
        const string text = """
            1. PSM-002050 ADJUSTABEL HEIGHT TROLLEY - 10-LCP-035393 1
            SURFACE PLATE
            """;

        var items = PdfTextParser.ParseLineItems(text);

        Assert.Single(items);
        Assert.Equal("PSM-002050", items[0].PartNo);
        Assert.Equal("10-LCP-035393", items[0].DrawingNo);
        Assert.Equal("ADJUSTABEL HEIGHT TROLLEY - SURFACE PLATE", items[0].Description);
        Assert.Equal(1, items[0].Quantity);
    }

    [Fact]
    public void ParseHeader_AllowsCustomerOrderWithPeriod()
    {
        const string text = """
            Delivery Note No. 004223 rev 1
            Customer Order No. 4507425575
            Date: 28/05/2026
            """;

        var header = PdfTextParser.ParseHeader(text);

        Assert.Equal("4507425575", header.CustomerOrderNo);
    }

    [Fact]
    public void ParseHeader_AllowsCustomerOrderOnNextLine()
    {
        const string text = """
            Delivery Note No. 004223 rev 1
            Customer Order No:
            4507425575
            Date: 28/05/2026
            """;

        var header = PdfTextParser.ParseHeader(text);

        Assert.Equal("004223 rev 1", header.DeliveryNoteNo);
        Assert.Equal("4507425575", header.CustomerOrderNo);
    }

    [Fact]
    public void ParseLineItems_MergesWrappedDescription()
    {
        const string text = """
            1. PSM-001976 BRACKET, T-SENSOR,
            KNOB, BIN FULL, SS 30745655 REV A 1 ea
            """;

        var items = PdfTextParser.ParseLineItems(text);

        Assert.Single(items);
        Assert.Equal("PSM-001976", items[0].PartNo);
        Assert.Equal("30745655 REV A", items[0].DrawingNo);
    }

    [Fact]
    public void ParseLineItems_AllowsMissingEaSuffix()
    {
        const string text = "2. PSM-001977 BRACKET, T-REFLECTOR, RCU, SS 30745668 REV A 2";

        var items = PdfTextParser.ParseLineItems(text);

        Assert.Single(items);
        Assert.Equal(2, items[0].Quantity);
    }

    [Fact]
    public void ParseLineItems_SupportsSeparateRevisionColumn()
    {
        const string text = "1. PSM-002011 PROFILE ADV 150X150 31179176 A 1 ea PIPELINE FM FRAME";

        var items = PdfTextParser.ParseLineItems(text);

        Assert.Single(items);
        Assert.Equal("PSM-002011", items[0].PartNo);
        Assert.Equal("31179176 REV A", items[0].DrawingNo);
        Assert.Equal("PROFILE ADV 150X150 PIPELINE FM FRAME", items[0].Description);
        Assert.Equal(1, items[0].Quantity);
    }

    [Fact]
    public void TryParseLineItemLine_ParsesNormalizedShiftedFontLine()
    {
        const string line = "1. PSM-001146 COVER REJECT L/F 30768957 REV F 50 ea";

        Assert.True(PdfTextParser.TryParseLineItemLine(line, out var item));
        Assert.Equal("PSM-001146", item.PartNo);
        Assert.Equal("30768957 REV F", item.DrawingNo);
        Assert.Equal("COVER REJECT L/F", item.Description);
        Assert.Equal(50, item.Quantity);
    }

    [Theory]
    [InlineData(@"c:\Users\brook\Desktop\Del Note file\deliverynote004246.pdf", "004246", 1)]
    [InlineData(@"c:\Users\brook\Desktop\Del Note file\deliverynote004247.pdf", "004247", 1)]
    [InlineData(@"c:\Users\brook\Desktop\Del Note file\deliverynote004248.pdf", "004248", 1)]
    public void ExtractDeliveryNote_PharmaShiftedFontPdfs(string pdfPath, string deliveryNoteNo, int lineCount)
    {
        if (!File.Exists(pdfPath))
        {
            return;
        }

        var note = PdfExtractor.ExtractDeliveryNote(pdfPath);

        Assert.Equal(deliveryNoteNo, note.DeliveryNoteNo);
        Assert.Equal(lineCount, note.LineItems.Count);
    }

    [Fact]
    public void DocumentContainsExtractableText_IsFalseForImageOnlyPdf()
    {
        const string imageOnlyPdf =
            @"C:\Users\brook\Documents\Delivery Note Scan\Untitled_05062026_101605.pdf";

        if (!File.Exists(imageOnlyPdf))
        {
            return;
        }

        Assert.False(PdfTextExtractor.DocumentContainsExtractableText(imageOnlyPdf));
    }

    [Fact]
    public void DocumentContainsExtractableText_IsTrueForTextPdf()
    {
        const string textPdf =
            @"C:\Users\brook\Documents\Delivery Note Scan\deliverynote004223 rev 1.pdf";

        if (!File.Exists(textPdf))
        {
            return;
        }

        Assert.True(PdfTextExtractor.DocumentContainsExtractableText(textPdf));
    }
}
