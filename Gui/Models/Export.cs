using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Html.Dom;
using FomoCal.Gui;

namespace FomoCal;

internal static partial class Export
{
    internal static readonly PropertyInfo[] EventFields = [.. typeof(Event).GetProperties().Where(p => p.Name != nameof(Event.IsPast))];

    private const string htmlEventFieldsPreferencesKey = "Export.HtmlEventFields",
        textEventFieldsPreferencesKey = "Export.TextEventFields",
        textAlignedWithHeadersPreferencesKey = "Export.TextAlignedWithHeaders";

    internal static IEnumerable<PropertyInfo> EventFieldsForHtml
    {
        get => LoadEventProperties(htmlEventFieldsPreferencesKey, () => [.. EventFields.Select(p => p.Name)]);
        set => SaveEventProperties(value, htmlEventFieldsPreferencesKey);
    }

    internal static IEnumerable<PropertyInfo> EventFieldsForText
    {
        get => LoadEventProperties(textEventFieldsPreferencesKey, () => [nameof(Event.Date), nameof(Event.Name), nameof(Event.Venue)]);
        set => SaveEventProperties(value, textEventFieldsPreferencesKey);
    }

    internal static bool TextAlignedWithHeaders
    {
        get => Preferences.Get(textAlignedWithHeadersPreferencesKey, true);
        set => Preferences.Set(textAlignedWithHeadersPreferencesKey, value);
    }

    private static IEnumerable<PropertyInfo> LoadEventProperties(string preferencesKey, Func<string[]> getDefaults)
    {
        var saved = Preferences.Get(preferencesKey, null)?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (saved.Length == 0) saved = getDefaults();
        return saved.Select(name => EventFields.First(p => p.Name == name));
    }

    private static void SaveEventProperties(IEnumerable<PropertyInfo> value, string preferencesKey)
        => Preferences.Set(preferencesKey, value.Select(p => p.Name).Join(","));

    [GeneratedRegex(@"(\d{1,2})(?::(\d{2}))?", RegexOptions.Compiled)] private static partial Regex TimeRegex();

    private static bool TryParseStartTime(string? startTimeStr, out TimeSpan time)
    {
        time = default;
        if (startTimeStr.IsNullOrWhiteSpace()) return false;

        var match = TimeRegex().Match(startTimeStr!);
        if (!match.Success) return false;

        if (int.TryParse(match.Groups[1].Value, out int hours))
        {
            string minutesGroup = match.Groups[2].Value;
            var minutes = minutesGroup.IsSignificant() && int.TryParse(minutesGroup, out int mins) ? mins : 0;
            time = new TimeSpan(hours, minutes, 0); // Assume full hour if no minutes are given
            return true;
        }

        return false;
    }

    // see https://en.wikipedia.org/wiki/ICalendar
    internal static async Task ExportToIcal(this IEnumerable<Event> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//FomoCal//NONSGML v1.0//EN");

        foreach (var evt in events)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"SUMMARY:{evt.Name}");

            // construct start time with Date and optional StartTime or DoorsTime, defaulting to midnight
            var startTime = TryParseStartTime(evt.StartTime, out TimeSpan start) ? start
                : TryParseStartTime(evt.DoorsTime, out TimeSpan doors) ? doors : default;

            var localTime = evt.Date.Date + startTime;
            DateTime.SpecifyKind(localTime, DateTimeKind.Local); // interpret time as local, but export UTC
            sb.AppendLine($"DTSTART:{localTime.ToUniversalTime():yyyyMMddTHHmmssZ}");

            var location = evt.Stage.IsSignificant() ? $"{evt.Stage}, {evt.Venue}" : evt.Venue;
            sb.AppendLine($"LOCATION:{location}");

            if (evt.Genres.IsSignificant()) sb.AppendLine($"CATEGORIES:{evt.Genres}");
            if (evt.Url.IsSignificant()) sb.AppendLine($"URL:{evt.Url}");
            if (evt.ImageUrl.IsSignificant()) sb.AppendLine($"IMAGE;VALUE=URI:{evt.ImageUrl}");

