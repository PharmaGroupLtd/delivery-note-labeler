namespace DeliveryNoteLabeler.Core.Printing;

public static class ZplEscaper
{
    /// <summary>
    /// Escapes text for use in a ZPL ^FH\ field (hex escape mode).
    /// </summary>
    public static string EscapeFieldData(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append("_5C");
                    break;
                case '^':
                    builder.Append("_5E");
                    break;
                case '~':
                    builder.Append("_7E");
                    break;
                case '_':
                    builder.Append("_5F");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }
}
