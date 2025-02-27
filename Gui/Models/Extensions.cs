using System.Text.RegularExpressions;

namespace FomoCal;

internal static class StringExtensions
{
    internal static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);
    internal static bool IsSignificant(this string? str) => !string.IsNullOrWhiteSpace(str);
    internal static string LineJoin(this IEnumerable<string?> strings) => string.Join(Environment.NewLine, strings);

    private static readonly Regex consecutiveWhitespace = new(@"\s+");

    internal static string NormalizeWhitespace(this string? input)
    {
        if (input.IsNullOrWhiteSpace()) return string.Empty;
        string result = consecutiveWhitespace.Replace(input!, " "); // replace with single space
        return result.Trim(); // trim leading/trailing whitespace
    }
}

internal static class EnumerableExtensions
{
    internal static void UpdateWith<T>(this HashSet<T> set, IEnumerable<T> newItems)
    {
        set.RemoveWhere(newItems.Contains); // Remove old duplicates
        set.UnionWith(newItems);
    }
}
