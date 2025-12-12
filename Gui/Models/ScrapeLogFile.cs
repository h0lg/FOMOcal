using FomoCal.Gui;

namespace FomoCal;

public interface ISaveScrapeLogFiles
{
    Task<string?> SaveScrapeLogAsync(Venue venue, string log);
}

internal class DefaultScrapeLogFileSaver : ISaveScrapeLogFiles
{
    public Task<string?> SaveScrapeLogAsync(VenueScrapeContext venueScrape)
        => ScrapeLogFile.Save(venueScrape.Venue, venueScrape.GetScrapeLog());

    public Task<string?> SaveScrapeLogAsync(Venue venue, string log) => ScrapeLogFile.Save(venue, log);
}

public static class ScrapeLogFile
{
    private const string timeFormat = "yyyy-MM-dd HH-mm-ss", extension = ".txt";
    private static readonly string folder = Path.Combine(MauiProgram.StoragePath, "scrape logs");

    static ScrapeLogFile() => Directory.CreateDirectory(folder);

    internal static async Task<string?> Save(Venue venue, string contents)
    {
        try
        {
            string filePath = GeneratePath(venue);
            await FileHelper.WriteAsync(filePath, contents);
            return filePath;
        }
        catch (Exception ex)
        {
            await ErrorReport.WriteAsync(ex.ToString(), "writing scrape log");
            return null;
        }
    }

    /// <summary>Returns the existing scrape logs for the <paramref name="venue"/>,
    /// file paths (values) by time stamps (keys).</summary>
    internal static IEnumerable<ForVenue> GetAll(Venue venue)
    {
        string prefix = GetNamePrefix(venue);
        if (prefix.IsNullOrWhiteSpace()) return [];
        string[] paths = Directory.GetFiles(folder, $"{prefix}*{extension}");
        if (paths.Length == 0) return [];

        /* number of chars preceding the time in the file path,
            including one path separator and one space in between name and time */
        int timeStartsAt = folder.Length + prefix.Length + 2;

        // use timestamp in file name as key, full path as value
        return paths.Select(path => new ForVenue(path.Substring(timeStartsAt, timeFormat.Length), path));
    }

    private static string GetNamePrefix(Venue venue)
        // use something required and unique - to avoid mis-matches due to overlapping venue names
        => venue.ProgramUrl.MakeFileNameSafe();

    private static string GeneratePath(Venue venue)
        => Path.Combine(folder, $"{GetNamePrefix(venue)} {DateTime.Now.ToString(timeFormat)}{extension}");

    public record ForVenue(string TimeStamp, string Path);
}
