using System.Reflection;

namespace DeliveryNoteLabeler.Core.Updates;

public static class AppVersion
{
    public static Version Current { get; } = ResolveCurrentVersion();

    public static string Display => Current.ToString(3);

    private static Version ResolveCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
        return ReadAssemblyVersion(assembly) ?? new Version(0, 0, 0);
    }

    private static Version? ReadAssemblyVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
            var versionText = plusIndex >= 0 ? informational[..plusIndex] : informational;
            if (Version.TryParse(versionText, out var parsedInformational))
            {
                return parsedInformational;
            }
        }

        return assembly.GetName().Version;
    }
}
