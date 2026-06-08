using System.Globalization;
using System.Text;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Printing;

public static class ZplGenerator
{
    private const int LabelFontHeight = 22;
    private const int ValueFontHeight = 30;
    private const int QuantityLabelFontHeight = 24;
    private const int QuantityValueFontHeight = 42;
    private const int DescriptionValueFontHeight = 26;
    private const int FooterFontHeight = 20;
    private const int LabelValueGapDots = 4;
    private const int StandardMaxValueLines = 1;
    private const int QuantityMaxValueLines = 1;

    public static string BuildLabelZpl(
        LabelJob job,
        LabelLayoutOptions? layout = null,
        ZplEmbeddedGraphic? logo = null)
    {
        layout ??= LabelLayoutOptions.Default;

        var sections = BuildLabelSections(layout, logo);

        var builder = new StringBuilder();
        builder.AppendLine("^XA");
        builder.AppendLine($"^PW{layout.WidthDots}");
        builder.AppendLine($"^LL{layout.HeightDots}");
        builder.AppendLine("^LH0,0");
        builder.AppendLine($"^MD{LabelLayoutMetrics.MaxPrintDarkness}");
        builder.AppendLine("^CI28");

        AppendInnerBorders(builder, layout, sections);

        if (logo is not null)
        {
            builder.AppendLine($"^FO{logo.OriginX},{logo.OriginY}{logo.GraphicFieldCommand}^FS");
        }

        AppendLabelValueSection(
            builder,
            sections.PartNumber,
            "PART NUMBER:",
            job.DrawingNo,
            LabelFontHeight,
            ValueFontHeight,
            StandardMaxValueLines);
        AppendLabelValueSection(
            builder,
            sections.DeliveryNumber,
            "DELIVERY NUMBER:",
            job.DeliveryNoteNo,
            LabelFontHeight,
            ValueFontHeight,
            StandardMaxValueLines);
        AppendLabelValueSection(
            builder,
            sections.OrderNumber,
            "ORDER NUMBER:",
            job.CustomerOrderNo,
            LabelFontHeight,
            ValueFontHeight,
            StandardMaxValueLines);
        AppendLabelValueSection(
            builder,
            sections.Quantity,
            "QUANTITY:",
            FormatQuantity(job.PartQuantity),
            QuantityLabelFontHeight,
            QuantityValueFontHeight,
            QuantityMaxValueLines);

        AppendDescriptionSection(
            builder,
            sections.Description,
            sections.DescriptionMaxLines,
            job.Description);

        AppendFieldCentered(
            builder,
            sections.Footer,
            FooterFontHeight,
            "MADE IN UK");
        AppendFieldRight(
            builder,
            sections.Footer,
            FooterFontHeight,
            $"DATE: {DateTime.Now:dd/MM/yyyy}");

        builder.AppendLine("^XZ");
        return builder.ToString();
    }

    public static LabelJob CreateSampleLabelJob()
    {
        return new LabelJob
        {
            DeliveryNoteNo = "004223 rev 1",
            CustomerOrderNo = "4507425575",
            DrawingNo = "30745655 REV A",
            PartQuantity = 2,
            LabelQuantity = 1,
            Description = "BRACKET, T-SENSOR, KNOB, BIN FULL, SS",
            LineNo = 1,
        };
    }

    public static string BuildSampleLabelZpl(LabelLayoutOptions? layout = null, ZplEmbeddedGraphic? logo = null)
    {
        return BuildLabelZpl(CreateSampleLabelJob(), layout, logo);
    }

    private sealed record SectionRect(int X, int Y, int Width, int Height)
    {
        public int InnerX(int padding) => X + padding;

        public int InnerY(int padding) => Y + padding;

        public int InnerWidth(int padding) => Math.Max(1, Width - (padding * 2));
    }

    private sealed record LabelSections(
        SectionRect Header,
        SectionRect PartNumber,
        SectionRect DeliveryNumber,
        SectionRect OrderNumber,
        SectionRect Quantity,
        SectionRect Description,
        SectionRect Footer,
        int ContentTop,
        int ContentBottom,
        int DividerX,
        int DescriptionMaxLines);

