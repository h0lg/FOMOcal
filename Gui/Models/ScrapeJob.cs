using AngleSharp.Dom;

namespace FomoCal;

public class ScrapeJob
{
    public string Selector { get; set; } = string.Empty;
    public bool IgnoreNestedText { get; set; }
    public string? Attribute { get; set; }
    public string? Match { get; set; }

    public virtual string? GetValue(AngleSharp.Dom.IElement element)
    {
        if (Selector.IsNullOrWhiteSpace()) return null;

        try
        {
            var node = element.QuerySelector(Selector);
            if (node == null) return null;

            var value = Attribute.IsSignificant() ? node.GetAttribute(Attribute!)
                : IgnoreNestedText ? node.ChildNodes.Where(n => n.NodeType == NodeType.Text).Select(n => n.TextContent.Trim()).LineJoin()
                : node.TextContent.Trim();

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
    internal string? GetUrl(AngleSharp.Dom.IElement element)
    {
        string? maybeRelativeUri = GetValue(element);

        return maybeRelativeUri == null ? null
            : new Uri(new Uri(element.BaseUri), maybeRelativeUri).ToString();
    }

    protected static string ApplyRegex(string input, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
        return match.Success ? match.Value : input;
    }

    public class Error : Exception
    {
        public Error() : base() { }
        public Error(string? message) : base(message) { }
        public Error(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
