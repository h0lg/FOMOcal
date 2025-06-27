using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.XPath;

namespace FomoCal;

public partial class ScrapeJob
{
    internal const string XPathSelectorPrefix = "XPATH^";
    internal static string FormatXpathSelector(string selector) => XPathSelectorPrefix + selector;
    [GeneratedRegex("(?<=\\[xpath>\")(.+)(?=\"\\])")] private static partial Regex OldXpathSelectorPattern();

    internal static string? MigrateSelector(string? selector)
        => selector.IsNullOrWhiteSpace() ? null
            : TryGetXPathSelector(selector!, out var xPath) ? FormatXpathSelector(xPath) : selector;

    internal static bool TryGetXPathSelector(string selector, [MaybeNullWhen(false)] out string xPathSelector)
    {
        if (selector.StartsWith(XPathSelectorPrefix, StringComparison.Ordinal))
        {
            xPathSelector = selector[XPathSelectorPrefix.Length..];
            return true;
        }

        var xpathMatch = OldXpathSelectorPattern().Match(selector);

        if (xpathMatch.Success)
        {
            xPathSelector = xpathMatch.Value;
            return true;
        }

        xPathSelector = null;
        return false;
    }

    protected static T? AddOrThrow<T>(List<Exception>? errors, Error error)
    {
        if (errors == null) throw error;
        errors.Add(error);
        return default;
    }

    public string? Closest { get; set; }
    public string? Selector { get; set; }
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
    public string? Comment { get; set; }

    public virtual string? GetValue(AngleSharp.Dom.IElement element, List<Exception>? errors = null)
    {
        try
        {
            INode? node = element;

            if (Closest.IsSignificant())
                node = TryGetXPathSelector(Closest!, out var xPathClosest)
                    /* select on ancestor axis, [1] yielding the nearest one
                     * (because XPath returns ancestor nodes in reverse order — closest first)
                     * see also https://github.com/AngleSharp/AngleSharp.XPath */
                    ? element.SelectSingleNode($"ancestor-or-self::{xPathClosest}[1]")
                    : element.Closest(Closest!);

            if (node == null) return null;

            List<INode> nodes = Selector.IsSignificant() && node is AngleSharp.Dom.IElement selectable
                ? TryGetXPathSelector(Selector!, out var xPathSelector)
                    ? selectable.SelectNodes(xPathSelector)
                    : [.. selectable.QuerySelectorAll(Selector!).Cast<INode>()]
                : [node];

            if (nodes.Count == 0) return null;

            var text = nodes.Select(node =>
                Attribute.IsSignificant() && node is AngleSharp.Dom.IElement attributed ? attributed.GetAttribute(Attribute!)
                    : IgnoreNestedText ? node.ChildNodes.Where(n => n.NodeType == NodeType.Text).Select(n => n.TextContent).LineJoin()
                    : node.TextContent)
                .LineJoin();

            if (text.IsNullOrWhiteSpace()) return null;
            text = text.NormalizeWhitespace(); // trims text as well
            if (Replace.IsSignificant() && Replacements.Count > 0) text = text.ApplyReplacements(Replacements);
            return Match.IsSignificant() ? ApplyRegex(text!, Match!) : text;
        }
        catch (Exception ex)
        {
            var jobJson = JsonSerializer.Serialize(this, JsonFileStore.JsonOptions);
            var error = new Error($"Failed while extracting value from {element} using {jobJson}", ex);
            return AddOrThrow<string?>(errors, error);
        }
    }

    /// <summary>Returns an absolute URL for relative or root-relative paths
    /// scraped from <paramref name="element"/>, like from href or src attributes.</summary>
    internal string? GetUrl(AngleSharp.Dom.IElement element, List<Exception>? errors = null)
    {
        string? maybeRelativeUri = GetValue(element, errors);

        return maybeRelativeUri == null ? null
            : new Uri(new Uri(element.BaseUri), maybeRelativeUri).ToString();
    }

    protected static string? ApplyRegex(string input, string pattern)
    {
        var match = Regex.Match(input, pattern);
        return match.Success ? match.Value : null;
    }

    public override bool Equals(object? obj) => obj is ScrapeJob other && Equals(other);

    public bool Equals(ScrapeJob? other) =>
        other is not null &&
        Closest == other.Closest &&
        Selector == other.Selector &&
        IgnoreNestedText == other.IgnoreNestedText &&
        Attribute == other.Attribute &&
        Replace == other.Replace &&
        Match == other.Match;

    public override int GetHashCode() => HashCode.Combine(Closest, Selector, IgnoreNestedText, Attribute, Replace, Match);

    public class Error : Exception
    {
        public Error() { }
        public Error(string? message) : base(message) { }
        public Error(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