    private static LabelSections BuildLabelSections(LabelLayoutOptions layout, ZplEmbeddedGraphic? logo)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var leftX = LabelLayoutMetrics.LeftColumnX(layout);
        var leftWidth = LabelLayoutMetrics.LeftColumnWidth(layout);
        var rightX = LabelLayoutMetrics.RightColumnX(layout);
        var rightWidth = LabelLayoutMetrics.RightColumnWidth(layout);
        var dividerX = rightX;

        var headerY = LabelLayoutMetrics.ScaleY(layout, LabelLayoutMetrics.HeaderYDots);
        var headerHeight = GetHeaderSectionHeight(layout, logo);
        var header = new SectionRect(
            LabelLayoutMetrics.EdgeMarginDots,
            headerY,
            layout.WidthDots - (LabelLayoutMetrics.EdgeMarginDots * 2),
            headerHeight);

        var contentTop = header.Y + header.Height;
        var standardHeight = GetLabelValueSectionHeight(LabelFontHeight, ValueFontHeight, StandardMaxValueLines);
        var quantityHeight = GetLabelValueSectionHeight(
            QuantityLabelFontHeight,
            QuantityValueFontHeight,
            QuantityMaxValueLines);
        var footerHeight = GetFooterSectionHeight();

        var partNumber = new SectionRect(leftX, contentTop, leftWidth, standardHeight);
        var deliveryNumber = new SectionRect(leftX, partNumber.Y + partNumber.Height, leftWidth, standardHeight);
        var orderNumber = new SectionRect(
            leftX,
            deliveryNumber.Y + deliveryNumber.Height,
            leftWidth,
            standardHeight);
        var quantity = new SectionRect(
            leftX,
            orderNumber.Y + orderNumber.Height,
            leftWidth,
            quantityHeight);

        var footerY = quantity.Y + quantity.Height;
        var contentBottom = footerY;
        var footer = new SectionRect(
            LabelLayoutMetrics.EdgeMarginDots,
            footerY,
            layout.WidthDots - (LabelLayoutMetrics.EdgeMarginDots * 2),
            footerHeight);
        var description = new SectionRect(rightX, contentTop, rightWidth, contentBottom - contentTop);

        var descriptionMaxLines = GetDescriptionMaxLines(description, padding);

