using System.Text.RegularExpressions;

namespace FomoCal;

internal static class StringExtensions
{
    internal static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);
    internal static bool IsSignificant(this string? str) => !string.IsNullOrWhiteSpace(str);
    internal static string LineJoin(this IEnumerable<string?> strings) => string.Join(Environment.NewLine, strings);

    internal static string NormalizeWhitespace(this string? input)
    {
        if (input.IsNullOrWhiteSpace()) return string.Empty;

        // Normalize all line endings to '\n' and handle horizontal whitespace
        string result = Regex.Replace(input!, @"\r\n?|\n", "\n");  // Convert all line breaks to '\n'

        // Replace consecutive horizontal whitespace (spaces or tabs) with a single space
        result = Regex.Replace(result, @"[ \t]+", " ");

        // Replace all horizontal and vertical whitespace (spaces/tabs and newlines) with a single newline
        result = Regex.Replace(result, @"([ \t]+)(\n)", "\n");

        // Replace any consecutive newlines with a single newline
        result = Regex.Replace(result, @"\n{2,}", "\n");

        // Trim leading/trailing whitespace
        return result.Trim();
    }
}
