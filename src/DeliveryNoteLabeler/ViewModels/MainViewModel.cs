using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Export;
using DeliveryNoteLabeler.Core.Extraction;
using DeliveryNoteLabeler.Core.Models;
using DeliveryNoteLabeler.Services;
using Microsoft.Win32;

namespace DeliveryNoteLabeler.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public const string StatusReady = "Drop a delivery note PDF here, or use Browse PDF";
    public const string StatusDrag = "Release to load PDF";

    private static readonly string DefaultPdfDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Delivery Note Scan");

    private readonly ExtractionPipeline _pipeline = new();
    private readonly LabelPrintService _printService = new();
    private Window? _ownerWindow;
    private QueueItemViewModel? _selectedQueueItem;
    private bool _scanOrchestrationRunning;
    private bool _printInProgress;
    private string _statusMessage = StatusReady;
    private bool _statusIsError;
    private bool _statusIsBold;
    private string _fileLabel = "No delivery note selected";
    private string _footerLabel = "0 labels";
    private string _queueSummaryLabel = string.Empty;
    private bool _showQueuePanel;
    private bool _canExport;
    private bool _canBrowse = true;
    private bool _canPrint;
    private bool _canPrintAll;
    private bool _canRetryScan;
    private bool _showRetryScanButton;
    private string _deliveryNoteStat = "—";
    private string _orderStat = "—";
    private string _linesStat = "—";
    private string _labelsStat = "—";
    private string _detailPlaceholder = StatusReady;
    private LabelJobRow? _selectedLabelRow;

    public ObservableCollection<QueueItemViewModel> QueueItems { get; } = [];

    public ObservableCollection<LabelJobRow> LabelRows { get; } = [];

    public LabelJobRow? SelectedLabelRow
    {
        get => _selectedLabelRow;
        set
        {
            if (!SetField(ref _selectedLabelRow, value))
            {
                return;
            }

            DeleteLabelLineCommand.RaiseCanExecuteChanged();
        }
    }

    public QueueItemViewModel? SelectedQueueItem
    {
        get => _selectedQueueItem;
        set
        {
            if (!SetField(ref _selectedQueueItem, value))
            {
                return;
            }

            DisplaySelectedQueueItem();
            UpdateCommandStates();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetField(ref _statusIsError, value);
    }

    public bool StatusIsBold
    {
        get => _statusIsBold;
        private set => SetField(ref _statusIsBold, value);
    }

    public string FileLabel
    {
        get => _fileLabel;
        private set => SetField(ref _fileLabel, value);
    }

    public string FooterLabel
    {
        get => _footerLabel;
        private set => SetField(ref _footerLabel, value);
    }

    public string QueueSummaryLabel
    {
        get => _queueSummaryLabel;
        private set => SetField(ref _queueSummaryLabel, value);
    }

    public bool ShowQueuePanel
    {
        get => _showQueuePanel;
        private set => SetField(ref _showQueuePanel, value);
    }

    public bool CanExport
    {
        get => _canExport;
        private set => SetField(ref _canExport, value);
    }

    public bool CanBrowse
    {
        get => _canBrowse;
        private set => SetField(ref _canBrowse, value);
    }

    public bool CanPrint
    {
        get => _canPrint;
        private set => SetField(ref _canPrint, value);
    }

    public bool CanPrintAll
    {
        get => _canPrintAll;
        private set => SetField(ref _canPrintAll, value);
    }

    public bool CanRetryScan
    {
        get => _canRetryScan;
        private set => SetField(ref _canRetryScan, value);
    }

    public bool ShowRetryScanButton
    {
        get => _showRetryScanButton;
        private set => SetField(ref _showRetryScanButton, value);
    }

    public string DeliveryNoteStat
    {
        get => _deliveryNoteStat;
        private set => SetField(ref _deliveryNoteStat, value);
    }

    public string OrderStat
    {
        get => _orderStat;
        private set => SetField(ref _orderStat, value);
    }

    public string LinesStat
    {
        get => _linesStat;
        private set => SetField(ref _linesStat, value);
    }

    public string LabelsStat
    {
        get => _labelsStat;
        private set => SetField(ref _labelsStat, value);
    }

    public string DetailPlaceholder
    {
        get => _detailPlaceholder;
        private set => SetField(ref _detailPlaceholder, value);
    }

    public bool HasLabelRows => LabelRows.Count > 0;

    public bool ShowDetailPlaceholder => !HasLabelRows;

    public RelayCommand BrowseCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand PrintAllCommand { get; }
    public RelayCommand AddLabelLineCommand { get; }
    public RelayCommand DeleteLabelLineCommand { get; }
    public RelayCommand RetryScanCommand { get; }

    public MainViewModel()
    {
        BrowseCommand = new RelayCommand(BrowsePdf, () => CanBrowse);
        ExportCommand = new RelayCommand(ExportCsv, () => CanExport);
        SettingsCommand = new RelayCommand(OpenSettingsFromCommand);
        PrintCommand = new RelayCommand(PrintSelected, () => CanPrint);
        PrintAllCommand = new RelayCommand(PrintAllReady, () => CanPrintAll);
        AddLabelLineCommand = new RelayCommand(AddLabelLine, () => CanEditLabelRows);
        DeleteLabelLineCommand = new RelayCommand(DeleteSelectedLabelLine, () => CanDeleteLabelLine);
        RetryScanCommand = new RelayCommand(RetryScan, () => CanRetryScan);
    }

    private bool CanEditLabelRows =>
        SelectedQueueItem?.Status == QueueItemStatus.Ready && SelectedQueueItem.Note is not null;

    private bool CanDeleteLabelLine =>
        CanEditLabelRows && SelectedLabelRow is not null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AttachOwner(Window ownerWindow) => _ownerWindow = ownerWindow;

    public void SetStatusInvalidDrop()
    {
        SetStatus("Please drop a valid PDF file.", error: true);
    }

    public void Initialize(IReadOnlyList<string> initialPdfs, bool _)
    {
        QueueItems.Clear();
        AddPdfPathsToQueue(initialPdfs);
        SetStatus(
            QueueItems.Count > 0
                ? $"Scanning {QueueItems.Count} delivery note(s)…"
                : StatusReady,
            muted: QueueItems.Count == 0);

        if (QueueItems.Count == 0)
        {
            ShowEmptyState();
            return;
        }

        StartScanOrchestration();
    }

    public void EnqueuePdfs(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        var added = AddPdfPathsToQueue(paths);
        if (added == 0)
        {
            return;
        }

        SetStatus($"Added {added} PDF(s) to the queue. Scanning…", muted: true);
        StartScanOrchestration();
    }

    public void BrowsePdf()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select delivery note PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            InitialDirectory = Directory.Exists(DefaultPdfDir) ? DefaultPdfDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        QueueItems.Clear();
        AddPdfPathsToQueue([dialog.FileName]);
        SelectedQueueItem = null;
        StartScanOrchestration();
    }

    public void LoadDroppedPdfs(IReadOnlyList<string> paths)
    {
        QueueItems.Clear();
        AddPdfPathsToQueue(paths);
        SelectedQueueItem = null;
        StartScanOrchestration();
    }

    public void LoadDroppedPdf(string path)
    {
        LoadDroppedPdfs([path]);
    }

    public void ExportCsv()
    {
        if (SelectedQueueItem?.Note is null || LabelRows.Count == 0)
        {
            return;
        }

        var note = SelectedQueueItem.Note;
        var jobs = GetCurrentLabelJobs();
        var defaultName = "label_jobs.csv";
        if (!string.IsNullOrEmpty(note.SourcePath))
        {
            defaultName = $"{Path.GetFileNameWithoutExtension(note.SourcePath)}_labels.csv";
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export label jobs",
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = defaultName,
            InitialDirectory = Directory.Exists(DefaultPdfDir) ? DefaultPdfDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var outputPath = CsvExporter.ExportLabelJobsToCsv(jobs, dialog.FileName);
            var labelCount = LabelJob.CountLabelsToPrint(jobs);
            SetStatus($"Exported {jobs.Count} rows ({labelCount} labels) to {Path.GetFileName(outputPath)}", success: true);
            MessageBox.Show(
                $"Saved {jobs.Count} rows ({labelCount} labels) to:\n{outputPath}",
                "Export complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (IOException ex)
        {
            SetStatus($"Could not export CSV: {ex.Message}", error: true);
            MessageBox.Show(ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void OpenSettings(Window owner)
    {
        var dialog = new SettingsWindow
        {
            Owner = owner,
        };

        if (dialog.ShowDialog() == true)
        {
            UpdateCommandStates();

            if (AppConfig.GeminiFallbackAvailable())
            {
                SetStatus(
                    SelectedQueueItem?.Status == QueueItemStatus.Failed
                        ? "Gemini API key saved. Use Retry scan to try again."
                        : "Gemini API key saved.",
                    success: true);
            }
            else if (AppConfig.IsPrinterConfigured())
            {
                SetStatus($"Printer set to {AppConfig.GetPrinterName()}.", success: true);
            }
            else
            {
                SetStatus("Gemini API key cleared.", muted: true);
            }
        }
    }

    public void SetDragStatus()
    {
        SetStatus(StatusDrag, primary: true);
    }

    public void ClearDragStatus()
    {
        if (SelectedQueueItem?.Status == QueueItemStatus.Ready)
        {
            SetStatus(
                $"Reviewing {SelectedQueueItem.DisplayTitle}: {LabelJob.CountLabelsToPrint(SelectedQueueItem.Jobs)} labels ready to print.",
                success: true);
            return;
        }

        if (QueueItems.Any(item => item.Status is QueueItemStatus.Waiting or QueueItemStatus.Scanning))
        {
            SetStatus($"Scanning delivery notes… {ReadyCount()} ready, {PendingScanCount()} remaining.", muted: true);
            return;
        }

        SetStatus(StatusReady, muted: true);
    }

    private int AddPdfPathsToQueue(IReadOnlyList<string> paths)
    {
        var existing = new HashSet<string>(QueueItems.Select(item => item.PdfPath), StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var path in paths)
        {
            if (!existing.Add(path))
            {
                continue;
            }

            QueueItems.Add(new QueueItemViewModel(path));
            added++;
        }

        UpdateQueueSummary();
        return added;
    }

    private void StartScanOrchestration()
    {
        if (_scanOrchestrationRunning)
        {
            return;
        }

        _scanOrchestrationRunning = true;
        CanBrowse = false;
        UpdateCommandStates();
        _ = RunScanOrchestrationAsync();
    }

    private async Task RunScanOrchestrationAsync()
    {
        try
        {
            while (true)
            {
                var next = QueueItems.FirstOrDefault(item => item.Status == QueueItemStatus.Waiting);
                if (next is null)
                {
                    break;
                }

                await ScanQueueItemAsync(next).ConfigureAwait(false);
            }
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _scanOrchestrationRunning = false;
                CanBrowse = true;
                EnsureSelection();
                UpdateQueueSummary();
                UpdateCommandStates();

                if (QueueItems.Count == 0)
                {
                    ShowEmptyState();
                    SetStatus(StatusReady, muted: true);
                    return;
                }

                var failedCount = QueueItems.Count(item => item.Status == QueueItemStatus.Failed);
                if (ReadyCount() == 0 && failedCount > 0)
                {
                    SetStatus("Could not scan any of the selected PDFs.", error: true);
                    return;
                }

                if (ReadyCount() > 0)
                {
                    SetStatus(
                        failedCount > 0
                            ? $"{ReadyCount()} delivery note(s) ready to print. {failedCount} failed to scan."
                            : $"{ReadyCount()} delivery note(s) ready to print.",
                        success: true);
                }
            });
        }
    }

    private async Task ScanQueueItemAsync(QueueItemViewModel item)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            item.ErrorMessage = null;
            item.Status = QueueItemStatus.Scanning;
            UpdateQueueSummary();
            SetStatus($"Scanning {item.FileName} with AI… ({ReadyCount()} ready, {PendingScanCount()} remaining)", muted: true);

            if (SelectedQueueItem is null || SelectedQueueItem == item)
            {
                SelectedQueueItem = item;
            }
        });

        try
        {
            var (note, jobs) = await Task.Run(() =>
                _pipeline.ExtractLabelJobs(
                    item.PdfPath,
                    message => Application.Current.Dispatcher.Invoke(() =>
                        SetStatus($"Scanning {item.FileName}… {message}", muted: true)))).ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                item.Note = note;
                item.Jobs = jobs;
                item.Status = QueueItemStatus.Ready;
                UpdateQueueSummary();

                if (SelectedQueueItem is null)
                {
                    SelectedQueueItem = item;
                }
                else if (SelectedQueueItem == item)
                {
                    DisplaySelectedQueueItem();
                }

                UpdateCommandStates();
            });
        }
        catch (ExtractionException ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                item.Status = QueueItemStatus.Failed;
                item.ErrorMessage = ex.Message;
                UpdateQueueSummary();

                if (SelectedQueueItem is null)
                {
                    SelectedQueueItem = item;
                }
                else if (SelectedQueueItem == item)
                {
                    DisplaySelectedQueueItem();
                }

                UpdateCommandStates();
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                item.Status = QueueItemStatus.Failed;
                item.ErrorMessage = $"Unexpected error: {ex.Message}";
                UpdateQueueSummary();

                if (SelectedQueueItem is null)
                {
                    SelectedQueueItem = item;
                }
                else if (SelectedQueueItem == item)
                {
                    DisplaySelectedQueueItem();
                }

                UpdateCommandStates();
            });
        }
    }

    private void PrintSelected()
    {
        if (!EnsurePrinterConfigured())
        {
            return;
        }

        if (SelectedQueueItem is null || !SelectedQueueItem.CanPrint || _printInProgress)
        {
            return;
        }

        _ = PrintQueueItemsAsync([SelectedQueueItem]);
    }

    private void PrintAllReady()
    {
        if (!EnsurePrinterConfigured())
        {
            return;
        }

        if (_printInProgress)
        {
            return;
        }

        var readyItems = QueueItems.Where(item => item.CanPrint).ToList();
        if (readyItems.Count == 0)
        {
            return;
        }

        _ = PrintQueueItemsAsync(readyItems);
    }

    private bool EnsurePrinterConfigured()
    {
        if (AppConfig.IsPrinterConfigured())
        {
            return true;
        }

        SetStatus("Configure a label printer in Settings before printing.", error: true);
        MessageBox.Show(
            "No label printer is configured.\n\nInstall the shared GK420d printer from your print host, then open Settings and select it.",
            "Printer not configured",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private async Task PrintQueueItemsAsync(IReadOnlyList<QueueItemViewModel> items)
    {
        _printInProgress = true;
        CanBrowse = false;
        UpdateCommandStates();

        var printed = 0;
        try
        {
            foreach (var item in items.ToList())
            {
                if (!item.CanPrint || item.Note is null)
                {
                    continue;
                }

                SetStatus($"Printing {item.DisplayTitle}… ({printed + 1}/{items.Count})", muted: true);

                try
                {
                    await _printService.PrintAsync(item.Note, item.Jobs).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    SetStatus($"Could not print {item.DisplayTitle}: {ex.Message}", error: true);
                    MessageBox.Show(ex.Message, "Print failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                }

                var index = QueueItems.IndexOf(item);
                QueueItems.Remove(item);
                printed++;

                if (QueueItems.Count == 0)
                {
                    SelectedQueueItem = null;
                    ShowEmptyState();
                    SetStatus($"Printed {printed} delivery note(s). Queue complete.", success: true);
                    return;
                }

                SelectNextQueueItem(index);
                UpdateQueueSummary();
            }

            if (printed > 0)
            {
                var remainingReady = ReadyCount();
                SetStatus(
                    remainingReady > 0
                        ? $"Printed {printed} delivery note(s). {remainingReady} remaining in queue."
                        : $"Printed {printed} delivery note(s). Queue complete.",
                    success: true);
            }
        }
        finally
        {
            _printInProgress = false;
            CanBrowse = true;
            EnsureSelection();
            UpdateQueueSummary();
            UpdateCommandStates();
        }
    }

    private void SelectNextQueueItem(int removedIndex)
    {
        if (QueueItems.Count == 0)
        {
            SelectedQueueItem = null;
            return;
        }

        var nextIndex = Math.Min(removedIndex, QueueItems.Count - 1);
        SelectedQueueItem = QueueItems[nextIndex];
    }

    private void EnsureSelection()
    {
        if (SelectedQueueItem is not null && QueueItems.Contains(SelectedQueueItem))
        {
            DisplaySelectedQueueItem();
            return;
        }

        SelectedQueueItem = QueueItems.FirstOrDefault(item => item.Status == QueueItemStatus.Ready)
            ?? QueueItems.FirstOrDefault(item => item.Status == QueueItemStatus.Scanning)
            ?? QueueItems.FirstOrDefault(item => item.Status == QueueItemStatus.Waiting)
            ?? QueueItems.FirstOrDefault(item => item.Status == QueueItemStatus.Failed)
            ?? QueueItems.FirstOrDefault();
    }

    private void DisplaySelectedQueueItem()
    {
        var item = SelectedQueueItem;
        if (item is null)
        {
            ShowEmptyState();
            return;
        }

        switch (item.Status)
        {
            case QueueItemStatus.Waiting:
                DetailPlaceholder = "Waiting to scan this delivery note…";
                ClearDetailData();
                FileLabel = item.FileName;
                FooterLabel = "Not scanned yet";
                SetStatus($"Waiting to scan {item.FileName}.", muted: true);
                break;

            case QueueItemStatus.Scanning:
                DetailPlaceholder = $"Scanning {item.FileName}…";
                ClearDetailData();
                FileLabel = item.FileName;
                FooterLabel = "Scanning";
                break;

            case QueueItemStatus.Failed:
                DetailPlaceholder = item.ErrorMessage ?? "This delivery note could not be scanned.";
                ClearDetailData();
                FileLabel = item.FileName;
                FooterLabel = "Scan failed — try Retry scan";
                SetStatus(item.ErrorMessage ?? "Could not scan PDF.", error: true);
                break;

            case QueueItemStatus.Ready when item.Note is not null:
                var note = item.Note;
                var method = ExtractionMethodLabel(note);
                var labelsToPrint = LabelJob.CountLabelsToPrint(item.Jobs);
                FileLabel = $"{note.DeliveryNoteNo} · Order {note.CustomerOrderNo} · {method}";
                SelectedLabelRow = null;
                PopulateTable(item.Jobs);
                RefreshLabelSummary();
                DetailPlaceholder = string.Empty;
                SetStatus(
                    $"Review {note.DeliveryNoteNo}: {labelsToPrint} labels ready to print.",
                    success: true);
                break;
        }

        CanExport = item.Status == QueueItemStatus.Ready && LabelRows.Count > 0;
        UpdateCommandStates();
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
    }

    private void ClearDetailData()
    {
        SelectedLabelRow = null;
        UpdateStats(null);
        LabelRows.Clear();
        CanExport = false;
        OnPropertyChanged(nameof(HasLabelRows));
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
    }

    private void UpdateQueueSummary()
    {
        ShowQueuePanel = QueueItems.Count > 0;
        if (QueueItems.Count == 0)
        {
            QueueSummaryLabel = string.Empty;
            return;
        }

        QueueSummaryLabel = $"{ReadyCount()} ready · {PendingScanCount()} scanning · {QueueItems.Count} total";
    }

    private int ReadyCount() => QueueItems.Count(item => item.Status == QueueItemStatus.Ready);

    private int PendingScanCount() =>
        QueueItems.Count(item => item.Status is QueueItemStatus.Waiting or QueueItemStatus.Scanning);

    private void RetryScan()
    {
        var item = SelectedQueueItem;
        if (item?.Status != QueueItemStatus.Failed || _scanOrchestrationRunning || _printInProgress)
        {
            return;
        }

        if (!AppConfig.GeminiFallbackAvailable())
        {
            SetStatus("Add a Gemini API key in Settings to scan delivery notes.", error: true);
            OpenSettingsFromCommand();
            return;
        }

        _ = ScanQueueItemAsync(item);
    }

    private void UpdateCommandStates()
    {
        var printerConfigured = AppConfig.IsPrinterConfigured();
        CanPrint = printerConfigured
            && SelectedQueueItem?.CanPrint == true
            && !_printInProgress;
        CanPrintAll = printerConfigured
            && QueueItems.Any(item => item.CanPrint)
            && !_printInProgress;
        CanExport = SelectedQueueItem?.Status == QueueItemStatus.Ready && LabelRows.Count > 0;
        ShowRetryScanButton = SelectedQueueItem?.Status == QueueItemStatus.Failed;
        CanRetryScan = ShowRetryScanButton
            && !_scanOrchestrationRunning
            && !_printInProgress;

        BrowseCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        PrintCommand.RaiseCanExecuteChanged();
        PrintAllCommand.RaiseCanExecuteChanged();
        AddLabelLineCommand.RaiseCanExecuteChanged();
        DeleteLabelLineCommand.RaiseCanExecuteChanged();
        RetryScanCommand.RaiseCanExecuteChanged();
    }

    private void OpenSettingsFromCommand()
    {
        var owner = _ownerWindow ?? Application.Current.MainWindow;
        if (owner is not null)
        {
            OpenSettings(owner);
        }
    }

    private static string ExtractionMethodLabel(DeliveryNote note) =>
        note.GeminiModel is not null ? $"AI scan ({note.GeminiModel})" : "AI scan";

    private void UpdateStats(DeliveryNote? note, int lineCount = 0, int labelCount = 0)
    {
        if (note is null)
        {
            DeliveryNoteStat = "—";
            OrderStat = "—";
            LinesStat = "—";
            LabelsStat = "—";
            return;
        }

        DeliveryNoteStat = note.DeliveryNoteNo;
        OrderStat = note.CustomerOrderNo;
        LinesStat = note.LineItems.Count.ToString();
        LabelsStat = labelCount > 0 ? labelCount.ToString() : lineCount.ToString();
    }

    private void PopulateTable(IReadOnlyList<LabelJob> jobs)
    {
        LabelRows.Clear();
        foreach (var job in jobs)
        {
            LabelRows.Add(LabelJobRow.FromJob(job, RefreshLabelSummary));
        }

        OnPropertyChanged(nameof(HasLabelRows));
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
    }

    private void AddLabelLine()
    {
        if (SelectedQueueItem?.Note is null)
        {
            return;
        }

        var note = SelectedQueueItem.Note;
        var nextLineNo = LabelRows.Count == 0
            ? 1
            : LabelRows.Max(row => row.Job.LineNo) + 1;
        var job = LabelJobExpander.CreateManualLine(note, nextLineNo);
        SelectedQueueItem.Jobs.Add(job);
        var row = LabelJobRow.FromJob(job, RefreshLabelSummary);
        LabelRows.Add(row);
        SelectedLabelRow = row;
        RefreshLabelSummary();
    }

    private void DeleteSelectedLabelLine()
    {
        if (SelectedQueueItem is null || SelectedLabelRow is null)
        {
            return;
        }

        SelectedQueueItem.Jobs.Remove(SelectedLabelRow.Job);
        LabelRows.Remove(SelectedLabelRow);
        SelectedLabelRow = null;
        RefreshLabelSummary();
    }

    private IReadOnlyList<LabelJob> GetCurrentLabelJobs()
    {
        return LabelRows.Select(row => row.Job).ToList();
    }

    private void RefreshLabelSummary()
    {
        if (SelectedQueueItem?.Note is null)
        {
            return;
        }

        var rowCount = LabelRows.Count;
        var labelsToPrint = rowCount == 0 ? 0 : LabelJob.CountLabelsToPrint(GetCurrentLabelJobs());
        FooterLabel = rowCount == 0
            ? "No label lines"
            : $"{labelsToPrint} labels from {rowCount} line items";
        UpdateStats(SelectedQueueItem.Note, rowCount, labelsToPrint);
        SelectedQueueItem.NotifyLabelRowsChanged();
        OnPropertyChanged(nameof(HasLabelRows));
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
        CanExport = SelectedQueueItem.Status == QueueItemStatus.Ready && rowCount > 0;
        UpdateCommandStates();

        if (SelectedQueueItem.Status == QueueItemStatus.Ready && rowCount > 0)
        {
            SetStatus(
                $"Review {SelectedQueueItem.Note.DeliveryNoteNo}: {labelsToPrint} labels ready to print.",
                success: true);
        }
    }

    private void ShowEmptyState()
    {
        SelectedLabelRow = null;
        FileLabel = "No delivery note selected";
        DetailPlaceholder = StatusReady;
        UpdateStats(null);
        FooterLabel = "0 labels";
        LabelRows.Clear();
        CanExport = false;
        OnPropertyChanged(nameof(HasLabelRows));
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
    }

    private void SetStatus(string message, bool muted = false, bool success = false, bool error = false, bool primary = false)
    {
        StatusMessage = message;
        StatusIsBold = primary || success || error;
        StatusIsError = error;
        if (!primary && !success && !error)
        {
            StatusIsBold = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        RefreshCommands(propertyName);
        return true;
    }

    private void RefreshCommands(string? propertyName)
    {
        if (propertyName is nameof(CanBrowse))
        {
            BrowseCommand.RaiseCanExecuteChanged();
        }

        if (propertyName is nameof(CanExport))
        {
            ExportCommand.RaiseCanExecuteChanged();
        }

        if (propertyName is nameof(CanPrint))
        {
            PrintCommand.RaiseCanExecuteChanged();
        }

        if (propertyName is nameof(CanPrintAll))
        {
            PrintAllCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
