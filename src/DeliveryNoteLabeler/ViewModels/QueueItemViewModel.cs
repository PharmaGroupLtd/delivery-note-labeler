using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.ViewModels;

public sealed class QueueItemViewModel : INotifyPropertyChanged
{
    private QueueItemStatus _status = QueueItemStatus.Waiting;
    private DeliveryNote? _note;
    private List<LabelJob> _jobs = [];
    private string? _errorMessage;

    public QueueItemViewModel(string pdfPath)
    {
        PdfPath = pdfPath;
        FileName = Path.GetFileName(pdfPath);
    }

    public string PdfPath { get; }

    public string FileName { get; }

    public QueueItemStatus Status
    {
        get => _status;
        set
        {
            if (!SetField(ref _status, value))
            {
                return;
            }

            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(DisplaySubtitle));
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(CanPrint));
        }
    }

    public DeliveryNote? Note
    {
        get => _note;
        set
        {
            if (!SetField(ref _note, value))
            {
                return;
            }

            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(DisplaySubtitle));
        }
    }

    public List<LabelJob> Jobs
    {
        get => _jobs;
        set
        {
            if (!SetField(ref _jobs, value))
            {
                return;
            }

            OnPropertyChanged(nameof(DisplaySubtitle));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public string DisplayTitle => Note?.DeliveryNoteNo ?? FileName;

    public string DisplaySubtitle
    {
        get
        {
            if (Note is null)
            {
                return FileName;
            }

            return $"Order {Note.CustomerOrderNo} · {LabelJob.CountLabelsToPrint(Jobs)} labels";
        }
    }

    public string StatusLabel => Status switch
    {
        QueueItemStatus.Waiting => "Waiting to scan",
        QueueItemStatus.Scanning => "Scanning…",
        QueueItemStatus.Ready => "Ready to print",
        QueueItemStatus.Failed => ErrorMessage ?? "Scan failed",
        _ => string.Empty,
    };

    public bool CanPrint => Status == QueueItemStatus.Ready && Jobs.Count > 0;

    public void NotifyLabelRowsChanged()
    {
        OnPropertyChanged(nameof(DisplaySubtitle));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
