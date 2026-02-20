using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace FomoCal;

internal static partial class StringExtensions
{
    /// <summary>Indicates whether <paramref name="text"/> contains all of the supplied
    /// <paramref name="terms"/> using <paramref name="stringComparison"/> to compare.</summary>
    internal static bool ContainsAll(this string text, IEnumerable<string> terms,
        StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase)
        => terms.All(t => text.Contains(t, stringComparison));

    [GeneratedRegex(@"^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.IgnoreCase)]
    private static partial Regex DomainLike();

    internal static bool IsDomainLike(this string value, [MaybeNullWhen(false)] out string? validUrl)
    {
        validUrl = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        if (trimmed.Contains(' ')) return false; // reject spaces anywhere

        // return early if it already has a scheme
        if (trimmed.IsValidHttpUrl())
        {
            validUrl = trimmed;
            return true;
        }

        // no scheme, but matches domain-like pattern
        if (DomainLike().IsMatch(trimmed))
        {
            var url = "https://" + trimmed; // assume page supports https

            if (url.IsValidHttpUrl())
            {
                validUrl = url;
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\((?<url>https?:\/\/[^\s)]+)\)|(?<urlonly>https?:\/\/[^\s\[\]()]+)")]
    private static partial Regex LinkRegex();

    internal static IEnumerable<(string text, string? url)> ChunkByLinksAndUrls(this string text)
    {
        int lastIndex = 0;

        foreach (Match match in LinkRegex().Matches(text))
        {
            // Add normal text before this match
            if (match.Index > lastIndex) yield return (text[lastIndex..match.Index], null);

            string displayText, url;

            Group label = match.Groups["label"],
                urlMatch = match.Groups["url"];

            if (label.Success && urlMatch.Success) // Markdown-style link
            {
                displayText = label.Value;
                url = urlMatch.Value;
            }
            else
            {
                Group urlOnly = match.Groups["urlonly"];

                if (urlOnly.Success) // Plain URL
                    displayText = url = urlOnly.Value;
                else throw new ArgumentException(nameof(LinkRegex) + " matched something unexpected " + match);
            }

            yield return (displayText, url);
            lastIndex = match.Index + match.Length;
        }

        // Remaining normal text
        if (lastIndex < text.Length) yield return (text[lastIndex..], null);
    }

    internal static bool IsValidHttpUrl(this string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    internal static bool IsSignificantValidUrl(this string value) => value.IsSignificant() && value.IsValidHttpUrl();

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
