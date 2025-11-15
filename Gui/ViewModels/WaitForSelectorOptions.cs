namespace FomoCal.Gui.ViewModels;

partial class AutomatedEventPageView
{
    internal class WaitForSelectorOptions
    {
        public string? Selector { get; set; }

        /// <summary>Whether <see cref="Selector"/> is an XPath expression. If false, it's CSS.</summary>
        public bool IsXpathSelector { get; set; }

        /// <summary>How many times to repeat checking for the <see cref="Selector"/>
        /// after <see cref="IntervalDelayMs"/> before giving up.
        /// Used by <see cref="Venue.EventScrapeJob.LazyLoaded"/>,
        /// <see cref="Venue.PagingStrategy.ClickElementToLoadMore"/>
        /// and <see cref="Venue.PagingStrategy.ScrollDownToLoadMore"/>.</summary>
        public ushort MaxTries { get; set; }

        /// <summary>How many milliseconds to wait before checking for the <see cref="Selector"/> (again).
        /// Used with <see cref="MaxTries"/> by <see cref="Venue.EventScrapeJob.LazyLoaded"/>,
        /// <see cref="Venue.PagingStrategy.ClickElementToLoadMore"/>
        /// and <see cref="Venue.PagingStrategy.ScrollDownToLoadMore"/>.</summary>
        public ushort IntervalDelayMs { get; set; }

        /// <summary>Used by <see cref="Venue.PagingStrategy.ScrollDownToLoadMore"/> as an early escape condition.
        /// It avoids having to wait for the timeout (defined implicitly by <see cref="MaxTries"/> and <see cref="IntervalDelayMs"/>)
        /// and prevents loading past events if the venue pages into the past instead of the future.
        /// This is required for this paging strategy because it is implemented
        /// to automatically scroll the page down again after <see cref="IntervalDelayMs"/>,
        /// which helps to make the rather flaky nature of the scroll trigger
        /// in a blind, automated browser a bit more reliable.</summary>
        public ushort MaxMatchesScrollingDown { get; set; }

        /// <summary>How many milliseconds to wait between scrolling down the page and triggering the scroll event.
        /// Used by <see cref="Venue.PagingStrategy.ScrollDownToLoadMore"/>.</summary>
        public ushort TriggerScrollAfterMs { get; set; }

        /// <summary>How many milliseconds to wait before considering a started AJAX request to have ended.
        /// Helps count active AJAX requests, used as an early escape condition
        /// by <see cref="Venue.PagingStrategy.ScrollDownToLoadMore"/>.</summary>
        public ushort AjaxTimeoutMs { get; set; }

        /// <summary>How many milliseconds to wait for the document to change after
        /// <see cref="Venue.PagingStrategy.ClickElementToLoadDifferent"/> before timing out.</summary>
        public ushort MutationTimeoutMs { get; set; }

        /// <summary>How many resetting milliseconds to wait for the last document mutation after
        /// <see cref="Venue.PagingStrategy.ClickElementToLoadDifferent"/> before succeeding.
        /// Used to prevent returning the document early, before all mutations are completed.</summary>
        public ushort MutationDebounceMs { get; set; }

        internal static WaitForSelectorOptions LoadSettings() => new()
        {
            MaxTries = Remembered.MaxTries.Get(),
            IntervalDelayMs = Remembered.IntervalDelayMs.Get(),
            MaxMatchesScrollingDown = Remembered.MaxMatchesScrollingDown.Get(),
            TriggerScrollAfterMs = Remembered.TriggerScrollAfterMs.Get(),
            AjaxTimeoutMs = Remembered.AjaxTimeoutMs.Get(),
            MutationTimeoutMs = Remembered.MutationTimeoutMs.Get(),
            MutationDebounceMs = Remembered.MutationDebounceMs.Get()
        };

        internal static class Remembered
        {
            internal static readonly RememberedUshort MaxTries = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.MaxTries), 25);
            internal static readonly RememberedUshort IntervalDelayMs = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.IntervalDelayMs), 200);
            internal static readonly RememberedUshort MaxMatchesScrollingDown = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.MaxMatchesScrollingDown), 100);
            internal static readonly RememberedUshort TriggerScrollAfterMs = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.TriggerScrollAfterMs), 500);
            internal static readonly RememberedUshort AjaxTimeoutMs = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.AjaxTimeoutMs), 3000);
            internal static readonly RememberedUshort MutationTimeoutMs = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.MutationTimeoutMs), 5000);
            internal static readonly RememberedUshort MutationDebounceMs = new(nameof(WaitForSelectorOptions) + nameof(WaitForSelectorOptions.MutationDebounceMs), 200);
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
