using System.Text.RegularExpressions;
using DeliveryNoteLabeler.Core.Exceptions;
using DeliveryNoteLabeler.Core.Models;

namespace DeliveryNoteLabeler.Core.Extraction;

public static partial class PdfTextParser
{
    public const string StatusScanningNormal = "Scanning with normal mode…";
    public const string StatusScanningAi = "Normal scan could not read this PDF — trying AI…";

    private const string DrawingNoPattern =
        @"(?:\d{5,9}\s+REV\s+[A-Z0-9]+|\d{2,}(?:-[A-Z0-9]+){2,})";

    private static readonly Regex LineItemRevColumnRegex = new(
        @"^(\d+)\.\s+(PSM-\d+)\s+(.+)\s+(\d{5,9})\s+([A-Z0-9]+)\s+(\d+)\s*(?:ea|each)?(?:\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LineItemTailRegex = new(
        @"^(\d+)\.\s+(PSM-\d+)\s+(.+)\s+(\d+)\s*(?:ea|each)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DrawingNoAtEndRegex = new(
        DrawingNoPattern + @"$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WrappedQuantityBeforeDescriptionRegex = new(
        @"^(\d+\.\s+PSM-\d+\s+)(.+?)(\s+" + DrawingNoPattern + @")(\s+\d+\s*(?:ea|each)?)(?:\s+(.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [GeneratedRegex(@"Delivery Note (?:No|Number)[.:]?\s*([^\n\r)]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DeliveryNoteNoPattern();

    [GeneratedRegex(@"Customer Order No[.:]?\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerOrderPattern();

    [GeneratedRegex(@"(\S+)\s+Customer Order No", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerOrderReversedPattern();

    [GeneratedRegex(@"Customer Order No[.:]?\s*[\r\n]+\s*(\S+)", RegexOptions.IgnoreCase)]
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

        var shiftedFont = PharmaFontEncoding.LooksLikeShiftedEncoding(text);
        text = PharmaFontEncoding.DecodeIfNeeded(text);

        var normalized = text.Replace('\u00A0', ' ').Replace("\r\n", "\n").Replace('\r', '\n');
        if (shiftedFont)
        {
            normalized = normalized.Replace(')', '\n');
            normalized = normalized.Replace('?', ' ');
        }

        normalized = LabelValueLineBreakPattern().Replace(normalized, "${label} ${value}");
        normalized = Regex.Replace(normalized, "[ \t]+", " ");
        normalized = Regex.Replace(normalized, "\n+", "\n");
        return normalized.Trim();
    }

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
                    mergedLines.Add(NormalizeWrappedDescriptionOrder(current.Trim()));
                }

                current = line;
                continue;
            }

            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            if (IsLineItemContinuation(line))
            {
                current += " " + line;
                continue;
            }

            mergedLines.Add(NormalizeWrappedDescriptionOrder(current.Trim()));
            current = string.Empty;
        }

        if (!string.IsNullOrEmpty(current))
        {
            mergedLines.Add(NormalizeWrappedDescriptionOrder(current.Trim()));
        }

        return string.Join('\n', mergedLines);
    }

    internal static string NormalizeWrappedDescriptionOrder(string line)
    {
        var match = WrappedQuantityBeforeDescriptionRegex.Match(line.Trim());
        if (!match.Success || !match.Groups[5].Success)
        {
            return line.Trim();
        }

        var trailingDescription = match.Groups[5].Value.Trim();
        if (string.IsNullOrEmpty(trailingDescription))
        {
            return line.Trim();
        }

        return $"{match.Groups[1].Value}{match.Groups[2].Value.Trim()} {trailingDescription}{match.Groups[3].Value}{match.Groups[4].Value}".Trim();
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
        foreach (var rawLine in text.Split('\n'))
        {
            if (TryParseLineItemLine(rawLine, out var item))
            {
                items.Add(item);
            }
        }

        return items;
    }

    internal static bool TryParseLineItemLine(string rawLine, out LineItem item)
    {
        item = null!;
        var line = rawLine.Trim();
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        var match = LineItemRevColumnRegex.Match(line);
        if (match.Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out var revLineNo)
                || !int.TryParse(match.Groups[6].Value, out var revQuantity))
            {
                return false;
            }

            var descriptionText = match.Groups[3].Value.Trim();
            if (match.Groups[7].Success)
            {
                var trailingDescription = match.Groups[7].Value.Trim();
                if (!string.IsNullOrEmpty(trailingDescription))
                {
                    descriptionText = $"{descriptionText} {trailingDescription}".Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(descriptionText))
            {
                return false;
            }

            item = new LineItem
            {
                LineNo = revLineNo,
                PartNo = match.Groups[2].Value.ToUpperInvariant(),
                Description = descriptionText,
                DrawingNo = $"{match.Groups[4].Value.Trim().ToUpperInvariant()} REV {match.Groups[5].Value.Trim().ToUpperInvariant()}",
                Quantity = revQuantity,
            };

            return true;
        }

        match = LineItemTailRegex.Match(line);
        if (match.Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out var lineNo)
                || !int.TryParse(match.Groups[4].Value, out var quantity))
            {
                return false;
            }

            var partNo = match.Groups[2].Value.ToUpperInvariant();
            var body = match.Groups[3].Value.Trim();
            var drawingMatch = DrawingNoAtEndRegex.Match(body);
            if (!drawingMatch.Success)
            {
                return false;
            }

            var drawingNo = drawingMatch.Value.Trim().ToUpperInvariant();
            var description = body[..drawingMatch.Index].Trim().TrimEnd('-').Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            item = new LineItem
            {
                LineNo = lineNo,
                PartNo = partNo,
                Description = description,
                DrawingNo = drawingNo,
                Quantity = quantity,
            };

            return true;
        }

        return false;
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
            CustomerOrderNo = customerOrderMatch.Groups[1].Value.Trim().TrimStart(':', '?'),
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

    private static bool IsCompleteLineItem(string line) => TryParseLineItemLine(line, out _);

    public sealed class HeaderFields
    {
        public required string DeliveryNoteNo { get; init; }
        public required string CustomerOrderNo { get; init; }
        public string? SalesOrderNo { get; init; }
        public string? CustomerReference { get; init; }
        public string? Date { get; init; }
    }
}
