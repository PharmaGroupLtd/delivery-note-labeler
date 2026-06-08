namespace DeliveryNoteLabeler.Core.Printing;

public static class LabelLayoutMetrics
{
    public const int ReferenceWidthDots = 812;
    public const int ReferenceHeightDots = 406;

    public const int EdgeMarginDots = 10;
    public const int MaxPrintDarkness = 30;
    public const int BorderThicknessDots = 2;
    public const int SectionPaddingDots = 6;
    public const int SectionGapDots = 4;
    public const int HeaderYDots = 6;

    public const int LogoOriginYDots = 8;
    public const int LogoMaxWidthDots = 235;
    public const int LogoMaxHeightDots = 78;
    public const int MaxLogoGraphicBytes = 18000;

    public static int GetAnchoredFooterY(LabelLayoutOptions layout, int footerHeightDots) =>
        layout.HeightDots - footerHeightDots - EdgeMarginDots;

    public static (int MaxLogoWidthDots, int MaxLogoHeightDots) GetLogoBounds(LabelLayoutOptions layout)
    {
        return (
            ScaleX(layout, LogoMaxWidthDots),
            ScaleY(layout, LogoMaxHeightDots));
    }

    public static (int MaxLogoWidthDots, int MaxLogoHeightDots) GetLogoBounds(
        LabelLayoutOptions layout,
        int contentTopDots)
    {
        var originY = ScaleY(layout, LogoOriginYDots);
        var maxWidth = layout.WidthDots - (EdgeMarginDots * 2);
        var maxHeight = Math.Max(1, contentTopDots - originY - SectionPaddingDots);
        return (maxWidth, maxHeight);
    }

    public static (int OriginX, int OriginY) GetLogoOrigin(
        LabelLayoutOptions layout,
        int logoWidthDots,
        int logoHeightDots)
    {
        var originX = Math.Max(
            EdgeMarginDots,
            (layout.WidthDots - logoWidthDots) / 2);
        var originY = ScaleY(layout, LogoOriginYDots);
        _ = logoHeightDots;
        return (originX, originY);
    }

    public static (int OriginX, int OriginY) GetLogoOrigin(
        LabelLayoutOptions layout,
        int logoWidthDots,
        int logoHeightDots,
        int contentTopDots)
    {
        var (originX, baseOriginY) = GetLogoOrigin(layout, logoWidthDots, logoHeightDots);
        var availableHeight = Math.Max(1, contentTopDots - baseOriginY - SectionPaddingDots);
        var originY = baseOriginY + Math.Max(0, (availableHeight - logoHeightDots) / 2);
        return (originX, originY);
    }

    public static int LeftColumnX(LabelLayoutOptions layout) => ScaleX(layout, 14);
    public static int RightColumnX(LabelLayoutOptions layout) => ScaleX(layout, 418);
    public static int LeftColumnWidth(LabelLayoutOptions layout) => ScaleX(layout, 385);
    public static int RightColumnWidth(LabelLayoutOptions layout) => ScaleX(layout, 384);

    public static int ScaleX(LabelLayoutOptions layout, int dots) =>
        Math.Max(1, (int)Math.Round(dots * layout.WidthDots / (double)ReferenceWidthDots));

    public static int ScaleY(LabelLayoutOptions layout, int dots) =>
        Math.Max(1, (int)Math.Round(dots * layout.HeightDots / (double)ReferenceHeightDots));
}
