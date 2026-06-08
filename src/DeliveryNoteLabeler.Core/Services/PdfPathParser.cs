using System.IO;

namespace DeliveryNoteLabeler.Core.Services;

public static class PdfPathParser
{
    public const string OpenFromSwitch = "--open-from";

    public static IReadOnlyList<string> ResolveStartupPdfPaths(IEnumerable<string>? startupArgs = null)
    {
        var allArgs = startupArgs?.ToArray() ?? Environment.GetCommandLineArgs().Skip(1).ToArray();

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
            var cleaned = CleanForwardedPath(rawPath);
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

            if (!fullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath))
            {
                continue;
            }

            resolved.Add(fullPath);
        }

        return resolved;
    }

    internal static string CleanForwardedPath(string rawPath)
    {
        var cleaned = rawPath.Trim().Trim('"').Trim();
        if (cleaned.EndsWith(')'))
        {
            cleaned = cleaned[..^1].Trim().Trim('"').Trim();
        }

        return cleaned;
    }

    public static List<string> ParseInitialPdfPaths(IEnumerable<string> args)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new List<string>();

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            var trimmed = arg.Trim();
            if (TryAddPdfPath(trimmed, seen, paths))
            {
                continue;
            }

            // Legacy PrintLabels.cmd passed multiple quoted paths in one argument.
            foreach (var token in SplitArgumentTokens(trimmed))
            {
                TryAddPdfPath(token, seen, paths);
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

    private static bool TryAddPdfPath(string rawPath, ISet<string> seen, IList<string> paths)
    {
        var cleaned = rawPath.Trim().Trim('"');
        if (string.IsNullOrEmpty(cleaned))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(cleaned);
        }
        catch (IOException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!seen.Add(fullPath))
        {
            return true;
        }

        if (!fullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return false;
        }

        paths.Add(fullPath);
        return true;
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
