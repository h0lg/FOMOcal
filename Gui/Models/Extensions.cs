using System.Text.RegularExpressions;

namespace FomoCal;

internal static class StringExtensions
{
    internal static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);
    internal static bool IsSignificant(this string? str) => !string.IsNullOrWhiteSpace(str);
    internal static string Join(this IEnumerable<string?> strings, string separator) => string.Join(separator, strings);
    internal static string LineJoin(this IEnumerable<string?> strings) => strings.Join(Environment.NewLine);

    internal static string CsvEscape(this string? value)
        => value.IsNullOrWhiteSpace() ? "" : $"\"{value!.Replace("\"", "\"\"")}\"";

    private static readonly Regex consecutiveWhitespace = new(@"\s+");

    internal static string NormalizeWhitespace(this string? input)
    {
        if (input.IsNullOrWhiteSpace()) return string.Empty;
        string result = consecutiveWhitespace.Replace(input!, " "); // replace with single space
        return result.Trim(); // trim leading/trailing whitespace
    }

    /// <summary>Indicates whether <paramref name="text"/> contains any of the supplied
    /// <paramref name="terms"/> using <paramref name="stringComparison"/> to compare.</summary>
    internal static bool ContainsAny(this string text, IEnumerable<string> terms,
        StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase)
        => terms.Any(t => text.Contains(t, stringComparison));
}

internal static class EnumerableExtensions
{
    internal static void UpdateWith<T>(this HashSet<T> set, IEnumerable<T> newItems)
    {
        set.RemoveWhere(newItems.Contains); // Remove old duplicates
        set.UnionWith(newItems);
    }
}
