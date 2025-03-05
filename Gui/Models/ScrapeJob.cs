using PuppeteerSharp;

namespace FomoCal;

public class ScrapeJob
{
    public string Selector { get; set; } = string.Empty;
    public string? Attribute { get; set; }
    public string? Match { get; set; }
    public bool IgnoreNestedText { get; set; }

    public virtual async Task<string?> GetValueAsync(IElementHandle element)
    {
        if (Selector.IsNullOrWhiteSpace()) return null;

        try
        {
            var node = await element.QuerySelectorAsync(Selector);
            if (node == null) return null;

            var getValue = Attribute.HasSignificantValue() ? node.EvaluateFunctionAsync<string>($"el => el.getAttribute('{Attribute!}')")
                : IgnoreNestedText ? node.EvaluateFunctionAsync<string>(@"el => 
    Array.from(el.childNodes)
        .filter(node => node.nodeType === Node.TEXT_NODE)
        .map(node => node.textContent.trim())
        .filter(text => text.length > 0)
        .join(' ')
")
                : node.GetTextContentAsync();

            var value = await getValue;

            if (value.IsNullOrWhiteSpace()) return null;
            value = value.NormalizeWhitespace();
            return Match != null ? ApplyRegex(value!, Match) : value;
        }
        catch (Exception ex)
        {
            throw new Error($"Failed while extracting value from selector '{Selector}' Attribute '{Attribute}' Match '{Match}'", ex);
        }
    }

    /// <summary>Returns an absolute URL for relative or root-relative paths
    /// scraped from <paramref name="element"/>, like from href or src attributes.</summary>
    internal async Task<string?> GetUrlAsync(IElementHandle element)
    {
        string? maybeRelativeUri = await GetValueAsync(element);

        if (maybeRelativeUri == null) return null;
        else
        {
            var baseUri = await element.EvaluateFunctionAsync<string>("el => el.baseURI");
            return new Uri(new Uri(baseUri), maybeRelativeUri).ToString();
        }
    }

    protected static string ApplyRegex(string input, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
        return match.Success ? match.Value : input;
    }

    public override bool Equals(object? obj) => obj is ScrapeJob other && Equals(other);

    public bool Equals(ScrapeJob? other) =>
        other is not null &&
        Selector == other.Selector &&
        Attribute == other.Attribute &&
        Match == other.Match &&
        IgnoreNestedText == other.IgnoreNestedText;

    public override int GetHashCode() => HashCode.Combine(Selector, IgnoreNestedText, Attribute, Match);

    public class Error : Exception
    {
        public Error() : base() { }
        public Error(string? message) : base(message) { }
        public Error(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
