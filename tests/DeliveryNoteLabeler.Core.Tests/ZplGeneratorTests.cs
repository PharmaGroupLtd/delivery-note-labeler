using DeliveryNoteLabeler.Core.Models;
using DeliveryNoteLabeler.Core.Printing;

namespace DeliveryNoteLabeler.Core.Tests;

public class ZplGeneratorTests
{
    [Fact]
    public void BuildLabelZpl_ContainsExpectedFields()
    {
        var job = new LabelJob
        {
            DeliveryNoteNo = "004223 rev 1",
            CustomerOrderNo = "4507425575",
            DrawingNo = "30745655 REV A",
            PartQuantity = 2,
            LabelQuantity = 1,
            Description = "BRACKET, T-SENSOR",
            LineNo = 1,
        };

        var zpl = ZplGenerator.BuildLabelZpl(job);

        Assert.StartsWith("^XA", zpl, StringComparison.Ordinal);
        Assert.Contains("^XZ", zpl, StringComparison.Ordinal);
        Assert.Contains("^PW812", zpl, StringComparison.Ordinal);
        Assert.Contains("^MD30", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("^GB812,406", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("^GB385,102", zpl, StringComparison.Ordinal);
        Assert.Contains("^FO10,", zpl, StringComparison.Ordinal);
        Assert.Contains("^GB792,2,2,B,0^FS", zpl, StringComparison.Ordinal);
        Assert.Contains("^GB408,2,2,B,0^FS", zpl, StringComparison.Ordinal);
        Assert.Contains("^FO418,", zpl, StringComparison.Ordinal);
        Assert.Contains("^GB2,", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("MATERIAL & PART", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("IDENTIFICATION", zpl, StringComparison.Ordinal);
        Assert.Contains("PART NUMBER:", zpl, StringComparison.Ordinal);
        Assert.Contains("30745655 REV A", zpl, StringComparison.Ordinal);
        Assert.Contains("DELIVERY NUMBER:", zpl, StringComparison.Ordinal);
        Assert.Contains("004223 rev 1", zpl, StringComparison.Ordinal);
        Assert.Contains("ORDER NUMBER:", zpl, StringComparison.Ordinal);
        Assert.Contains("4507425575", zpl, StringComparison.Ordinal);
        Assert.Contains("DESCRIPTION:", zpl, StringComparison.Ordinal);
        Assert.Contains("QUANTITY:", zpl, StringComparison.Ordinal);
        Assert.Contains("2 PCS", zpl, StringComparison.Ordinal);
        Assert.Contains("^FO20,", zpl, StringComparison.Ordinal);
        Assert.Contains("^A0N,24,24^FH\\^FDQUANTITY:", zpl, StringComparison.Ordinal);
        Assert.Contains("^A0N,42,42^FB373,1,46,L,0^FH\\^FD2 PCS", zpl, StringComparison.Ordinal);
        Assert.Contains("MADE IN UK", zpl, StringComparison.Ordinal);
        Assert.Contains("DATE:", zpl, StringComparison.Ordinal);
        Assert.Contains("BRACKET, T-SENSOR", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("Part No:", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("Del Note:", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("Order:", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("Order No:", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLabelZpl_EscapesSpecialCharacters()
    {
        var job = new LabelJob
        {
            DeliveryNoteNo = "DN^1",
            CustomerOrderNo = "ORD~2",
            DrawingNo = "PART~3",
            PartQuantity = 1,
            LabelQuantity = 1,
            Description = "Test\\value",
            LineNo = 1,
        };

        var zpl = ZplGenerator.BuildLabelZpl(job);

        Assert.Contains("_5E", zpl, StringComparison.Ordinal);
        Assert.Contains("_7E", zpl, StringComparison.Ordinal);
        Assert.Contains("_5C", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("DN^1", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSampleLabelZpl_ProducesValidDocument()
    {
        var zpl = ZplGenerator.BuildSampleLabelZpl();

        Assert.Contains("^XA", zpl, StringComparison.Ordinal);
        Assert.Contains("^XZ", zpl, StringComparison.Ordinal);
        Assert.Contains("30745655 REV A", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLabelZpl_LongDescription_WrapsToMultipleLines()
    {
        var job = new LabelJob
        {
            DeliveryNoteNo = "004223 rev 1",
            CustomerOrderNo = "4507425575",
            DrawingNo = "30745655 REV A",
            PartQuantity = 2,
            LabelQuantity = 1,
            Description = "BRACKET, T-SENSOR, KNOB, BIN FULL, STAINLESS STEEL, LEFT HAND, MOUNTING PLATE",
            LineNo = 1,
        };

        var zpl = ZplGenerator.BuildLabelZpl(job);

        Assert.Contains("BRACKET, T-SENSOR", zpl, StringComparison.Ordinal);
        Assert.Contains("MOUNTING PLATE", zpl, StringComparison.Ordinal);
        Assert.Contains("^FB372,", zpl, StringComparison.Ordinal);
        Assert.Matches(@"\^FB372,\d+,30,L,0", zpl);
    }

    [Fact]
    public void BuildLabelZpl_WithLogo_PlacesLogoOnLeft()
    {
        var logo = new ZplEmbeddedGraphic
        {
            WidthDots = 100,
            HeightDots = 40,
            OriginX = 12,
            OriginY = 8,
            GraphicFieldCommand = "^GFA,10,10,1,FF",
        };

        var zpl = ZplGenerator.BuildLabelZpl(ZplGenerator.CreateSampleLabelJob(), logo: logo);

        Assert.Contains("^FO12,8^GFA,10,10,1,FF^FS", zpl, StringComparison.Ordinal);
        Assert.Contains("PART NUMBER:", zpl, StringComparison.Ordinal);
    }
}
