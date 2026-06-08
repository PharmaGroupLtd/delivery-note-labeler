using System.Text.RegularExpressions;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Extraction;

public static partial class PdfTextParser
{
    public const string StatusScanningNormal = "Scanning with normal mode…";
    public const string StatusScanningAi = "Normal scan found nothing — scanning with AI…";

    [GeneratedRegex(
        @"^(\d+)\.\s+(PSM-\d+)\s+(.+?)\s+(\d{6,8}\s+REV\s+[A-Z])\s+(\d+)\s+ea\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex LineItemPattern();

    [GeneratedRegex(@"Delivery Note No\.?\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase)]
    private static partial Regex DeliveryNoteNoPattern();

    [GeneratedRegex(@"Customer Order No:?\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerOrderPattern();

    [GeneratedRegex(@"(\S+)\s+Customer Order No", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerOrderReversedPattern();

    [GeneratedRegex(@"Sales Order No:?\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex SalesOrderPattern();

    [GeneratedRegex(@"Customer Reference:?\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerReferencePattern();

    [GeneratedRegex(@"Date:?\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex DatePattern();

    public static List<LineItem> ParseLineItems(string text)
    {
        var items = new List<LineItem>();
        foreach (Match match in LineItemPattern().Matches(text))
        {
            items.Add(new LineItem
            {
                LineNo = int.Parse(match.Groups[1].Value),
                PartNo = match.Groups[2].Value.ToUpperInvariant(),
                Description = match.Groups[3].Value.Trim(),
                DrawingNo = match.Groups[4].Value.ToUpperInvariant(),
                Quantity = int.Parse(match.Groups[5].Value),
            });
        }

        return items;
    }

    public static HeaderFields ParseHeader(string pageText)
    {
        var deliveryMatch = DeliveryNoteNoPattern().Match(pageText);
        if (!deliveryMatch.Success)
        {
            throw new ExtractionException("Could not find Delivery Note No. in PDF.");
        }

        var customerOrderMatch = CustomerOrderPattern().Match(pageText);
        if (!customerOrderMatch.Success)
        {
            customerOrderMatch = CustomerOrderReversedPattern().Match(pageText);
        }

        if (!customerOrderMatch.Success)
        {
            throw new ExtractionException("Could not find Customer Order No. in PDF.");
        }

        var salesOrderMatch = SalesOrderPattern().Match(pageText);
        var customerRefMatch = CustomerReferencePattern().Match(pageText);
        var dateMatch = DatePattern().Match(pageText);

        return new HeaderFields
        {
            DeliveryNoteNo = deliveryMatch.Groups[1].Value.Trim(),
            CustomerOrderNo = customerOrderMatch.Groups[1].Value.Trim(),
            SalesOrderNo = salesOrderMatch.Success ? salesOrderMatch.Groups[1].Value.Trim() : null,
            CustomerReference = customerRefMatch.Success ? customerRefMatch.Groups[1].Value.Trim() : null,
            Date = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : null,
        };
    }

    public static List<LineItem> DedupeLineItems(IEnumerable<LineItem> items)
    {
        var seen = new HashSet<(int LineNo, string DrawingNo)>();
        var unique = new List<LineItem>();
        foreach (var item in items)
        {
            var key = (item.LineNo, item.DrawingNo);
            if (!seen.Add(key))
            {
                continue;
            }

            unique.Add(item);
        }

        return unique;
    }

    public sealed class HeaderFields
    {
        public required string DeliveryNoteNo { get; init; }
        public required string CustomerOrderNo { get; init; }
        public string? SalesOrderNo { get; init; }
        public string? CustomerReference { get; init; }
        public string? Date { get; init; }
    }
}
