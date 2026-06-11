using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Printing;

public static class LabelPrintValidator
{
    public const int PartNumberCharsPerLine = 24;
    public const int PartNumberMaxChars = 48;

    public static int GetPartNumberLineCount(string? partNumber)
    {
        if (string.IsNullOrEmpty(partNumber))
        {
            return 1;
        }

        return Math.Max(1, (partNumber.Length + PartNumberCharsPerLine - 1) / PartNumberCharsPerLine);
    }

    public static bool TryValidatePartNumbers(IEnumerable<LabelJob> jobs, out string errorMessage)
    {
        foreach (var job in jobs)
        {
            if (job.DrawingNo.Length <= PartNumberMaxChars)
            {
                continue;
            }

            errorMessage =
                $"Part number \"{job.DrawingNo}\" on line {job.LineNo} is {job.DrawingNo.Length} characters. "
                + $"Maximum is {PartNumberMaxChars} characters ({PartNumberMaxChars / PartNumberCharsPerLine} lines of {PartNumberCharsPerLine}). "
                + "Please shorten it before printing.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
