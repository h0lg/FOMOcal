using AngleSharp.Dom;

namespace FomoCal;

public class ScrapeJob
{
    public string? Closest { get; set; }
    public string Selector { get; set; } = string.Empty;
    public bool IgnoreNestedText { get; set; }
    public string? Attribute { get; set; }

    public string? Replace
    {
        get => replace;
        set
        {
            replace = value;
            replacements = null;
        }
    }

    private string? replace;
    private Dictionary<string, string>? replacements;

    private Dictionary<string, string> Replacements
    {
        get
        {
            if (replacements == null && Replace.IsSignificant())
                replacements = Replace!.ExplodeInlinedReplacements();

            return replacements!;
        }
    }

    public string? Match { get; set; }

    public virtual string? GetValue(AngleSharp.Dom.IElement element)
    {
        if (Selector.IsNullOrWhiteSpace()) return null;

        try
        {
            AngleSharp.Dom.IElement? node = Closest.IsSignificant() ? element.Closest(Closest!) : element;
            node = node?.QuerySelector(Selector);
            if (node == null) return null;

            var value = Attribute.IsSignificant() ? node.GetAttribute(Attribute!)
                : IgnoreNestedText ? node.ChildNodes.Where(n => n.NodeType == NodeType.Text).Select(n => n.TextContent.Trim()).LineJoin()
                : node.TextContent.Trim();

            if (value.IsNullOrWhiteSpace()) return null;
            value = value.NormalizeWhitespace();
            if (Replace.IsSignificant()) value = value.ApplyReplacements(Replacements);
            return Match.IsSignificant() ? ApplyRegex(value!, Match!) : value;
        }
        catch (Exception ex)
        {
            throw new Error($"Failed while extracting value from closest '{Closest}' selector '{Selector}' Attribute '{Attribute}' Match '{Match}'", ex);
        }
    }

    /// <summary>Returns an absolute URL for relative or root-relative paths
    /// scraped from <paramref name="element"/>, like from href or src attributes.</summary>
    internal string? GetUrl(AngleSharp.Dom.IElement element)
    {
        string? maybeRelativeUri = GetValue(element);

        return maybeRelativeUri == null ? null
            : new Uri(new Uri(element.BaseUri), maybeRelativeUri).ToString();
    }

    protected static string? ApplyRegex(string input, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
        return match.Success ? match.Value : null;
    }

    public override bool Equals(object? obj) => obj is ScrapeJob other && Equals(other);

    public bool Equals(ScrapeJob? other) =>
        other is not null &&
        Selector == other.Selector &&
        Attribute == other.Attribute &&
        Closest == other.Closest &&
        Match == other.Match &&
        IgnoreNestedText == other.IgnoreNestedText;

    public override int GetHashCode() => HashCode.Combine(Selector, Closest, IgnoreNestedText, Attribute, Match);

    public class Error : Exception
    {
        public Error() : base() { }
        public Error(string? message) : base(message) { }
        public Error(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
