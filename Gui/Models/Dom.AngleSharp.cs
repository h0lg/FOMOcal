using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;
using AngleSharp.XPath;

namespace FomoCal;

internal partial class Browser() : IBrowser
{
    private readonly IBrowsingContext browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader());

    public async Task<IDomDocument> OpenAsync(Action<IResponseBuilder> request, CancellationToken cancel = default)
    {
        var doc = await browsingContext.OpenAsync(response => request(new ResponseBuilder(response)), cancel);
        return new DomDocument(doc);
    }

    public async Task<IDomDocument> OpenAsync(string url, CancellationToken cancel = default)
    {
        var doc = await browsingContext.OpenAsync(new Url(url), cancel);
        return new DomDocument(doc);
    }

    public void Dispose() => browsingContext.Dispose();
}

public class ResponseBuilder(VirtualResponse response) : IResponseBuilder
{
    public IResponseBuilder Address(string url)
    {
        response.Address(url);
        return this;
    }

    public IResponseBuilder Content(Stream stream)
    {
        response.Content(stream);
        return this;
    }

    public IResponseBuilder Content(string text)
    {
        response.Content(text);
        return this;
    }

    public IResponseBuilder Header(string name, string value)
    {
        response.Header(name, value);
        return this;
    }
}

public partial class DomDocument(IDocument doc) : IDomDocument
{
    public string Url => doc.Url;
    public string? Title => doc.Title;

    public IDomElement? QuerySelector(string css)
    {
        var element = doc.QuerySelector(css);
        return element == null ? null : new DomElement(element);
    }

    public IEnumerable<IDomElement> QuerySelectorAll(string css)
        => doc.QuerySelectorAll(css).Select(x => new DomElement(x));

    public IEnumerable<IDomNode> SelectNodes(string xPath)
        // see https://github.com/AngleSharp/AngleSharp.XPath
        => doc.Body.SelectNodes(xPath).Select(node => node.Wrap());

    public void Dispose() => doc.Dispose();
}

public class DomNode(INode node) : IDomNode
{
    public string TextContent => node.TextContent;
    public string BaseUri => node.BaseUri;

    public IEnumerable<IDomNode> GetTextNodes()
        => node.ChildNodes.Where(n => n.NodeType == NodeType.Text).Select(n => new DomNode(n));

    public string? HyperReference(string url) => node.HyperReference(url)?.ToString();
}

public class DomElement(AngleSharp.Dom.IElement element) : DomNode(element), IDomElement
{
    public IDomElement? Closest(string css)
    {
        var closest = element.Closest(css);
        return closest == null ? null : new DomElement(closest);
    }

    public string? GetAttribute(string name) => element.GetAttribute(name);

    public IEnumerable<IDomElement> QuerySelectorAll(string css)
        => element.QuerySelectorAll(css).Select(elmt => new DomElement(elmt));

    public List<IDomNode> SelectNodes(string xPath)
        => [.. element.SelectNodes(xPath).Select(n => n.Wrap())];

    public IDomNode SelectSingleNode(string xPath) => element.SelectSingleNode(xPath).Wrap();
}

internal static class WrapExtensions
{
    internal static IDomNode Wrap(this INode node)
        => node is AngleSharp.Dom.IElement element ? new DomElement(element) : new DomNode(node);
}
