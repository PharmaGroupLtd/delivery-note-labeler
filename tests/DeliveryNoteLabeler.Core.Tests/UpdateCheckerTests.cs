using DeliveryNoteLabeler.Core.Updates;

namespace DeliveryNoteLabeler.Core.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.0.3", "1.0.3")]
    [InlineData("1.0.3+build", "1.0.3")]
    public void NormalizeVersion_StripsBuildMetadata(string input, string expected)
    {
        Assert.Equal(expected, UpdateChecker.NormalizeVersion(input));
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNotConfiguredWhenManifestUrlMissing()
    {
        var checker = new UpdateChecker(
            httpClient: null,
            manifestUrlProvider: () => null,
            currentVersionProvider: () => new Version(1, 0, 2));

        var result = await checker.CheckForUpdateAsync();

        Assert.Equal(UpdateCheckStatus.NotConfigured, result.Status);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpToDateWhenRemoteMatchesCurrent()
    {
        var handler = new FakeManifestHandler("""
            {
              "version": "1.0.2",
              "downloadUrl": "https://example.com/setup.exe"
            }
            """);

        var checker = new UpdateChecker(
            new HttpClient(handler),
            () => "https://example.com/latest.json",
            () => new Version(1, 0, 2));

        var result = await checker.CheckForUpdateAsync();

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpdateAvailableWhenRemoteIsNewer()
    {
        var handler = new FakeManifestHandler("""
            {
              "version": "1.0.3",
              "downloadUrl": "https://example.com/DeliveryNoteLabeler-1.0.3-Setup.exe",
              "releaseNotes": "Fixes Print Labels on other PCs."
            }
            """);

        var checker = new UpdateChecker(
            new HttpClient(handler),
            () => "https://example.com/latest.json",
            () => new Version(1, 0, 2));

        var result = await checker.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.0.3", result.Manifest!.Version);
        Assert.Equal("Fixes Print Labels on other PCs.", result.Manifest.ReleaseNotes);
    }

    private sealed class FakeManifestHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            };
            return Task.FromResult(response);
        }
    }
}