            // Description: Append details not covered by standard fields
            var descriptionParts = new List<string>();
            if (evt.SubTitle.IsSignificant()) descriptionParts.Add(evt.SubTitle!);
            if (evt.Description.IsSignificant()) descriptionParts.Add(evt.Description!);
            if (evt.DoorsTime.IsSignificant()) descriptionParts.Add($"Doors: {evt.DoorsTime}");
            if (evt.StartTime.IsSignificant()) descriptionParts.Add($"Starts: {evt.StartTime}");
            if (evt.TicketUrl.IsSignificant()) descriptionParts.Add($"Tickets: {evt.TicketUrl}");
            if (evt.PresalePrice.IsSignificant()) descriptionParts.Add($"Pre-sale: {evt.PresalePrice}");
            if (evt.DoorsPrice.IsSignificant()) descriptionParts.Add($"At the door: {evt.DoorsPrice}");

            if (descriptionParts.Count > 0)
                sb.AppendLine($"DESCRIPTION:{descriptionParts.Join("\\n")}");

            sb.AppendLine($"DTSTAMP:{evt.Scraped:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"SOURCE:{MauiProgram.RepoUrl}");
            sb.AppendLine($"X-FOMOCAL-GENERATED-BY:{AppInfo.Name} v{MauiProgram.GetAppVersion()}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        await ExportFile("iCalendar", contents: sb.ToString(), extension: "ics", contentType: "text/calendar");
    }

    internal static async Task ExportToCsv(this IEnumerable<Event> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine(EventFields.Select(p => p.Name).Join(","));

        foreach (var evt in events)
            sb.AppendLine(EventFields.Select(c =>
            {
                var value = c.GetValue(evt, null);
                if (value is string str) return str.CsvEscape();
                if (value is DateTime date) return date.ToString("yyyy-MM-dd");
                return value?.ToString() ?? "";
            }).Join(","));

        await ExportFile("CSV", contents: sb.ToString(), extension: "csv", contentType: MediaTypeNames.Text.Csv);
    }

    internal static async Task ExportToHtml(this IEnumerable<Event> events)
    {
        PropertyInfo[] eventFields = [.. EventFieldsForHtml];
        var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
        var doc = await context.OpenNewAsync();

        // styling
        var style = doc.CreateElement("style");

        style.TextContent =
@"table { border-collapse: collapse; font-family: sans-serif; }
th, td { border: 1px solid #ccc; padding: 4px 8px; }
img { max-height: 100px; }";

        doc.Head!.AppendChild(style);
        var table = doc.CreateElement("table");
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
        await ExportFile(fileTypeLabel: "HTML", contents: doc.ToHtml(), extension: "html", contentType: MediaTypeNames.Text.Html);
    }

    internal static async Task ExportToText(this IEnumerable<Event> events, bool alignedWithHeaders = true)
    {
        PropertyInfo[] eventFields = [.. EventFieldsForText];
        var headers = eventFields.Select(f => f.Name).ToList();

        var rows = events.Select(evt =>
            eventFields.Select(f =>
            {
                var value = f.GetValue(evt, null);

                return value switch
                {
                    string str => str,
                    DateTime date => date.ToString("yyyy-MM-dd"),
                    _ => value?.ToString() ?? string.Empty
                };
            }).ToList()
        ).ToList();

        // Calculate column widths
        int[]? widths = alignedWithHeaders ? [.. headers.Select((h, i) => Math.Max(h.Length, rows.Max(r => r[i].Length)))]
            : null; // unused

        var sb = new StringBuilder();

        if (alignedWithHeaders)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(alignedWithHeaders ? headers[i].PadRight(widths![i]) : headers[i]);
            }

            sb.AppendLine();
        }

        // Data rows
        foreach (var row in rows)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(alignedWithHeaders ? row[i].PadRight(widths![i]) : row[i]);
            }

            sb.AppendLine();
        }

        await ExportFile(fileTypeLabel: "Text", contents: sb.ToString(), extension: "txt", contentType: MediaTypeNames.Text.Plain);
    }

    private static async Task ExportFile(string fileTypeLabel, string contents, string extension, string contentType)
    {
        string filePath = GetExportFilePath(extension);
        await FileHelper.WriteAsync(filePath, contents);
        ShareFile(fileTypeLabel, filePath, contentType);
    }

    internal static void ShareFile(string fileTypeLabel, string filePath, string contentType)
        => FileHelper.ShareFile(filePath, contentType, title: $"Share {fileTypeLabel} export");

    private static string GetExportFilePath(string extension)
        => Path.Combine(MauiProgram.StoragePath, "exports",
            $"{AppInfo.Name} export {DateTime.Now:yyyy-MM-dd HH-mm-ss}.{extension}");
}