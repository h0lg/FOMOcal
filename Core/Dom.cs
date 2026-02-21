namespace FomoCal;

public interface IBrowser : IDisposable
{
    Task<IDomDocument> OpenAsync(Action<IResponseBuilder> request, CancellationToken cancel = default);
    Task<IDomDocument> OpenAsync(string url, CancellationToken cancel = default);
}

public interface IResponseBuilder
{
    IResponseBuilder Address(string url);
    IResponseBuilder Content(Stream stream);
    IResponseBuilder Content(string text);
    IResponseBuilder Header(string name, string value);
}

public interface IDomDocument : IDisposable
{
    string Url { get; }
    string? Title { get; }

    IDomElement? QuerySelector(string css);
    IEnumerable<IDomElement> QuerySelectorAll(string css);
    IEnumerable<IDomNode> SelectNodes(string xPath);
}

public interface IDomNode
{
    string TextContent { get; }
    string BaseUri { get; }
    IEnumerable<IDomNode> GetTextNodes();
    string? HyperReference(string url);
}

public interface IDomElement : IDomNode
{
    IDomElement? Closest(string css);
    IEnumerable<IDomElement> QuerySelectorAll(string css);
    IDomNode SelectSingleNode(string xPath);
    List<IDomNode> SelectNodes(string xPath);
    string? GetAttribute(string name);
}
