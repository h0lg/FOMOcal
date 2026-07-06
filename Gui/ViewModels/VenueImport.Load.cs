using System.Net.Mime;

namespace FomoCal.Gui.ViewModels;

public partial class VenueImport
{
    internal static async Task<HashSet<Venue>?> ChooseSourceAndLoadAsync()
    {
        const string fromFile = "a local JSON file",
            fromUrl = "a web link",
            fromWiki = "the FOMOcal wiki",
            nowhere = "nowhere";

        var source = await App.CurrentPage.DisplayActionSheetAsync(
            "Import venues from", nowhere, null, fromWiki, fromFile, fromUrl);

        if (source == null || source == nowhere) return null;

        if (source == fromWiki)
        {
            // use external browser app because WebView doesn't allow downloads
            await Launcher.OpenAsync($"{MauiProgram.RepoUrl}/wiki/Venues-by-city");
            return null;
        }

        if (source == fromUrl)
        {
            if (!App.HasInternet)
            {
                await App.CurrentPage.DisplayAlertAsync("Connect the internet first",
                    "...and then we can download from there. Sounds good?", "Sure");

                return null;
            }

            var url = await App.CurrentPage.DisplayPromptAsync("Import venues from the web",
                "Enter the URL to the JSON file to import",
                placeholder: "https://some.page/venues.json");

            if (url == null) return null;

            if (!url.EndsWith(".json"))
            {
                var proceed = await App.CurrentPage.DisplayAlertAsync("Unexpected file extension",
                    "That link doesn't have the .json file extension. Try to download a file from it anyway?",
                    accept: "Yes", cancel: "No");

                if (!proceed) return null;
            }

            try
            {
                using HttpClient httpClient = new();
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                const string json = MediaTypeNames.Application.Json;
                string? mediaType = response.Content.Headers.ContentType?.MediaType;

                if (mediaType != json)
                {
                    var proceed = await App.CurrentPage.DisplayAlertAsync("Unexpected content type",
                        $"The content type {mediaType} of the downloaded file is not {json}."
                            + " Try to load venues from it anyway?",
                        accept: "Yes", cancel: "No");

                    if (!proceed) return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonFileStore.DeserializeFromAsync<HashSet<Venue>>(stream);
            }
            catch (Exception ex)
            {
                await App.CurrentPage.DisplayAlertAsync("Error importing venues from " + url, ex.Message, "OK");
            }
        }
        else if (source == fromFile)
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Pick a venues config",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                    { DevicePlatform.Android, ["application/json"] },
                    { DevicePlatform.WinUI, [".json"] } })
            });

            if (file == null) return null;

            try
            {
                return await JsonFileStore.DeserializeFrom<HashSet<Venue>>(file.FullPath);
            }
            catch (Exception ex)
            {
                await App.CurrentPage.DisplayAlertAsync("Error importing venues from " + file.FullPath, ex.Message, "OK");
            }
        }

        return null;
    }
}
