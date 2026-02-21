using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text;

namespace FomoCal;

public interface ISaveScrapeLogFiles
{
    Task<string?> SaveScrapeLogAsync(Venue venue, string log);
}

public interface IFileSystem
{
    void ShareFile(string filePath, string contentType, string title);
    Task WriteAsync(string filePath, string contents, Encoding? encoding = null);
    Task OpenFileAsync(string path, string? title = null, string? contentType = null);
}

public class DefaultScrapeLogFileSaver : ISaveScrapeLogFiles
{
    public Task<string?> SaveScrapeLogAsync(Venue venue, string log) => ScrapeLogFile.Save(venue, log);
}

public static class ScrapeLogFile
{
    private const string timeFormat = "yyyy-MM-dd HH-mm-ss", extension = ".txt";
    private static string? storagePath;
    private static string? folder;
    private static IFileSystem? fileSystem;

    private static Action<Exception, string> reportError =
        (_, __) => throw new NotImplementedException("set this before using " + nameof(ScrapeLogFile));

    public static void Setup(string storagePath, IFileSystem fileSystem, Action<Exception, string> reportError)
    {
        ScrapeLogFile.fileSystem = fileSystem;
        ScrapeLogFile.reportError = reportError;
        ScrapeLogFile.storagePath = storagePath;
        folder = Path.Combine(ScrapeLogFile.storagePath, "scrape logs");
        Directory.CreateDirectory(folder);
    }

    internal static async Task<string?> Save(Venue venue, string contents)
    {
        try
        {
            string filePath = GeneratePath(venue);
            await fileSystem!.WriteAsync(filePath, contents, Encoding.UTF8);
            return filePath;
        }
        catch (Exception ex)
        {
            reportError(ex, "writing scrape log");
            return null;
        }
    }

    public static async Task Open(ForVenue log)
    {
        if (TrySanitizePath(log, out var path))
            await fileSystem!.OpenFileAsync(path, "Scrape log", MediaTypeNames.Text.Plain);
    }

    public static void Remove(ForVenue log)
    {
        if (TrySanitizePath(log, out var path))
            File.Delete(path);
    }

    private static bool TrySanitizePath(ForVenue log, [MaybeNullWhen(false)] out string path)
    {
        var fileName = Path.GetFileName(log.Path);

        if (fileName == null)
        {
            path = null;
            return false;
        }

        path = Path.Combine(folder!, fileName);
        return true;
    }

    /// <summary>Returns the existing scrape logs for the <paramref name="venue"/>,
    /// file paths (values) by time stamps (keys).</summary>
    public static IEnumerable<ForVenue> GetAll(Venue venue)
    {
        string prefix = GetNamePrefix(venue);
        if (prefix.IsNullOrWhiteSpace()) return [];
        string[] paths = Directory.GetFiles(folder!, $"{prefix}*{extension}");
        if (paths.Length == 0) return [];

        /* number of chars preceding the time in the file path,
            including one path separator and one space in between name and time */
        int timeStartsAt = folder!.Length + prefix.Length + 2;

        // use timestamp in file name as key, full path as value
        return paths.Select(path => new ForVenue(path.Substring(timeStartsAt, timeFormat.Length), path));
    }

    private static string GetNamePrefix(Venue venue)
        // use something required and unique - to avoid mis-matches due to overlapping venue names
        => venue.ProgramUrl.MakeFileNameSafe();

    private static string GeneratePath(Venue venue)
        => Path.Combine(folder!, $"{GetNamePrefix(venue)} {DateTime.Now.ToString(timeFormat)}{extension}");

    public record ForVenue(string TimeStamp, string Path);
}
