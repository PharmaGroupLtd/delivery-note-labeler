using DeliveryNoteLabeler.Core.Services;

namespace DeliveryNoteLabeler.Core.Tests;

public class PdfPathParserTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void ParseInitialPdfPaths_SplitsMultipleArguments()
    {
        var one = CreateTempPdf("one");
        var two = CreateTempPdf("two");

        var paths = PdfPathParser.ParseInitialPdfPaths([one, two]);

        Assert.Equal(2, paths.Count);
        Assert.Contains(one, paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(two, paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseInitialPdfPaths_SplitsQuotedTokensInSingleArgument()
    {
        var one = CreateTempPdf("one");
        var two = CreateTempPdf("two");

        var paths = PdfPathParser.ParseInitialPdfPaths([$"\"{one}\" \"{two}\""]);

        Assert.Equal(2, paths.Count);
    }

    [Fact]
    public void ParseInitialPdfPaths_DeduplicatesPaths()
    {
        var one = CreateTempPdf("one");

        var paths = PdfPathParser.ParseInitialPdfPaths([one, one]);

        Assert.Single(paths);
    }

    [Fact]
    public void ResolveStartupPdfPaths_UsesStartupArgsOnly()
    {
        var pdf = CreateTempPdf("startup-only");
        var listPath = Path.Combine(Path.GetTempPath(), $"delivery-note-labeler-list-{Guid.NewGuid():N}.pdflist");
        _tempFiles.Add(listPath);
        File.WriteAllText(listPath, pdf);

        var paths = PdfPathParser.ResolveStartupPdfPaths(
            [$"--open-from", listPath, "C:\\ignored\\missing.pdf"]);

        Assert.Single(paths);
        Assert.Contains(pdf, paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseForwardedPdfPaths_AcceptsLegacyPrintLabelsCmdLine()
    {
        var pdf = CreateTempPdf("legacy");
        var malformed = $"\"{pdf}\")";

        var paths = PdfPathParser.ParseForwardedPdfPaths([malformed]);

        Assert.Single(paths);
        Assert.Contains(pdf, paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadOpenFromListFile_LoadsMultiplePdfPaths()
    {
        var one = CreateTempPdf("one");
        var two = CreateTempPdf("two");
        var listPath = Path.Combine(Path.GetTempPath(), $"delivery-note-labeler-list-{Guid.NewGuid():N}.pdflist");
        _tempFiles.Add(listPath);
        File.WriteAllText(listPath, $"{one}{Environment.NewLine}{two}{Environment.NewLine}");

        var paths = PdfPathParser.ReadOpenFromListFile(listPath);

        Assert.Equal(2, paths.Count);
        Assert.Contains(one, paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(two, paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseInitialPdfPaths_PreservesPathsWithSpaces()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"delivery-note-labeler scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        _tempFiles.Add(folder);

        var pdf = Path.Combine(folder, "deliverynote004223 rev 1.pdf");
        File.WriteAllBytes(pdf, "%PDF-1.4"u8.ToArray());
        _tempFiles.Add(pdf);

        var paths = PdfPathParser.ParseInitialPdfPaths([pdf]);

        Assert.Single(paths);
        Assert.Contains(pdf, paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseInitialPdfPaths_IgnoresUnusedExplorerPlaceholders()
    {
        var pdf = CreateTempPdf("one");
        var args = new[] { pdf, string.Empty, string.Empty, string.Empty };

        var paths = PdfPathParser.ParseInitialPdfPaths(args);

        Assert.Single(paths);
        Assert.Contains(pdf, paths, StringComparer.OrdinalIgnoreCase);
    }

    private string CreateTempPdf(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"delivery-note-labeler-{name}-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, "%PDF-1.4"u8.ToArray());
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
