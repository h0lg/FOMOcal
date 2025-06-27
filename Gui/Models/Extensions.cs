using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FomoCal;

internal static partial class StringExtensions
{
    internal static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);
    internal static bool IsSignificant(this string? str) => !string.IsNullOrWhiteSpace(str);
    internal static string Join(this IEnumerable<string?> strings, string separator) => string.Join(separator, strings);
    internal static string LineJoin(this IEnumerable<string?> strings) => strings.Join(Environment.NewLine);

    internal static bool IsValidHttpUrl(this string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    internal static string CsvEscape(this string? value)
        => value.IsNullOrWhiteSpace() ? "" : $"\"{value!.Replace("\"", "\"\"")}\"";

    [GeneratedRegex(@"\s+")] private static partial Regex ConsecutiveWhitespace();

    internal static string NormalizeWhitespace(this string? input)
    {
        if (input.IsNullOrWhiteSpace()) return string.Empty;
        string result = ConsecutiveWhitespace().Replace(input!, " "); // replace with single space
        return result.Trim(); // trim leading/trailing whitespace
    }

    internal static string ApplyReplacements(this string input, Dictionary<string, string> replacements)
    {
        foreach (var pair in replacements) // Apply each replacement pair
            input = Regex.Replace(input, pair.Key, pair.Value);

        return input;
    }

    // Regex pattern to match "Pattern => Replacement, Pattern2 =>" pairs
    [GeneratedRegex(@"([^=\s]+)\s*=>\s*([^,]*)")] private static partial Regex InlinedReplacements();

    /// <summary>Explodes the in-lined <paramref name="replacements"/> in the form "Pattern => Replacement, Pattern2 =>"
    /// into pairs for <see cref="ApplyReplacements(string, Dictionary{string, string})"/>.</summary>
    internal static Dictionary<string, string> ExplodeInlinedReplacements(this string replacements)
    {
        // Create a dictionary to hold the pairs (pattern => replacement)
        Dictionary<string, string> exploded = [];

        // Use Regex to find all pattern-replacement pairs
        foreach (Match match in InlinedReplacements().Matches(replacements))
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
        => JsComments().Replace(code,
            // Keep strings and template literals, remove comments
            m => m.Groups[1].Success || m.Groups[2].Success || m.Groups[3].Success ? m.Value : string.Empty);

    private const string jsComments = """
("(?:\\.|[^"\\])*") | # Double-quoted string
('(?:\\.|[^'\\])*') | # Single-quoted string
(`(?:\\.|[^`\\])*`) | # Template literal (backticks)
(/[*](?s:.*?)?[*]/) | # Block comment
(//.*)                # Line comment
""";

    [GeneratedRegex(jsComments, RegexOptions.IgnorePatternWhitespace)] private static partial Regex JsComments();
}

internal static class EnumerableExtensions
{
    internal static void UpdateWith<T>(this HashSet<T> set, IEnumerable<T> newItems)
    {
        set.RemoveWhere(newItems.Contains); // Remove old duplicates
        set.UnionWith(newItems);
    }

    /// <summary>Returns only the non-null elements from <paramref name="nullables"/>.</summary>
    internal static IEnumerable<T> WithValue<T>(this IEnumerable<T> nullables)
        => nullables.Where(v => v != null).Select(v => v);
}

internal static class EnumExtensions
{
    internal static string GetDescription<T>(this T value) where T : Enum
    {
        var field = typeof(T).GetField(value.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}

internal static class FileHelper
{
    internal static async Task WriteAsync(string filePath, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, contents);
    }

    internal static void ShareFile(string filePath, string contentType, string title)
        => MainThread.BeginInvokeOnMainThread(async () =>
            // see https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/share#share-a-file
            await Share.Default.RequestAsync(
                new ShareFileRequest { Title = title, File = new ShareFile(filePath, contentType) }));
}
