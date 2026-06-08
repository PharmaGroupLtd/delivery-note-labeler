using System.IO;
using System.Windows;
using DeliveryNoteLabeler.Core.Services;
using DeliveryNoteLabeler.Services;

namespace DeliveryNoteLabeler;

public partial class App : Application
{
    private SingleInstanceService? _singleInstance;
    private MainWindow? _mainWindow;
    private readonly List<string> _pendingPdfPaths = [];
    private readonly object _pendingPdfLock = new();

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                ShowFatalError(ex);
            }
        };

        DispatcherUnhandledException += (_, args) =>
        {
            ShowFatalError(args.Exception);
            args.Handled = true;
            Shutdown(-1);
        };

        var initialPdfs = PdfPathParser.ResolveStartupPdfPaths(e.Args);
        _singleInstance = new SingleInstanceService();

        if (!_singleInstance.IsPrimaryInstance)
        {
            var exitCode = 0;

            if (initialPdfs.Count == 0)
            {
                WritePrintLabelsLog("Secondary instance received no PDF paths.");
                MessageBox.Show(
                    "Delivery Note Labeler could not read the selected PDF file paths.\n\n"
                    + "Try Print Labels again. If this keeps happening, reinstall the latest version of the app.\n\n"
                    + $"Details: {GetPrintLabelsLogPath()}",
                    "Delivery Note Labeler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                exitCode = 1;
            }
            else if (!_singleInstance.TryForwardToExistingInstance(initialPdfs))
            {
                WritePrintLabelsLog(
                    $"Secondary instance could not forward {initialPdfs.Count} PDF path(s) to the running app.");
                MessageBox.Show(
                    "Delivery Note Labeler is still starting and could not receive the selected PDFs. Try Print Labels again.",
                    "Delivery Note Labeler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                exitCode = 1;
            }
            else
            {
                WritePrintLabelsLog($"Secondary instance forwarded {initialPdfs.Count} PDF path(s).");
            }

            _singleInstance.Dispose();
            Environment.Exit(exitCode);
        }

        var autoProcessQueue = initialPdfs.Count > 0;
        _singleInstance.StartListening(paths =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_mainWindow is null)
                {
                    lock (_pendingPdfLock)
                    {
                        _pendingPdfPaths.AddRange(paths);
                    }

                    return;
                }

                ActivateExistingWindow(paths);
            });
        });

        try
        {
            await _singleInstance.WaitForListenerReadyAsync(TimeSpan.FromSeconds(3));
        }
        catch (TimeoutException)
        {
            // Continue without blocking launch if the listener is slow to start.
        }

        _mainWindow = new MainWindow(initialPdfs, autoProcessQueue);
        _mainWindow.Show();

        List<string> pendingPaths;
        lock (_pendingPdfLock)
        {
            pendingPaths = [.. _pendingPdfPaths];
            _pendingPdfPaths.Clear();
        }

        if (pendingPaths.Count > 0)
        {
            ActivateExistingWindow(pendingPaths);
        }

        _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (_mainWindow is null)
        {
            return;
        }

        try
        {
            await UpdatePromptService.CheckOnStartupAsync(_mainWindow);
        }
        catch
        {
            // Ignore update-check failures during startup.
        }
    }

    private void ActivateExistingWindow(IReadOnlyList<string> paths)
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Activate();
        _mainWindow.Focus();

        if (paths.Count > 0 && _mainWindow.DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.EnqueuePdfs(paths);
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _singleInstance?.Dispose();
    }

    private static string GetPrintLabelsLogPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Delivery Note Labeler",
            "print-labels.log");

    private static void WritePrintLabelsLog(string message)
    {
        var logPath = GetPrintLabelsLogPath();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(
                logPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Delivery Note Labeler",
            "startup-error.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(
                logPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }

        MessageBox.Show(
            $"Delivery Note Labeler could not start.{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Details were saved to:{Environment.NewLine}{logPath}",
            "Delivery Note Labeler",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
