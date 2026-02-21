using System.Net.Mime;
using System.Reflection;
using System.Text;

namespace FomoCal;

static partial class Export
{
    public static async Task ExportToText(this IEnumerable<Event> events, PropertyInfo[] eventFields, bool alignedWithHeaders = true)
    {
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

        await ExportFile(fileTypeLabel: "Text", contents: sb.ToString(),
            extension: "txt", contentType: MediaTypeNames.Text.Plain, Encoding.UTF8);
    }
}
