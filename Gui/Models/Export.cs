using System.Text;
using System.Text.RegularExpressions;
using FomoCal.Gui;

namespace FomoCal;

internal static class Export
{
    private static readonly Regex TimeRegex = new(@"(\d{1,2})(?::(\d{2}))?", RegexOptions.Compiled);

    private static bool TryParseStartTime(string? startTimeStr, out TimeSpan time)
    {
        time = default;
        if (startTimeStr.IsNullOrWhiteSpace()) return false;

        var match = TimeRegex.Match(startTimeStr!);
        if (!match.Success) return false;

        if (int.TryParse(match.Groups[1].Value, out int hours))
        {
            int minutes = 0;
            int.TryParse(match.Groups[2].Value, out minutes);
            time = new TimeSpan(hours, minutes, 0); // Assume full hour if no minutes are given
            return true;
        }

        return false;
    }

    // see https://en.wikipedia.org/wiki/ICalendar
    internal static async Task ExportToIcs(this IEnumerable<Event> events)
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
            TryParseStartTime(evt.StartTime, out TimeSpan startTime);
            if (startTime == default) TryParseStartTime(evt.DoorsTime, out startTime);
            var localTime = evt.Date.Date + startTime;
            DateTime.SpecifyKind(localTime, DateTimeKind.Local); // interpret time as local, but export UTC
            sb.AppendLine($"DTSTART:{localTime.ToUniversalTime():yyyyMMddTHHmmssZ}");

            var location = evt.Stage.HasSignificantValue() ? $"{evt.Stage}, {evt.Venue}" : evt.Venue;
            sb.AppendLine($"LOCATION:{location}");

            if (evt.Genres.HasSignificantValue()) sb.AppendLine($"CATEGORIES:{evt.Genres}");
            if (evt.Url.HasSignificantValue()) sb.AppendLine($"URL:{evt.Url}");
            if (evt.ImageUrl.HasSignificantValue()) sb.AppendLine($"IMAGE;VALUE=URI:{evt.ImageUrl}");

            // Description: Append details not covered by standard fields
            var descriptionParts = new List<string>();
            if (evt.SubTitle.HasSignificantValue()) descriptionParts.Add(evt.SubTitle!);
            if (evt.Description.HasSignificantValue()) descriptionParts.Add(evt.Description!);
            if (evt.DoorsTime.HasSignificantValue()) descriptionParts.Add($"Doors: {evt.DoorsTime}");
            if (evt.StartTime.HasSignificantValue()) descriptionParts.Add($"Starts: {evt.StartTime}");
            if (evt.TicketUrl.HasSignificantValue()) descriptionParts.Add($"Tickets: {evt.TicketUrl}");
            if (evt.PresalePrice.HasSignificantValue()) descriptionParts.Add($"Presale: {evt.PresalePrice}");
            if (evt.DoorsPrice.HasSignificantValue()) descriptionParts.Add($"At the door: {evt.DoorsPrice}");

            if (descriptionParts.Count > 0)
                sb.AppendLine($"DESCRIPTION:{descriptionParts.Join("\\n")}");

            sb.AppendLine($"DTSTAMP:{evt.Scraped:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"SOURCE:{MauiProgram.RepoUrl}");
            sb.AppendLine($"X-FOMOCAL-GENERATED-BY:{MauiProgram.Name} v{MauiProgram.GetAppVersion()}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        await ExportFile("iCalendar", sb.ToString(), "ics");
    }

    internal static async Task ExportToCsv(this IEnumerable<Event> events)
    {
        var columns = typeof(Event).GetProperties();
        var sb = new StringBuilder();
        sb.AppendLine(columns.Select(p => p.Name).Join(","));

        foreach (var evt in events)
            sb.AppendLine(columns.Select(c =>
            {
                var value = c.GetValue(evt, null);
                if (value is string str) return str.CsvEscape();
                if (value is DateTime date) return date.ToString("yyyy-MM-dd");
                return value?.ToString() ?? "";
            }).Join(","));

        await ExportFile("CSV", sb.ToString(), "csv");
    }

    private static async Task ExportFile(string fileType, string contents, string extension)
    {
        string filePath = GetExportFilePath(extension);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, contents);
        await ShareFile(fileType, filePath);
    }

    internal static async Task ShareFile(string fileType, string filePath)
    {
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Share " + fileType + " export",
            File = new ShareFile(filePath)
        });
    }

    private static string GetExportFilePath(string extension)
        => Path.Combine(MauiProgram.StoragePath, "exports",
            $"{MauiProgram.Name} export {DateTime.Now:yyyy-MM-dd HH-mm-ss}.{extension}");

}