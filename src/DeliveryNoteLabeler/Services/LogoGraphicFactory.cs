using DeliveryNoteLabeler.Core.Printing;

namespace DeliveryNoteLabeler.Services;

public static class LogoGraphicFactory
{
    public static ZplEmbeddedGraphic? CreateFromFile(string imagePath, LabelLayoutOptions layout) =>
        ZplBitmapEncoder.CreateLogoGraphic(imagePath, layout, ZplGenerator.GetContentTopDots(layout));
}
