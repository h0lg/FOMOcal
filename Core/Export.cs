using System.Net.Mime;
using System.Text;

namespace FomoCal;

public static partial class Export
{
    private static IFileSystem? fileSystem;
    private static string? AppName, AppVersion, RepoUrl, StoragePath;
    private static Func<string, string?, string?, string[], Task<string?>>? DisplayActionSheet;

    public static void Setup(IFileSystem fileSystem, string appName, string appVersion,
        string repoUrl, string storagePath, Func<string, string?, string?, string[], Task<string?>> displayActionSheet)
    {
        Export.fileSystem = fileSystem;
        AppName = appName;
        AppVersion = appVersion;
        RepoUrl = repoUrl;
        StoragePath = storagePath;
        DisplayActionSheet = displayActionSheet;
    }

    public static async Task ExportToCsv(this IEnumerable<Event> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Event.Fields.Select(p => p.Name).Join(","));

        foreach (var evt in events)
            sb.AppendLine(Event.Fields.Select(c =>
            {
                var value = c.GetValue(evt, null);
                if (value is string str) return str.CsvEscape();
                if (value is DateTime date) return date.ToString("yyyy-MM-dd");
                return value?.ToString() ?? "";
            }).Join(","));

        await ExportFile("CSV", contents: sb.ToString(),
            extension: "csv", contentType: MediaTypeNames.Text.Csv, Encoding.UTF8);
    }

    private static async Task ExportFile(string fileTypeLabel, string contents,
        string extension, string contentType, Encoding? encoding = null)
    {
        const string share = "Share or copy the file.", open = "Open - to import or read it.";

        var choice = await DisplayActionSheet!(
            $"{fileTypeLabel} export generated.", null, null, [share, open, "Ignore it."]);

        if (choice != open && choice != share) return; // nothing to do

        string filePath = GetExportFilePath(extension);
        await fileSystem!.WriteAsync(filePath, contents, encoding);

        if (choice == open) await fileSystem!.OpenFileAsync(filePath, $"Open {fileTypeLabel} export", contentType);
        else if (choice == share) ShareFile(fileTypeLabel, filePath, contentType);
        // leave export file in FS after - otherwise opening or sharing fails
    }

    internal static void ShareFile(string fileTypeLabel, string filePath, string contentType)
        => fileSystem!.ShareFile(filePath, contentType, title: $"Share {fileTypeLabel} export");

    private static string GetExportFilePath(string extension)
        => Path.Combine(StoragePath!, $"{AppName} export {DateTime.Now:yyyy-MM-dd HH-mm-ss}.{extension}");
}
