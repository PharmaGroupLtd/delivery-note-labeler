namespace DeliveryNoteLabeler.Core.Printing;

public sealed class ZplEmbeddedGraphic
{
    public required int WidthDots { get; init; }
    public required int HeightDots { get; init; }
    public required int OriginX { get; init; }
    public required int OriginY { get; init; }

    /// <summary>Graphic field command without field origin, e.g. ^GFA,bytes,bytes,rowBytes,data</summary>
    public required string GraphicFieldCommand { get; init; }
}
