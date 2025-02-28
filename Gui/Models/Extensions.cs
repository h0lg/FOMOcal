using System.Text.RegularExpressions;

namespace FomoCal;

internal static class StringExtensions
{
    internal static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);
    internal static bool HasSignificantValue(this string? str) => !string.IsNullOrWhiteSpace(str);
    internal static string LineJoin(this IEnumerable<string?> strings) => string.Join(Environment.NewLine, strings);

    internal static string NormalizeWhitespace(this string? input)
    {
        if (input.IsNullOrWhiteSpace()) return string.Empty;

        string result = Regex.Replace(input!, @"\s+", " ");  // Convert all line breaks to '\n'

        // Trim leading/trailing whitespace
        return result.Trim();
    }
}
