using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.ViewModels;

public sealed partial class LabelJobRow : INotifyPropertyChanged
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

    public string Copy
    {
        get => _job.CopyLabel;
        set
        {
            var cleaned = value.Trim();
            if (!TryParseCopyLabel(cleaned, out var copyIndex, out var lineQuantity))
            {
                return;
            }

            if (_job.CopyIndex == copyIndex && _job.LineQuantity == lineQuantity)
            {
                return;
            }

            _job.CopyIndex = copyIndex;
            _job.LineQuantity = lineQuantity;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineQty));
        }
    }

    public string LineQty
    {
        get => _job.LineQuantity.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineQuantity)
                || lineQuantity <= 0
                || _job.LineQuantity == lineQuantity)
            {
                return;
            }

            _job.LineQuantity = lineQuantity;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Copy));
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

    public static LabelJobRow FromJob(LabelJob job) => new(job);

    public event PropertyChangedEventHandler? PropertyChanged;

    private static bool TryParseCopyLabel(string value, out int copyIndex, out int lineQuantity)
    {
        copyIndex = 0;
        lineQuantity = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = CopyLabelRegex().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(match.Groups["copy"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out copyIndex)
            && int.TryParse(match.Groups["total"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out lineQuantity)
            && copyIndex > 0
            && lineQuantity > 0;
    }

    [GeneratedRegex(@"^(?<copy>\d+)\s*/\s*(?<total>\d+)$")]
    private static partial Regex CopyLabelRegex();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
