using System.Windows;
using DeliveryNoteLabeler.Core.Updates;
using DeliveryNoteLabeler.Services;

namespace DeliveryNoteLabeler;

public partial class UpdateAvailableWindow : Window
{
    private readonly UpdateManifest _manifest;

    public UpdateAvailableWindow(UpdateManifest manifest)
    {
        InitializeComponent();
        _manifest = manifest;

        VersionSummaryLabel.Text =
            $"Version {manifest.Version} is available. You are running {AppVersion.Display}.";

        ReleaseNotesLabel.Text = string.IsNullOrWhiteSpace(manifest.ReleaseNotes)
            ? "Download the installer and run it on this PC to update."
            : manifest.ReleaseNotes.Trim();
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        UpdatePromptService.OpenDownloadUrl(_manifest.DownloadUrl);
        DialogResult = true;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Core.Configuration.AppConfig.SaveSkippedUpdateVersion(_manifest.Version);
        DialogResult = false;
        Close();
    }
}
