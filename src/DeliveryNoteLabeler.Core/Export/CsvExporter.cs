using System.Globalization;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Export;

public static class CsvExporter
{
    private static readonly string[] Columns =
    [
        "delivery_note_no",
        "customer_order_no",
        "drawing_no",
        "line_no",
        "part_quantity",
        "label_quantity",
        "description",
    ];

    public static string ExportLabelJobsToCsv(IReadOnlyList<LabelJob> jobs, string outputPath)
    {
        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        writer.WriteLine(string.Join(',', Columns));

        foreach (var job in jobs)
        {
            var values = new[]
            {
                job.DeliveryNoteNo,
                job.CustomerOrderNo,
                job.DrawingNo,
                job.LineNo.ToString(CultureInfo.InvariantCulture),
                job.PartQuantity.ToString(CultureInfo.InvariantCulture),
                job.LabelQuantity.ToString(CultureInfo.InvariantCulture),
                job.Description,
            };

            writer.WriteLine(string.Join(',', values.Select(EscapeCsvField)));
        }

        return path;
    }

    private static string EscapeCsvField(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
