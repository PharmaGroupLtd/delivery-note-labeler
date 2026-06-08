using System.Drawing.Printing;
using System.Windows;
using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Updates;
using DeliveryNoteLabeler.Services;

namespace DeliveryNoteLabeler;

public partial class SettingsWindow : Window
{
    private readonly LabelPrintService _printService = new();

    public SettingsWindow()
    {
        InitializeComponent();
        LoadPrinters();
        LoadGeminiSettings();
        CurrentVersionLabel.Text = $"Installed version: {AppVersion.Display}";
        InstallPathLabel.Text = $"Installed from: {Environment.ProcessPath ?? AppContext.BaseDirectory}";
        UpdateStatusLabel.Text = string.Empty;
    }

    private void LoadPrinters()
    {
        PrinterCombo.Items.Clear();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            PrinterCombo.Items.Add(printer);
        }

        var configured = AppConfig.GetPrinterName();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            PrinterCombo.Text = configured;
            PrinterStatusLabel.Text = "Printer configured.";
        }
        else
        {
            PrinterStatusLabel.Text = "No printer selected.";
        }
    }

    private void LoadGeminiSettings()
    {
        var existing = AppConfig.GetGeminiApiKey() ?? string.Empty;
        if (!string.IsNullOrEmpty(existing))
        {
            ApiKeyBox.Text = existing;
            CurrentKeyLabel.Text = $"Current key: {(existing.Length > 8 ? existing[..8] + "…" : existing)}";
            CurrentKeyLabel.Visibility = Visibility.Visible;
        }
    }

    private async void TestPrint_Click(object sender, RoutedEventArgs e)
    {
        var printerName = PrinterCombo.Text.Trim();
        if (string.IsNullOrWhiteSpace(printerName))
        {
            MessageBox.Show(
                "Select or type a printer name first.",
                "Test print",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        AppConfig.SavePrinterSettings(printerName);

        try
        {
            PrinterStatusLabel.Text = "Sending test label…";
            await _printService.PrintSampleAsync();
            PrinterStatusLabel.Text = "Test label sent.";
        }
        catch (Exception ex)
        {
            PrinterStatusLabel.Text = "Test print failed.";
            MessageBox.Show(ex.Message, "Test print failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        AppConfig.SaveGeminiApiKey(ApiKeyBox.Text);
        AppConfig.SavePrinterSettings(string.IsNullOrWhiteSpace(PrinterCombo.Text) ? null : PrinterCombo.Text.Trim());
        DialogResult = true;
        Close();
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusLabel.Text = "Checking for updates…";

        var result = await UpdatePromptService.CheckForUpdateAsync();
        switch (result.Status)
        {
            case UpdateCheckStatus.NotConfigured:
                UpdateStatusLabel.Text = "Update check is not configured yet.";
                MessageBox.Show(
                    "This install does not have an online update URL configured yet. Ask IT for the latest Setup.exe, or publish the app to GitHub Releases using scripts\\publish-release.ps1.",
                    "Check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                break;
            case UpdateCheckStatus.UpToDate:
                UpdateStatusLabel.Text = "You have the latest version.";
                MessageBox.Show(
                    $"Delivery Note Labeler {AppVersion.Display} is up to date.",
                    "Check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                break;
            case UpdateCheckStatus.UpdateAvailable when result.Manifest is not null:
                UpdateStatusLabel.Text = $"Version {result.Manifest.Version} is available.";
                UpdatePromptService.ShowUpdateDialog(this, result.Manifest);
                break;
            case UpdateCheckStatus.InvalidManifest:
                UpdateStatusLabel.Text = "Update information was invalid.";
                MessageBox.Show(
                    "The online update file could not be read.",
                    "Check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                break;
            default:
                UpdateStatusLabel.Text = "Could not check for updates.";
                MessageBox.Show(
                    result.ErrorMessage ?? "Could not reach the update server.",
                    "Check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                break;
        }
    }
}
