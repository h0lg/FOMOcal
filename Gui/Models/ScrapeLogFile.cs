using FomoCal.Gui;

namespace FomoCal;

internal static class ScrapeLogFile
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

    private static string GetNamePrefix(Venue venue)
        // use something required and unique - to avoid mis-matches due to overlapping venue names
        => venue.ProgramUrl.MakeFileNameSafe();

    private static string GeneratePath(Venue venue)
        => Path.Combine(folder, $"{GetNamePrefix(venue)} {DateTime.Now.ToString(timeFormat)}{extension}");
}
