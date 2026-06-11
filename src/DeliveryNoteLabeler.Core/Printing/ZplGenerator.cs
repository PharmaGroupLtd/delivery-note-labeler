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
    private const int DescriptionValueFontHeight = 22;
    private const int DescriptionLineGapDots = 2;
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

        var partNumberLines = LabelPrintValidator.GetPartNumberLineCount(job.DrawingNo);
        var sections = BuildLabelSections(layout, partNumberLines);

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
            FormatPartNumberFieldData(job.DrawingNo),
            LabelFontHeight,
            ValueFontHeight,
            partNumberLines,
            valueAlreadyEscaped: true);
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

    /// <summary>
    /// Top edge of the bordered content block, anchored above the footer at the bottom of the label.
    /// </summary>
    public static int GetContentTopDots(LabelLayoutOptions layout)
    {
        var contentBlockHeight = GetContentBlockHeightDots();
        var footerHeight = GetFooterSectionHeight();
        var footerY = LabelLayoutMetrics.GetAnchoredFooterY(layout, footerHeight);
        return footerY - contentBlockHeight - LabelLayoutMetrics.SectionGapDots;
    }

    internal static string FormatPartNumberFieldData(string? partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return string.Empty;
        }

        var lines = SplitPartNumberLines(partNumber.Trim());
        return string.Join("\\&", lines.Select(ZplEscaper.EscapeFieldData));
    }

    internal static IReadOnlyList<string> SplitPartNumberLines(string partNumber)
    {
        var lines = new List<string>();
        for (var index = 0; index < partNumber.Length; index += LabelPrintValidator.PartNumberCharsPerLine)
        {
            var length = Math.Min(LabelPrintValidator.PartNumberCharsPerLine, partNumber.Length - index);
            lines.Add(partNumber.Substring(index, length));
        }

        return lines;
    }

    private static int GetContentBlockHeightDots()
    {
        var standardHeight = GetLabelValueSectionHeight(LabelFontHeight, ValueFontHeight, StandardMaxValueLines);
        var quantityHeight = GetLabelValueSectionHeight(
            QuantityLabelFontHeight,
            QuantityValueFontHeight,
            QuantityMaxValueLines);

        return standardHeight + standardHeight + quantityHeight;
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

    private static LabelSections BuildLabelSections(LabelLayoutOptions layout, int partNumberValueLines)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var leftX = LabelLayoutMetrics.LeftColumnX(layout);
        var leftWidth = LabelLayoutMetrics.LeftColumnWidth(layout);
        var rightX = LabelLayoutMetrics.RightColumnX(layout);
        var rightWidth = LabelLayoutMetrics.RightColumnWidth(layout);
        var dividerX = rightX;

        var headerY = LabelLayoutMetrics.ScaleY(layout, LabelLayoutMetrics.HeaderYDots);
        var standardHeight = GetLabelValueSectionHeight(LabelFontHeight, ValueFontHeight, StandardMaxValueLines);
        var partNumberHeight = GetLabelValueSectionHeight(LabelFontHeight, ValueFontHeight, partNumberValueLines);
        var quantityHeight = GetLabelValueSectionHeight(
            QuantityLabelFontHeight,
            QuantityValueFontHeight,
            QuantityMaxValueLines);
        var footerHeight = GetFooterSectionHeight();
        var contentBlockHeight = standardHeight + standardHeight + quantityHeight;
        var footerY = LabelLayoutMetrics.GetAnchoredFooterY(layout, footerHeight);
        var contentBottom = footerY;
        var contentTop = footerY - contentBlockHeight - LabelLayoutMetrics.SectionGapDots;
        var header = new SectionRect(
            LabelLayoutMetrics.EdgeMarginDots,
            headerY,
            layout.WidthDots - (LabelLayoutMetrics.EdgeMarginDots * 2),
            Math.Max(1, contentTop - headerY));

        var partNumber = new SectionRect(rightX, contentTop, rightWidth, partNumberHeight);
        var deliveryNumber = new SectionRect(leftX, contentTop, leftWidth, standardHeight);
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

        var footer = new SectionRect(
            LabelLayoutMetrics.EdgeMarginDots,
            footerY,
            layout.WidthDots - (LabelLayoutMetrics.EdgeMarginDots * 2),
            footerHeight);
        var description = new SectionRect(
            rightX,
            partNumber.Y + partNumber.Height,
            rightWidth,
            contentBottom - (partNumber.Y + partNumber.Height));

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
        var lineHeight = DescriptionValueFontHeight + DescriptionLineGapDots;

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
            sections.DeliveryNumber.Y + sections.DeliveryNumber.Height - thickness,
            leftRuleWidth,
            thickness);
        AppendHorizontalRule(
            builder,
            innerLeft,
            sections.OrderNumber.Y + sections.OrderNumber.Height - thickness,
            leftRuleWidth,
            thickness);
        AppendHorizontalRule(
            builder,
            sections.DividerX,
            sections.PartNumber.Y + sections.PartNumber.Height - thickness,
            innerLeft + innerWidth - sections.DividerX,
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
        int maxValueLines,
        bool valueAlreadyEscaped = false)
    {
        var padding = LabelLayoutMetrics.SectionPaddingDots;
        var x = section.InnerX(padding);
        var y = section.InnerY(padding);
        var blockWidth = section.InnerWidth(padding);

        AppendField(builder, x, y, labelHeight, labelHeight, label);
        y += labelHeight + LabelValueGapDots;
        AppendFieldBlock(
            builder,
            x,
            y,
            blockWidth,
            maxValueLines,
            valueHeight,
            valueHeight,
            value,
            valueAlreadyEscaped);
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

        var lineSpacing = DescriptionValueFontHeight + DescriptionLineGapDots;
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
        string text,
        bool alreadyEscaped = false)
    {
        var fieldData = alreadyEscaped ? text : ZplEscaper.EscapeFieldData(text);
        builder.AppendLine(
            $"^FO{x},{y}^A0N,{height},{width}^FB{blockWidth},{maxLines},{height + LabelValueGapDots},L,0^FH\\^FD{fieldData}^FS");
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
