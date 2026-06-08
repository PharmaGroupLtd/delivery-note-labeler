using System.Net.Http;
using System.Text.Json;

namespace DeliveryNoteLabeler.Core.Updates;

public sealed class UpdateChecker
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly Func<string?> _manifestUrlProvider;
    private readonly Func<Version> _currentVersionProvider;

    public UpdateChecker(HttpClient? httpClient = null)
        : this(httpClient, UpdateSettings.GetManifestUrl, () => AppVersion.Current)
    {
    }

    internal UpdateChecker(
        HttpClient? httpClient,
        Func<string?> manifestUrlProvider,
        Func<Version> currentVersionProvider)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
        _manifestUrlProvider = manifestUrlProvider;
        _currentVersionProvider = currentVersionProvider;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var manifestUrl = _manifestUrlProvider()?.Trim();
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            return UpdateCheckResult.NotConfigured;
        }

        try
        {
            using var response = await _httpClient.GetAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return UpdateCheckResult.InvalidManifest;
            }

            if (!Version.TryParse(NormalizeVersion(manifest.Version), out var remoteVersion))
            {
                return UpdateCheckResult.InvalidManifest;
            }

            if (remoteVersion <= _currentVersionProvider())
            {
                return UpdateCheckResult.UpToDate;
            }

            if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            {
                return UpdateCheckResult.InvalidManifest;
            }

            return UpdateCheckResult.UpdateAvailable(manifest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"DeliveryNoteLabeler/{AppVersion.Display}");
        return client;
    }

    internal static string NormalizeVersion(string version)
    {
        var trimmed = version.Trim();
        var plusIndex = trimmed.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0 ? trimmed[..plusIndex] : trimmed;
    }
}
