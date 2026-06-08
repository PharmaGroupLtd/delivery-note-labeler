using System.IO;

namespace DeliveryNoteLabeler.Core.Services;

public static class PdfPathParser
{
    public const string OpenFromSwitch = "--open-from";

    public static IReadOnlyList<string> ResolveStartupPdfPaths(IEnumerable<string>? startupArgs = null)
    {
        var startup = startupArgs?.ToArray() ?? [];
        var commandLine = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var allArgs = startup.Concat(commandLine).ToArray();

        var openFromPath = ExtractSwitchValue(allArgs, OpenFromSwitch);
        if (!string.IsNullOrWhiteSpace(openFromPath))
        {
            try
            {
                var fromFile = ReadOpenFromListFile(openFromPath);
                if (fromFile.Count > 0)
                {
                    return fromFile;
                }
            }
            finally
            {
                TryDeleteFile(openFromPath);
            }
        }

        return ParseInitialPdfPaths(FilterSwitchArguments(allArgs, OpenFromSwitch));
    }

    public static List<string> ReadOpenFromListFile(string listPath)
    {
        if (!File.Exists(listPath))
        {
            return [];
        }

        var lines = File.ReadAllLines(listPath);
        return ParseForwardedPdfPaths(lines);
    }

    public static List<string> ParseForwardedPdfPaths(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<string>();

        foreach (var rawPath in paths)
        {
            var cleaned = rawPath.Trim().Trim('"');
            if (string.IsNullOrEmpty(cleaned))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(cleaned);
            }
            catch (IOException)
            {
                continue;
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!seen.Add(fullPath))
            {
                continue;
            }

            if (!fullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resolved.Add(fullPath);
        }

        return resolved;
    }

    public static List<string> ParseInitialPdfPaths(IEnumerable<string> args)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new List<string>();

        foreach (var arg in args)
        {
            foreach (var token in SplitArgumentTokens(arg))
            {
                AddPdfPath(token, seen, paths);
            }
        }

        return paths;
    }

    public static string? ParseDroppedPath(string data)
    {
        foreach (var rawPath in SplitArgumentTokens(data))
        {
            var path = rawPath.Trim('"');
            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> SplitArgumentTokens(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            yield break;
        }

        var token = string.Empty;
        var inQuotes = false;

        foreach (var ch in arg)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (token.Length > 0)
                {
                    yield return token;
                    token = string.Empty;
                }

                continue;
            }

            token += ch;
        }

        if (token.Length > 0)
        {
            yield return token;
        }
    }

    private static void AddPdfPath(string rawPath, ISet<string> seen, IList<string> paths)
    {
        var cleaned = rawPath.Trim().Trim('"');
        if (string.IsNullOrEmpty(cleaned))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(cleaned);
        }
        catch (IOException)
        {
            return;
        }
        catch (ArgumentException)
        {
            return;
        }

        if (!seen.Add(fullPath))
        {
            return;
        }

        if (!fullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return;
        }

        paths.Add(fullPath);
    }

    private static IEnumerable<string> FilterSwitchArguments(IEnumerable<string> args, string switchName)
    {
        var skipNext = false;
        foreach (var arg in args)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (string.Equals(arg, switchName, StringComparison.OrdinalIgnoreCase))
            {
                skipNext = true;
                continue;
            }

            yield return arg;
        }
    }

    private static string? ExtractSwitchValue(string[] args, string switchName)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], switchName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                return null;
            }

            return args[index + 1].Trim().Trim('"');
        }

        return null;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
