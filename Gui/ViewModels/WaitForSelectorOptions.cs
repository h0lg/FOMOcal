namespace FomoCal.Gui.ViewModels;

partial class AutomatedEventPageView
{
    internal class WaitForSelectorOptions
    {
        public string? Selector { get; set; }
        public bool IsXpathSelector { get; set; }
        public uint MaxTries { get; set; }
        public uint IntervalDelayMs { get; set; }
        public uint MaxMatchesScrollingDown { get; set; }
    }

    /* check every 200ms for 25 resetting iterations,
    * i.e. wait for approx. 5sec for JS rendering or scrolling down to load more before timing out
    * while a change in the number of matched events resets the iterations (and wait time)
    * until we time out or load at least 100 events. */
    private readonly WaitForSelectorOptions waitForSelectorOptions = new() { MaxTries = 25, IntervalDelayMs = 200, MaxMatchesScrollingDown = 100 };

    private string GetWaitForSelectorOptions()
    {
        var isXpath = ScrapeJob.TryGetXPathSelector(venue.Event.Selector, out var xPathSelector);
        waitForSelectorOptions.IsXpathSelector = isXpath;
        waitForSelectorOptions.Selector = isXpath ? xPathSelector! : venue.Event.Selector;
        return ToJsonOptions(waitForSelectorOptions);
    }
}
