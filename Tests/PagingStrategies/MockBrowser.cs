using System.Reflection;
using AngleSharp;
using AngleSharp.Html.Dom;
using FomoCal;

namespace Tests.PagingStrategies;

enum EventPageError { NoName, NoDate, Past }

class InputEvent
{
    public string? Date { get; set; }
    public string? Name { get; set; }
    public string? Venue { get; set; }
}

public partial class MockBrowser : FomoCal.IBrowser
{
    private readonly IBrowsingContext browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    private readonly List<InputEvent> events = [];

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


    internal void AddEvents(Venue venue, int count, int? start = null, EventPageError? error = null)
    {
        events.AddRange(Enumerable.Range(start ?? (events.Count + 1), count).Select(n => new InputEvent
        {
            Name = error == EventPageError.NoName ? string.Empty : "event " + n,
            Date = (error == EventPageError.NoDate ? null as DateTime?
                : error == EventPageError.Past ? DateTime.Today.AddDays(-n)
                : DateTime.Today.AddDays(n))?.ToString("yyyy-MM-dd"),
            Venue = venue.Name
        }));
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

        // table body
        foreach (var evt in events)
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
        //await doc.WaitForReadyAsync();
        return doc;
    }

    public void Dispose() => browsingContext.Dispose();
}
