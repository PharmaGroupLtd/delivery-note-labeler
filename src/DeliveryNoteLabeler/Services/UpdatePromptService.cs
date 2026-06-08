using System.Diagnostics;
using System.Windows;
using DeliveryNoteLabeler.Core.Updates;

namespace DeliveryNoteLabeler.Services;

public static class UpdatePromptService
{
    public static async Task CheckOnStartupAsync(Window owner)
    {
        var result = await CheckForUpdateAsync().ConfigureAwait(true);
        if (!ShouldPrompt(result))
        {
            return;
        }

        ShowUpdateDialog(owner, result.Manifest!);
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var checker = new UpdateChecker();
        return await checker.CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);
    }

    public static bool ShouldPrompt(UpdateCheckResult result)
    {
        if (!result.IsUpdateAvailable || result.Manifest is null)
        {
            return false;
        }

        var skipped = Core.Configuration.AppConfig.GetSkippedUpdateVersion();
        return !string.Equals(skipped, result.Manifest.Version, StringComparison.OrdinalIgnoreCase);
    }

    public static void ShowUpdateDialog(Window owner, UpdateManifest manifest)
    {
        var dialog = new UpdateAvailableWindow(manifest)
        {
            Owner = owner,
        };
        dialog.ShowDialog();
    }

    public static void OpenDownloadUrl(string? downloadUrl)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(downloadUrl)
        {
            UseShellExecute = true,
        });
    }
}
