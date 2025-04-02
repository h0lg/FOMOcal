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

    internal static string ApplyReplacements(this string input, Dictionary<string, string> replacements)
    {
        foreach (var pair in replacements) // Apply each replacement pair
            input = Regex.Replace(input, Regex.Escape(pair.Key), pair.Value);

        return input;
    }

    // Regex pattern to match "Pattern => Replacement, Pattern2 =>" pairs
    private static readonly Regex inlinedReplacements = new(@"([^=\s]+)\s*=>\s*([^,]*)");

    /// <summary>Explodes the inlined <paramref name="replacements"/> in the form "Pattern => Replacement, Pattern2 =>"
    /// into pairs for <see cref="ApplyReplacements(string, Dictionary{string, string})"/>.</summary>
    internal static Dictionary<string, string> ExplodeInlinedReplacements(this string replacements)
    {
        // Create a dictionary to hold the pairs (pattern => replacement)
        Dictionary<string, string> exploded = [];

        // Use Regex to find all pattern-replacement pairs
        foreach (Match match in inlinedReplacements.Matches(replacements))
        {
            string patternKey = match.Groups[1].Value;
            exploded[patternKey] = match.Groups[2].Value;
        }

        return exploded;
    }

    /// <summary>Indicates whether <paramref name="text"/> contains any of the supplied
    /// <paramref name="terms"/> using <paramref name="stringComparison"/> to compare.</summary>
    internal static bool ContainsAny(this string text, IEnumerable<string> terms,
        StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase)
        => terms.Any(t => text.Contains(t, stringComparison));

    internal static string RemoveJsComments(this string code)
    {
        var pattern = @"
            (""(?:\\.|[^""\\])*"") |   # Double-quoted string
            ('(?:\\.|[^'\\])*')    |   # Single-quoted string
            (`(?:\\.|[^`\\])*`)    |   # Template literal (backticks)
            (/[*](?s:.*?)?[*]/)    |   # Block comment
            (//.*)                     # Line comment
        ";

        return Regex.Replace(code, pattern, m =>
        {
            // Keep strings and template literals, remove comments
            return m.Groups[1].Success || m.Groups[2].Success || m.Groups[3].Success ? m.Value : string.Empty;
        }, RegexOptions.IgnorePatternWhitespace);
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
