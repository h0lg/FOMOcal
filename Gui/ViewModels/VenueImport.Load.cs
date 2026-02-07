namespace FomoCal.Gui.ViewModels;

public partial class VenueImport
{
    internal static async Task<HashSet<Venue>?> ChooseSourceAndLoadAsync()
    {
        const string fromFile = "From a local JSON file",
            fromUrl = "From a URL on the web",
            nowhere = "Nowhere.";

        var source = await App.CurrentPage.DisplayActionSheetAsync(
            "Where do you want to import venues from?", nowhere, null, fromUrl, fromFile);

        if (source == null || source == nowhere) return null;

        if (source == fromUrl)
        {
            var url = await App.CurrentPage.DisplayPromptAsync("Import venues from the web",
                "Enter the URL to the JSON file to import",
                placeholder: "https://some.page/venues.json");

            if (url == null) return null;

            try
            {
                using HttpClient httpClient = new();
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
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
