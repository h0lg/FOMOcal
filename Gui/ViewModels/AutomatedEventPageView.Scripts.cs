using System.Text.Json;

namespace FomoCal.Gui.ViewModels;

partial class AutomatedEventPageView
{
    /* check every 200ms for 25 resetting iterations,
    * i.e. wait for approx. 5sec for JS rendering or scrolling down to load more before timing out
    * while a change in the number of matched events resets the iterations (and wait time)
    * until we time out or load at least 100 events. */
    private readonly WaitForSelectorOptions waitForSelectorOptions = new() { IntervalDelayMs = 200, MaxMatches = 100, MaxTries = 25 };

    /*  Used to cache the loaded and pre-processed script while allowing for a
     *  thread-safe asynchronous lazy initialization that only ever happens once. */
    private static readonly Lazy<Task<string>> consoleHooksScript = new(() => LoadAndInlineScriptAsync("consoleHooks.js"));
    private static readonly Lazy<Task<string>> waitForSelectorScript = new(() => LoadAndInlineScriptAsync("waitForSelector.js"));
    private static readonly Lazy<Task<string>> pickingScript = new(() => LoadAndInlineScriptAsync("picking.js"));

    private static async Task<string> LoadAndInlineScriptAsync(string fileName)
    {
        // see https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers#bundled-files
        await using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync(fileName);
        using StreamReader reader = new(fileStream);
        string script = await reader.ReadToEndAsync();

        return script.RemoveJsComments() // so that in-line comments don't comment out code during normalization
            .NormalizeWhitespace() // to in-line it; multi-line scripts seem to not be supported
            .Replace("\\", "\\\\"); // to escape the JS for EvaluateJavaScriptAsync
    }

    private static readonly JsonSerializerOptions scriptOptionSerializerOptions
        = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string ToJsonOptions<T>(T scriptOptions) where T : class
        => JsonSerializer.Serialize(scriptOptions, scriptOptionSerializerOptions);

    private string GetWaitForSelectorOptions()
    {
        var isXpath = ScrapeJob.TryGetXPathSelector(venue.Event.Selector, out var xPathSelector);
        waitForSelectorOptions.IsXpathSelector = isXpath;
        waitForSelectorOptions.Selector = isXpath ? xPathSelector! : venue.Event.Selector;
        return ToJsonOptions(waitForSelectorOptions);
    }

    internal class WaitForSelectorOptions
    {
        public string? Selector { get; set; }
        public bool IsXpathSelector { get; set; }
        public uint IntervalDelayMs { get; set; }
        public uint MaxMatches { get; set; }
        public uint MaxTries { get; set; }
    }

    internal interface PickedSelectorOptions
    {
        bool XPathSyntax { get; }
        bool TagName { get; }
        bool Ids { get; }
        bool SemanticClasses { get; }
        bool LayoutClasses { get; }
        bool OtherAttributes { get; }
        bool OtherAttributeValues { get; }
        bool Position { get; }
    }
}
