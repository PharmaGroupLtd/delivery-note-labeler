using DeliveryNoteLabeler.Core.Extraction;

namespace DeliveryNoteLabeler.Core.Tests;

public class PdfTextParserTests
{
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
