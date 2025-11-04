using System.Net.Mime;
using System.Reflection;
using AngleSharp;
using AngleSharp.Html.Dom;

namespace FomoCal;

internal static partial class Export
{
    private static readonly RememberedStrings htmlEventFields = new("Export.HtmlEventFields");

    internal static IEnumerable<PropertyInfo> EventFieldsForHtml
    {
        get => LoadEventProperties(htmlEventFields, () => [.. Event.Fields.Select(p => p.Name)]);
        set => SaveEventProperties(value, htmlEventFields);
    }

    internal static async Task ExportToHtml(this IEnumerable<Event> events)
    {
        PropertyInfo[] eventFields = [.. EventFieldsForHtml];
        using var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
        using var doc = await context.OpenNewAsync();

        // styling
        var link = (IHtmlLinkElement)doc.CreateElement("link");
        link.Href = "https://unpkg.com/simple-datatables@10.0.0/dist/style.css";
        link.Type = "text/css";
        link.Relation = "stylesheet";
        doc.Head!.AppendChild(link);
        var style = doc.CreateElement("style");

        style.TextContent =
@"table { font-family: sans-serif; }
img { max-height: 100px; }";

        doc.Head!.AppendChild(style);
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

            foreach (var field in eventFields)
            {
                var td = doc.CreateElement("td");
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

        var tablesInclude = (IHtmlScriptElement)doc.CreateElement("script");
        tablesInclude.Source = "https://unpkg.com/simple-datatables@10.0.0/dist/umd/simple-datatables.js";
        doc.Body!.AppendChild(tablesInclude);
        var initScript = (IHtmlScriptElement)doc.CreateElement("script");
        initScript.Text = $"new simpleDatatables.DataTable('#{table.Id}')";
        doc.Body!.AppendChild(initScript);

        await ExportFile(fileTypeLabel: "HTML", contents: doc.ToHtml(), extension: "html", contentType: MediaTypeNames.Text.Html);
    }
}
