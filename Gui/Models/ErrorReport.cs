using System.Net.Mime;
using System.Runtime.InteropServices;
using FomoCal.Gui;

namespace FomoCal;

public static class ErrorReport
{
    internal static readonly string OutputSpacing = Environment.NewLine + Environment.NewLine;

    /// <summary>Uses <see cref="WriteAsync(string, string?)"/> to write the <paramref name="errorReport"/>
    /// and displays an alert on the current page offering to share or ignore it on success
    /// or containing the error if writing fails.</summary>
    public static async Task WriteAsyncAndShare(string errorReport, string during)
    {
        string header = "Errors " + during;
        var (path, report) = await WriteAsync(errorReport, header);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!App.CurrentPage.IsLoaded) return; // displaying alerts won't work

            if (path == null) await App.CurrentPage.DisplayAlertAsync($"{header} and writing report.", report, "OK");
            else
            {
                const string share = "Share or copy the file.", open = "Open to read or copy the contents.";
                var choice = await App.CurrentPage.DisplayActionSheetAsync(header + " - a report was generated.", null, null, share, open, "Ignore it.");

                if (choice == open) await FileHelper.OpenFileAsync(path, header);
                else if (choice == share)
                    FileHelper.ShareFile(path, MediaTypeNames.Text.Plain, title: AppInfo.Name + " error report");
            }
        });
    }

    /// <summary>Prefixes the <paramref name="errors"/> with the optional <paramref name="header"/> and some environment info,
    /// creating a report. It then tries to save that report to a file path. If it succeeds, the path and the report are returned.
    /// Otherwise, only the report is returned and the path is null.</summary>
    public static async Task<(string? path, string report)> WriteAsync(string errors, string? header = null)
    {
        var environmentInfo = new[] { "on", Environment.OSVersion.VersionString, RuntimeInformation.FrameworkDescription,
            AppInfo.Name, MauiProgram.GetAppVersion() }.Join(" ");

        var preamble = header.IsSignificant() ? header + OutputSpacing : string.Empty;
        var report = preamble + environmentInfo + OutputSpacing + errors;

        try
        {
            var path = Path.Combine(MauiProgram.StoragePath, "error reports", $"{AppInfo.Name} error {DateTime.Now:yyyy-MM-dd HH-mm-ss}.txt");
            await FileHelper.WriteAsync(path, report);
            return (path, report);
        }
        catch (Exception ex)
        {
            report += OutputSpacing + "Error writing error log: " + ex;
            return (null, report);
        }
    }
}