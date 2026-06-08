namespace DeliveryNoteLabeler.Core.Printing;



public sealed class LabelLayoutOptions

{

    /// <summary>Default width for 4×2 inch media at 203 dpi (GK420d).</summary>

    public const int DefaultWidthDots = 812;



    /// <summary>Default height for 4×2 inch media at 203 dpi (GK420d).</summary>

    public const int DefaultHeightDots = 406;



    public int WidthDots { get; init; } = DefaultWidthDots;

    public int HeightDots { get; init; } = DefaultHeightDots;



    public static LabelLayoutOptions Default { get; } = new();

}

