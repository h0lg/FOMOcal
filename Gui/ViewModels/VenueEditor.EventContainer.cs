using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using FomoCal.Gui.Resources;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    private IDomElement[]? previewedEvents;

    [ObservableProperty] public partial bool PreviewRelatedHasFocus { get; set; } // for when related controls have focus
    [ObservableProperty] public partial ValuePreview[]? PreviewedEventTexts { get; set; }
    [ObservableProperty] public partial ushort SkipEvents { get; set; }
    [ObservableProperty] public partial ushort TakeEvents { get; set; } = 5;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))] public partial int SelectedEventCount { get; set; } = 0;
    [ObservableProperty] public partial int FilteredEventCount { get; set; } = 0;

    public string EventSelector
    {
        get => venue.Event.Selector;
        set
        {
            if (value == venue.Event.Selector) return;
            venue.Event.Selector = value;

            if (LazyLoaded) SetDocument(null); // because then loaded HTML depends on venue.Event.Selector
            else previewedEvents = null; // otherwise SetDocument takes care of it

            OnPropertyChanged();
            RevealMore();
        }
    }

    public bool LazyLoaded
    {
        get => venue.Event.LazyLoaded;
        set
        {
            if (value == venue.Event.LazyLoaded) return;
            venue.Event.LazyLoaded = value;
            SetDocument(null);
            OnPropertyChanged();
        }
    }

    public string? EventFilter
    {
        get => venue.Event.Filter;
        set
        {
            if (value == venue.Event.Filter) return;
            venue.Event.Filter = value;
            previewedEvents = null;
            OnPropertyChanged();
        }
    }

    public int? LastEventCount => venue.LastEventCount;
    public DateTime? LastRefreshed => venue.LastRefreshed;

    private void UpdateEventContainerPreview()
    {
        if (programDocument == null)
        {
            PreviewedEventTexts = null;
            return;
        }

        try
        {
            var selectedEvents = programDocument.SelectEvents(venue).ToArray();
            var filtered = selectedEvents.FilterEvents(venue).ToArray();
            SelectedEventCount = selectedEvents.Length;
            FilteredEventCount = filtered.Length;
            previewedEvents = [.. filtered.Skip(SkipEvents).Take(TakeEvents)];
            PreviewedEventTexts = [.. previewedEvents.Select(e => ValuePreview.Create(e.TextContent.NormalizeWhitespace()))];
            scrapeJobEditors.ForEach(e => e.UpdatePreview());
        }
        catch (Exception ex)
        {
            previewedEvents = null;
            PreviewedEventTexts = [ValuePreview.Error(ex)];
        }
    }

    partial class Page
    {
        private Grid EventContainer()
        {
            var help = HelpLabel();
            Label scrapeConfigInfo = Lbl("ⓘ");

            scrapeConfigInfo.TapGesture(async () =>
                await help.InlineHelpTextAsync(HelpTexts.ScrapeConfigInfo, host: scrapeConfigInfo,
                    focused: help.label.BindingContext != scrapeConfigInfo)); // close help if already opened

            var lastEventCount = BndLbl(nameof(LastEventCount))
                .StyleClass(Styles.Label.VenueRowDetail)
                .BindIsVisibleToHasValueOf<Label, int>(nameof(LastEventCount));

            var lastRefreshed = BndLbl(nameof(LastRefreshed), stringFormat: "last ⛏ {0:d MMM H:mm}")
                .StyleClass(Styles.Label.VenueRowDetail)
                .BindIsVisibleToHasValueOf<Label, DateTime>(nameof(LastRefreshed));

            var selectorText = Edtr(nameof(EventSelector), placeholder: "event container selector");

            var containerSelector = SelectorInput(selectorText, pickRelativeTo: () => (selector: "body", pickDescendant: true));
            (Switch Switch, Grid Wrapper) lazyLoaded = Swtch(nameof(LazyLoaded));

            var eventFilter = Entr(nameof(EventFilter), placeholder: "text or XPath");

            var previewOrErrors = ValuePreview.List(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(PreviewRelatedHasFocus), source: model!);

            var controls = HWrap(5,
                Lbl("Event container").Bold(),
                BndLbl(nameof(SelectedEventCount), "{0} selected by"), containerSelector,
                BndLbl(nameof(FilteredEventCount), "{0} filtered by"), eventFilter,
                Lbl("lazy"), lazyLoaded.Wrapper);

            var skip = NumericStepper.Create(nameof(SkipEvents), "skipping");
            var take = NumericStepper.Create(nameof(TakeEvents), startLabel: "and taking", max: 10);
            var previewControls = HWrap(5, Lbl("Preview events").Bold(), skip.Wrapper, take.Wrapper);

            VisualElement[] previewRelated = [selectorText, eventFilter, lazyLoaded.Switch, skip.Entry, take.Entry];

            selectorText.InlineTooltipOnFocus(HelpTexts.EventContainerSelector, help,
                onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused),
                /*  Only propagate the loss of focus to the property
                    if entry has not currently opened the visualSelector
                    to keep the help visible while working there */
                cancelFocusChanged: (vis, focused) => !focused && model!.visualSelectorHost == vis);

            eventFilter.InlineTooltipOnFocus(string.Format(HelpTexts.EventContainerFilterFormat, FomoCal.ScrapeJob.XPathSelectorPrefix),
                help, onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            lazyLoaded.Switch.InlineTooltipOnFocus(HelpTexts.LazyLoaded, help,
                onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            skip.Entry.InlineTooltipOnFocus("how many selected events to skip for the preview", help,
                onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            take.Entry.InlineTooltipOnFocus("the maximum number of selected events to show in the preview", help,
                onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            return Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Auto, Auto, Auto, Auto], spacing: 5,
                Lbl("How to dig a gig").StyleClass(Styles.Label.SubHeadline),
                scrapeConfigInfo.CenterVertical().Column(1),
                lastEventCount.Column(2),
                lastRefreshed.Column(3),
                help.layout.Row(1).ColumnSpan(4),
                controls.View.Row(2).ColumnSpan(4),
                previewControls.View.Row(3).ColumnSpan(4),
                previewOrErrors.Row(4).ColumnSpan(4));

            async Task TogglePreviewRelatedFocus(bool focused)
            {
                if (focused) model!.PreviewRelatedHasFocus = true;
                else
                {
                    await Task.Delay(300); // to allow for using the skip/take steppers without flickering
                    if (!previewRelated.Any(vis => vis.IsFocused)) model!.PreviewRelatedHasFocus = false;
                }
            }
        }
    }
}
