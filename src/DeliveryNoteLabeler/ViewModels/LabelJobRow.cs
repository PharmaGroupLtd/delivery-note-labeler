using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.ViewModels;

public sealed class LabelJobRow : INotifyPropertyChanged
{
    private readonly LabelJob _job;

    private LabelJobRow(LabelJob job)
    {
        _job = job;
    }

    public LabelJob Job => _job;

    public string DrawingNo
    {
        get => _job.DrawingNo;
        set
        {
            var cleaned = value.Trim();
            if (_job.DrawingNo == cleaned)
            {
                return;
            }

            _job.DrawingNo = cleaned;
            OnPropertyChanged();
        }
    }

    public string PartQty
    {
        get => _job.PartQuantity.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!TryParsePositiveQuantity(value, out var quantity) || _job.PartQuantity == quantity)
            {
                return;
            }

            _job.PartQuantity = quantity;
            OnPropertyChanged();
        }
    }

    public string LabelQty
    {
        get => _job.LabelQuantity.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!TryParsePositiveQuantity(value, out var quantity) || _job.LabelQuantity == quantity)
            {
                return;
            }

            _job.LabelQuantity = quantity;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _job.Description;
        set
        {
            if (_job.Description == value)
            {
                return;
            }

            _job.Description = value;
            OnPropertyChanged();
        }
    }

    public static LabelJobRow FromJob(LabelJob job, Action? onEdited = null)
    {
        var row = new LabelJobRow(job);
        if (onEdited is not null)
        {
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(PartQty) or nameof(LabelQty))
                {
                    onEdited();
                }
            };
        }

        return row;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static bool TryParsePositiveQuantity(string value, out int quantity)
    {
        quantity = 0;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity)
            && quantity > 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
