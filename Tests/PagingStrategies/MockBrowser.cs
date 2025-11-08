using System.Reflection;
using AngleSharp;
using AngleSharp.Html.Dom;
using FomoCal;

namespace Tests.PagingStrategies;

enum EventPageError { NoName, NoDate, Past }

class InputEvent
{
    public string? Category { get; set; }
    public string? Date { get; set; }
    public string? Name { get; set; }
    public string? Venue { get; set; }
}

class EventPage
{
    public List<InputEvent> Events { get; set; } = [];
    public Func<AngleSharp.Dom.IDocument, IHtmlElement>? AddNextPageNavigator { get; set; }
}

public partial class MockBrowser : FomoCal.IBrowser
{
    private readonly IBrowsingContext browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    private readonly List<EventPage> eventPages = [];

    public Task<IDomDocument> OpenAsync(Action<IResponseBuilder> request, CancellationToken cancel = default)
    {
        throw new NotImplementedException();
    }

    public async Task<IDomDocument> OpenAsync(string url, CancellationToken cancel = default)
    {
        var doc = await CreateDocumentAsync();
        //var html = doc.ToHtml();
        return new DomDocument(doc);
    }

    internal void AddEvents(Venue venue, int count, int? start = null, EventPageError? error = null, string category = "concert", uint page = 0)
    {
        EventPage eventPage = GetOrCreatePage(page);

        eventPage.Events.AddRange(Enumerable.Range(start ?? (eventPage.Events.Count + 1), count).Select(n => new InputEvent
        {
            Category = category,
            Name = error == EventPageError.NoName ? string.Empty : "event " + n,
            Date = (error == EventPageError.NoDate ? null as DateTime?
                : error == EventPageError.Past ? DateTime.Today.AddDays(-n)
                : DateTime.Today.AddDays(n))?.ToString("yyyy-MM-dd"),
            Venue = venue.Name
        }));
    }

    private EventPage GetOrCreatePage(uint page)
    {
        EventPage eventPage;

        if (eventPages.TryGetAt(page, out var p)) eventPage = p;
        else
        {
            eventPage = new();
            eventPages.Insert((int)page, eventPage);
        }

        return eventPage;
    }

    internal void AddNextPageLink(string href, uint page = 0)
    {
        EventPage eventPage = GetOrCreatePage(page);

        eventPage.AddNextPageNavigator = doc =>
        {
            var a = (IHtmlAnchorElement)doc.CreateElement("a");
            a.Href = href;
            a.TextContent = "next page";
            a.ClassName = "next-page";
            return a;
        };
    }

    private async Task<AngleSharp.Dom.IDocument> CreateDocumentAsync()
    {
        PropertyInfo[] eventFields = typeof(InputEvent).GetProperties();
        var doc = await browsingContext.OpenNewAsync();

        var table = doc.CreateElement("table");
        table.Id = "events";
        var thead = doc.CreateElement("thead");
        var headerRow = doc.CreateElement("tr");

        // table header
        foreach (var field in eventFields)
        {
            var th = doc.CreateElement("th");
            th.TextContent = field.Name;
            headerRow.AppendChild(th);
        }

        thead.AppendChild(headerRow);
        table.AppendChild(thead);
        var tbody = doc.CreateElement("tbody");

        EventPage eventPage;

        if (eventPages.Count > 0)
        {
            eventPage = eventPages[0];
            eventPages.Remove(eventPage);
        }
        else throw new InvalidOperationException("Out of event pages");

        // table body
        foreach (var evt in eventPage.Events)
        {
            var row = doc.CreateElement("tr");
            row.ClassName = "event";

            foreach (var field in eventFields)
            {
                var td = doc.CreateElement("td");
                td.ClassName = field.Name;
                var value = field.GetValue(evt, null);

                if (value != null)
                {
                    switch (field.Name)
                    {
                        case nameof(Event.Url):
                        case nameof(Event.TicketUrl):
                        case nameof(Event.ScrapedFrom):
                            var a = (IHtmlAnchorElement)doc.CreateElement("a");
                            a.TextContent = a.Href = value.ToString()!;
                            a.Target = "_blank";
                            td.AppendChild(a);
                            break;

                        case nameof(Event.ImageUrl):
                            var img = (IHtmlImageElement)doc.CreateElement("img");
                            img.Source = value.ToString();
                            img.AlternativeText = "Event Image";
                            td.AppendChild(img);
                            break;

                        default:
                            td.TextContent = value switch
                            {
                                DateTime dt => dt.ToString("yyyy-MM-dd"),
                                _ => value.ToString()!
                            };

                            break;
                    }
                }

                row.AppendChild(td);
            }

            tbody.AppendChild(row);
        }

        table.AppendChild(tbody);
        doc.Body!.AppendChild(table);

        if (eventPage.AddNextPageNavigator != null)
            doc.Body.AppendChild(eventPage.AddNextPageNavigator(doc));

        //await doc.WaitForReadyAsync();
        return doc;
    }

    public void Dispose() => browsingContext.Dispose();
}