        return new LabelSections(
            header,
            partNumber,
            deliveryNumber,
            orderNumber,
            quantity,
            description,
            footer,
            contentTop,
            contentBottom,
            dividerX,
            descriptionMaxLines);
    }

    private static int GetHeaderSectionHeight(LabelLayoutOptions layout, ZplEmbeddedGraphic? logo)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var headerY = LabelLayoutMetrics.ScaleY(layout, LabelLayoutMetrics.HeaderYDots);

        if (logo is null)
        {
            return padding * 2;
        }

        var contentBottom = logo.OriginY + logo.HeightDots;
        return Math.Max(1, contentBottom - headerY + padding);
    }

    private static int GetFooterSectionHeight() =>
        (LabelLayoutMetrics.SectionPaddingDots * 2) + FooterFontHeight;

    private static int GetLabelValueSectionHeight(int labelHeight, int valueHeight, int maxValueLines) =>
        (LabelLayoutMetrics.SectionPaddingDots * 2) +
        labelHeight +
        LabelValueGapDots +
        (maxValueLines * valueHeight) +
        ((maxValueLines - 1) * LabelValueGapDots);

    private static int GetDescriptionMaxLines(SectionRect section, int padding)
    {
        var valueStartY = section.InnerY(padding) + LabelFontHeight + LabelValueGapDots;
        var availableHeight = section.Y + section.Height - padding - valueStartY;
        var lineHeight = DescriptionValueFontHeight + LabelValueGapDots;

        return Math.Max(1, availableHeight / lineHeight);
    }

    private static string FormatQuantity(int quantity) =>
        string.Create(CultureInfo.InvariantCulture, $"{quantity} PCS");

    private static void AppendInnerBorders(StringBuilder builder, LabelLayoutOptions layout, LabelSections sections)
    {
        var thickness = LabelLayoutMetrics.BorderThicknessDots;
        var innerLeft = LabelLayoutMetrics.EdgeMarginDots;
        var innerWidth = layout.WidthDots - (innerLeft * 2);
        var leftRuleWidth = sections.DividerX - innerLeft;

        AppendHorizontalRule(builder, innerLeft, sections.ContentTop, innerWidth, thickness);

        AppendHorizontalRule(
            builder,
            innerLeft,
            sections.PartNumber.Y + sections.PartNumber.Height - thickness,
            leftRuleWidth,
            thickness);
        AppendHorizontalRule(
            builder,
            innerLeft,
            sections.DeliveryNumber.Y + sections.DeliveryNumber.Height - thickness,
            leftRuleWidth,
            thickness);
        AppendHorizontalRule(
            builder,
            innerLeft,
            sections.OrderNumber.Y + sections.OrderNumber.Height - thickness,
            leftRuleWidth,
            thickness);

        AppendVerticalRule(
            builder,
            sections.DividerX,
            sections.ContentTop,
            sections.ContentBottom - sections.ContentTop,
            thickness);

        AppendHorizontalRule(builder, innerLeft, sections.Footer.Y, innerWidth, thickness);
    }

    private static void AppendHorizontalRule(
        StringBuilder builder,
        int x,
        int y,
        int width,
        int thickness)
    {
        builder.AppendLine($"^FO{x},{y}^GB{width},{thickness},{thickness},B,0^FS");
    }

    private static void AppendVerticalRule(
        StringBuilder builder,
        int x,
        int y,
        int height,
        int thickness)
    {
        builder.AppendLine($"^FO{x},{y}^GB{thickness},{height},{thickness},B,0^FS");
    }

    private static void AppendLabelValueSection(
        StringBuilder builder,
        SectionRect section,
        string label,
        string value,
        int labelHeight,
        int valueHeight,
        int maxValueLines)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var x = section.InnerX(padding);
        var y = section.InnerY(padding);
        var blockWidth = section.InnerWidth(padding);

        AppendField(builder, x, y, labelHeight, labelHeight, label);
        y += labelHeight + LabelValueGapDots;
        AppendFieldBlock(builder, x, y, blockWidth, maxValueLines, valueHeight, valueHeight, value);
    }

    private static void AppendDescriptionSection(
        StringBuilder builder,
        SectionRect section,
        int maxLines,
        string description)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var x = section.InnerX(padding);
        var y = section.InnerY(padding);
        var blockWidth = section.InnerWidth(padding);

        AppendField(builder, x, y, LabelFontHeight, LabelFontHeight, "DESCRIPTION:");
        y += LabelFontHeight + LabelValueGapDots;

        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        var lineSpacing = DescriptionValueFontHeight + LabelValueGapDots;
        var escaped = ZplEscaper.EscapeFieldData(description.Trim());
        builder.AppendLine(
            $"^FO{x},{y}^A0N,{DescriptionValueFontHeight},{DescriptionValueFontHeight}^FB{blockWidth},{maxLines},{lineSpacing},L,0^FH\\^FD{escaped}^FS");
    }

    private static void AppendField(StringBuilder builder, int x, int y, int height, int width, string text)
    {
        var escaped = ZplEscaper.EscapeFieldData(text);
        builder.AppendLine($"^FO{x},{y}^A0N,{height},{width}^FH\\^FD{escaped}^FS");
    }

    private static void AppendFieldBlock(
        StringBuilder builder,
        int x,
        int y,
        int blockWidth,
        int maxLines,
        int height,
        int width,
        string text)
    {
        var escaped = ZplEscaper.EscapeFieldData(text);
        builder.AppendLine(
            $"^FO{x},{y}^A0N,{height},{width}^FB{blockWidth},{maxLines},{height + LabelValueGapDots},L,0^FH\\^FD{escaped}^FS");
    }

    private static void AppendFieldCentered(
        StringBuilder builder,
        SectionRect section,
        int height,
        string text)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var escaped = ZplEscaper.EscapeFieldData(text);
        var blockWidth = section.InnerWidth(padding);
        builder.AppendLine(
            $"^FO{section.InnerX(padding)},{section.InnerY(padding)}^A0N,{height},{height}^FB{blockWidth},1,0,C,0^FH\\^FD{escaped}^FS");
    }

    private static void AppendFieldRight(
        StringBuilder builder,
        SectionRect section,
        int height,
        string text)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var escaped = ZplEscaper.EscapeFieldData(text);
        var blockWidth = section.InnerWidth(padding);
        builder.AppendLine(
            $"^FO{section.InnerX(padding)},{section.InnerY(padding)}^A0N,{height},{height}^FB{blockWidth},1,0,R,0^FH\\^FD{escaped}^FS");
    }
}
