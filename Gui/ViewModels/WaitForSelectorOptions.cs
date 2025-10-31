namespace FomoCal.Gui.ViewModels;

partial class AutomatedEventPageView
{
    internal class WaitForSelectorOptions
    {
        public string? Selector { get; set; }
        public bool IsXpathSelector { get; set; }
        public ushort MaxTries { get; set; }
        public ushort IntervalDelayMs { get; set; }
        public ushort MaxMatchesScrollingDown { get; set; }

        internal static WaitForSelectorOptions LoadSettings() => new()
        {
            MaxTries = Remembered.MaxTries.Get(),
            IntervalDelayMs = Remembered.IntervalDelayMs.Get(),
            MaxMatchesScrollingDown = Remembered.MaxMatchesScrollingDown.Get()
        };

        internal static class Remembered
        {
            internal static readonly RememberedUshort MaxTries = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.MaxTries), 25);
            internal static readonly RememberedUshort IntervalDelayMs = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.IntervalDelayMs), 200);
            internal static readonly RememberedUshort MaxMatchesScrollingDown = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.MaxMatchesScrollingDown), 100);
        }
    }

    private readonly WaitForSelectorOptions waitForSelectorOptions = WaitForSelectorOptions.LoadSettings();

    private string GetWaitForSelectorOptions()
    {
        var isXpath = ScrapeJob.TryGetXPathSelector(venue.Event.Selector, out var xPathSelector);
        waitForSelectorOptions.IsXpathSelector = isXpath;
        waitForSelectorOptions.Selector = isXpath ? xPathSelector! : venue.Event.Selector;
        return ToJsonOptions(waitForSelectorOptions);
    }
}
