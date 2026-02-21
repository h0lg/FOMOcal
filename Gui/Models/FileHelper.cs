using System.Text;

namespace FomoCal;

internal class FileHelper : IFileSystem
{
    public async Task WriteAsync(string filePath, string contents, Encoding? encoding = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        if (encoding == null) await File.WriteAllTextAsync(filePath, contents); // saves UTF-8 w/o BOM by default
        else await File.WriteAllTextAsync(filePath, contents, encoding);
    }

    public void ShareFile(string filePath, string contentType, string title)
        => MainThread.BeginInvokeOnMainThread(async () =>
            // see https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/share#share-a-file
            await Share.Default.RequestAsync(
                new ShareFileRequest { Title = title, File = new ShareFile(filePath, contentType) }));

    public Task OpenFileAsync(string path, string? title = null, string? contentType = null)
    {
        ReadOnlyFile file = string.IsNullOrWhiteSpace(contentType) ? new(path) : new(path, contentType);
        return Launcher.OpenAsync(new OpenFileRequest { Title = title, File = file });
    }
}
