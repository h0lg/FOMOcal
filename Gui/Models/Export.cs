using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using FomoCal.Gui;

namespace FomoCal;

internal static partial class Export
{
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

        await ExportFile("CSV", contents: sb.ToString(), extension: "csv", contentType: MediaTypeNames.Text.Csv);
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