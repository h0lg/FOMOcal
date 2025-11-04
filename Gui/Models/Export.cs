using System.Net.Mime;
using System.Reflection;
using System.Text;
using FomoCal.Gui;

namespace FomoCal;

internal static partial class Export
{
    private const string textAlignedWithHeadersPreferencesKey = "Export.TextAlignedWithHeaders";

    internal static readonly PropertyInfo[] EventFields = [.. typeof(Event).GetProperties().Where(p => p.Name != nameof(Event.IsPast))];

    private static IEnumerable<PropertyInfo> LoadEventProperties(RememberedStrings remembered, Func<string[]> getDefaults)
    {
        var saved = remembered.Get();
        if (saved.Length == 0) saved = getDefaults();
        return saved.Select(name => EventFields.First(p => p.Name == name));
    }

    private static void SaveEventProperties(IEnumerable<PropertyInfo> value, RememberedStrings remembered)
        => remembered.Set(value.Select(p => p.Name));

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
