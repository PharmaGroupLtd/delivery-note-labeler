using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Printing;
using DeliveryNoteLabeler.Services;

var publishDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dist", "publish"));

var logoPath = LogoPaths.ResolveLogoPath(publishDir);
if (!File.Exists(logoPath))
{
    Console.Error.WriteLine($"Logo not found: {logoPath}");
    return 1;
}

var layout = AppConfig.GetLabelLayoutOptions();
var logo = LogoGraphicFactory.CreateFromFile(logoPath, layout);
if (logo is null)
{
    Console.Error.WriteLine("Failed to encode logo.");
    return 1;
}

var zpl = ZplGenerator.BuildSampleLabelZpl(layout, logo);
var outputPath = Path.Combine(publishDir, "sample-label.zpl");
await File.WriteAllTextAsync(outputPath, zpl);

Console.WriteLine($"Logo: {logoPath}");
Console.WriteLine($"Logo dots: {logo.WidthDots}x{logo.HeightDots} at ({logo.OriginX},{logo.OriginY})");
Console.WriteLine($"Wrote: {outputPath}");
return 0;
