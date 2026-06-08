using System.Text.RegularExpressions;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Extraction;

public static partial class PdfTextParser
{
    public const string StatusScanningNormal = "Scanning with normal mode…";
    public const string StatusScanningAi = "No readable text found — scanning with AI…";

    [GeneratedRegex(
        @"^(\d+)\.\s+(PSM-\d+)\s+(.+?)\s+(\d{5,9}\s+REV\s+[A-Z0-9]+)\s+(\d+)\s*(?:ea|each)?\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex LineItemPattern();

    [GeneratedRegex(
        @"(\d+)\.\s+(PSM-\d+)\s+(.+?)\s+(\d{5,9}\s+REV\s+[A-Z0-9]+)\s+(\d+)\s*(?:ea|each)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex LineItemSearchPattern();

    [GeneratedRegex(@"Delivery Note No\.?\s*([^\n\r]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DeliveryNoteNoPattern();

    [GeneratedRegex(@"Customer Order No:?\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerOrderPattern();

    [GeneratedRegex(@"(\S+)\s+Customer Order No", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerOrderReversedPattern();

    [GeneratedRegex(@"Customer Order No:?\s*[\r\n]+\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerOrderMultilinePattern();

    [GeneratedRegex(@"Sales Order No:?\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex SalesOrderPattern();

    [GeneratedRegex(@"Sales Order No:?\s*[\r\n]+\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex SalesOrderMultilinePattern();

    [GeneratedRegex(@"Customer Reference:?\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerReferencePattern();

    [GeneratedRegex(@"Date:?\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^(?<label>[A-Za-z][A-Za-z /]+:)\s*[\r\n]+\s*(?<value>\S[^\r\n]*)", RegexOptions.Multiline)]
    private static partial Regex LabelValueLineBreakPattern();

    public static string NormalizeExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = LabelValueLineBreakPattern().Replace(normalized, "${label} ${value}");
        normalized = Regex.Replace(normalized, "[ \t]+", " ");
        return normalized.Trim();
    }

    [GeneratedRegex(
        @"^\d+\.\s+PSM-\d+\s+.+\s+\d{5,9}\s+REV\s+[A-Z0-9]+\s+\d+\s*(?:ea|each)?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex CompleteLineItemPattern();

    public static string MergeWrappedLineItems(string text)
    {
        var normalized = NormalizeExtractedText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var mergedLines = new List<string>();
        var current = string.Empty;

        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (LineStartsItem(line))
            {
                if (!string.IsNullOrEmpty(current))
                {
                    mergedLines.Add(current.Trim());
                }

                current = line;
                continue;
            }

            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            if (!IsCompleteLineItem(current) && IsLineItemContinuation(line))
            {
                current += " " + line;
                continue;
            }

            mergedLines.Add(current.Trim());
            current = string.Empty;
        }

        if (!string.IsNullOrEmpty(current))
        {
            mergedLines.Add(current.Trim());
        }

        return string.Join('\n', mergedLines);
    }

    public static List<LineItem> ParseLineItems(string text)
    {
        var merged = MergeWrappedLineItems(text);
        var items = ParseLineItemsFromMergedText(merged);
        if (items.Count > 0)
        {
            return items;
        }

        return ParseLineItemsFromMergedText(NormalizeExtractedText(text));
    }

    private static List<LineItem> ParseLineItemsFromMergedText(string text)
    {
        var items = new List<LineItem>();
        foreach (Match match in LineItemPattern().Matches(text))
        {
            TryAddLineItem(match, items);
        }

        if (items.Count > 0)
        {
            return items;
        }

        foreach (Match match in LineItemSearchPattern().Matches(text))
        {
            TryAddLineItem(match, items);
        }

        return items;
    }

    public static HeaderFields ParseHeader(string pageText)
    {
        var text = NormalizeExtractedText(pageText);

        var deliveryMatch = DeliveryNoteNoPattern().Match(text);
        if (!deliveryMatch.Success)
        {
            throw new ExtractionException("Could not find Delivery Note No. in PDF.");
        }

        var customerOrderMatch = CustomerOrderPattern().Match(text);
        if (!customerOrderMatch.Success)
        {
            customerOrderMatch = CustomerOrderReversedPattern().Match(text);
        }

        if (!customerOrderMatch.Success)
        {
            customerOrderMatch = CustomerOrderMultilinePattern().Match(text);
        }

        if (!customerOrderMatch.Success)
        {
            throw new ExtractionException("Could not find Customer Order No. in PDF.");
        }

        var salesOrderMatch = SalesOrderPattern().Match(text);
        if (!salesOrderMatch.Success)
        {
            salesOrderMatch = SalesOrderMultilinePattern().Match(text);
        }

        var customerRefMatch = CustomerReferencePattern().Match(text);
        var dateMatch = DatePattern().Match(text);

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

    private static bool LineStartsItem(string line) =>
        line.Length > 0 && char.IsDigit(line[0]) && line.Contains("PSM-", StringComparison.OrdinalIgnoreCase);

    private static bool IsLineItemContinuation(string line) =>
        Regex.IsMatch(line, @"^\d{5,9}\s+REV\s+", RegexOptions.IgnoreCase)
        || Regex.IsMatch(line, @"^\d+\s*(?:ea|each)\b", RegexOptions.IgnoreCase)
        || (line.Length <= 48 && !line.Contains(':') && !line.StartsWith("--", StringComparison.Ordinal));

    private static bool IsCompleteLineItem(string line) => CompleteLineItemPattern().IsMatch(line.Trim());

    private static void TryAddLineItem(Match match, ICollection<LineItem> items)
    {
        if (!match.Success)
        {
            return;
        }

        items.Add(new LineItem
        {
            LineNo = int.Parse(match.Groups[1].Value),
            PartNo = match.Groups[2].Value.ToUpperInvariant(),
            Description = match.Groups[3].Value.Trim(),
            DrawingNo = match.Groups[4].Value.ToUpperInvariant(),
            Quantity = int.Parse(match.Groups[5].Value),
        });
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
