namespace WasteBatteriesObligationsBe.Utils.Text;

// Text is trimmed once, before validation. A required string is never null.
// An optional string uses null for every blank.
public static class StringValues
{
    public static string TrimToEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    public static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
