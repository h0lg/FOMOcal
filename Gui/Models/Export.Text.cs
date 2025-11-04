using System.Net.Mime;
using System.Reflection;
using System.Text;

namespace FomoCal;

internal static partial class Export
{
    private static readonly RememberedStrings textEventFields = new("Export.TextEventFields");

    internal static IEnumerable<PropertyInfo> EventFieldsForText
    {
        get => LoadEventProperties(textEventFields, () => [nameof(Event.Date), nameof(Event.Name), nameof(Event.Venue)]);
        set => SaveEventProperties(value, textEventFields);
    }

    internal static bool TextAlignedWithHeaders
    {
        get => Preferences.Get(textAlignedWithHeadersPreferencesKey, true);
        set => Preferences.Set(textAlignedWithHeadersPreferencesKey, value);
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
}
