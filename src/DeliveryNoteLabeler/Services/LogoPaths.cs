using System.IO;

namespace DeliveryNoteLabeler.Services;

public static class LogoPaths
{
    public const string AssetsFolderName = "assets";
    public const string PrimaryLogoFileName = "logo.png";
    public const string LegacyLogoFileName = "pharma-logo.png";

    public static string ResolveLogoPath(string baseDirectory)
    {
        var assetsDir = Path.Combine(baseDirectory, AssetsFolderName);
        var primary = Path.Combine(assetsDir, PrimaryLogoFileName);
        if (File.Exists(primary))
        {
            return primary;
        }

        var legacy = Path.Combine(assetsDir, LegacyLogoFileName);
        return File.Exists(legacy) ? legacy : primary;
    }
}
