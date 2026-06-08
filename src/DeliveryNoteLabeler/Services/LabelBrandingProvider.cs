using System.IO;
using DeliveryNoteLabeler.Core.Printing;

namespace DeliveryNoteLabeler.Services;

internal static class LabelBrandingProvider
{
    private static ZplEmbeddedGraphic? _cachedLogo;
    private static LabelLayoutOptions? _cachedLayout;
    private static DateTime _cachedWriteTimeUtc;
    private static string? _cachedLogoPath;

    public static ZplEmbeddedGraphic? GetLogo(LabelLayoutOptions layout)
    {
        var logoPath = LogoPaths.ResolveLogoPath(AppContext.BaseDirectory);
        DateTime writeTimeUtc = File.Exists(logoPath)
            ? File.GetLastWriteTimeUtc(logoPath)
            : DateTime.MinValue;

        var contentTop = ZplGenerator.GetContentTopDots(layout);

        if (_cachedLogo is not null &&
            _cachedLayout is not null &&
            _cachedLayout.WidthDots == layout.WidthDots &&
            _cachedLayout.HeightDots == layout.HeightDots &&
            _cachedWriteTimeUtc == writeTimeUtc &&
            string.Equals(_cachedLogoPath, logoPath, StringComparison.OrdinalIgnoreCase))
        {
            return _cachedLogo;
        }

        _cachedLogo = ZplBitmapEncoder.CreateLogoGraphic(logoPath, layout, contentTop);
        _cachedLayout = layout;
        _cachedWriteTimeUtc = writeTimeUtc;
        _cachedLogoPath = logoPath;
        return _cachedLogo;
    }
}
