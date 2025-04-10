using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueEditor : ObservableObject
{
    private readonly bool isDeletable;
    private readonly string originalVenueName;
    private readonly Scraper scraper;
    private readonly TaskCompletionSource<Actions?> awaiter;
    private readonly List<ScrapeJobEditor> scrapeJobEditors = [];
    private readonly SemaphoreSlim revealingMore = new(1, 1);
    private Entry? visualSelectorHost;

    [ObservableProperty] private bool isEventPageLoading = true;
    [ObservableProperty] private bool showEventContainer;
    [ObservableProperty] private bool showRequiredEventFields;
    [ObservableProperty] private bool showOptionalEventFields;

    [ObservableProperty] private bool eventSelectorRelatedHasFocus; // for when related controls have focus
    [ObservableProperty] private bool eventSelectorHasError;
    [ObservableProperty] private string[]? previewedEventTexts;
    [ObservableProperty] private int skipEvents;
    [ObservableProperty] private int takeEvents = 5;

    private AngleSharp.Dom.IDocument? programDocument;
    private AngleSharp.Dom.IElement[]? previewedEvents;

    private readonly Venue venue;
    private readonly ScrapeJobEditor eventName, eventDate;

    public string ProgramUrl
    {
        get => venue.ProgramUrl;
        set
        {
            if (value == venue.ProgramUrl) return;
            venue.ProgramUrl = value;
            OnPropertyChanged();
            RevealMore();
        }
    }

    public string VenueName
    {
        get => venue.Name;
        set
        {
            if (value == venue.Name) return;
            venue.Name = value;
            OnPropertyChanged();
            RevealMore();
        }
    }

    public string EventSelector
    {
        get => venue.Event.Selector;
        set
        {
            if (value == venue.Event.Selector) return;
            venue.Event.Selector = value;
            previewedEvents = null;
            if (WaitForJsRendering) programDocument = null; // because then loaded HTML depends on venue.Event.Selector
            OnPropertyChanged();
            RevealMore();
        }
    }

    public bool WaitForJsRendering
    {
        get => venue.Event.WaitForJsRendering;
        set
        {
            if (value == venue.Event.WaitForJsRendering) return;
            venue.Event.WaitForJsRendering = value;
            previewedEvents = null;
            programDocument = null;
            OnPropertyChanged();
        }
    }

    public string? NextEventPageSelector
    {
        get => venue.Event.NextPageSelector;
        set
        {
            if (value == venue.Event.NextPageSelector) return;
            venue.Event.NextPageSelector = value;
            OnPropertyChanged();
        }
    }

    public List<Venue.PagingStrategy> PagingStrategies { get; } = [.. Enum.GetValues<Venue.PagingStrategy>()];

    internal VenueEditor(Venue venue, Scraper scraper, TaskCompletionSource<Actions?> awaiter)
    {
        this.venue = venue;
        this.scraper = scraper;
        this.awaiter = awaiter;
        isDeletable = venue.ProgramUrl.IsSignificant();
        originalVenueName = venue.Name;

        var evt = this.venue.Event;
        eventName = ScrapeJob("❗ Name", evt.Name, nameof(Venue.EventScrapeJob.Name));
        eventDate = ScrapeJob("📆 Date", evt.Date, nameof(Venue.EventScrapeJob.Date));
        eventName.IsValidChanged += (_, _) => RevealMore();
        eventDate.IsValidChanged += (_, _) => RevealMore();

        PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(SkipEvents) || e.PropertyName == nameof(TakeEvents))
                UpdateEventSelectorPreview();
        };

        RevealMore(); // once initially if we're editing to reveal event container
    }

    private ScrapeJobEditor ScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty,
        bool isOptional = false, string? defaultAttribute = null)
    {
        ScrapeJobEditor editor = new(label, scrapeJob ?? new ScrapeJob(),
            () => previewedEvents, () => visualSelectorHost,
            eventProperty, isOptional, defaultAttribute);

        scrapeJobEditors.Add(editor);
        return editor;
    }

    private async void RevealMore()
    {
        await revealingMore.WaitAsync();
        bool hasProgramUrl = venue.ProgramUrl.IsSignificant();
        bool hasName = VenueName.IsSignificant();

        if (programDocument != null && !hasName && programDocument.Title.IsSignificant())
        {
            VenueName = programDocument.Title!;
            hasName = true;
        }

        ShowEventContainer = hasName && hasProgramUrl;
        ShowRequiredEventFields = ShowEventContainer && EventSelector.IsSignificant();
        ShowOptionalEventFields = ShowRequiredEventFields && eventName.IsValid && eventDate.IsValid;
        if (ShowRequiredEventFields && previewedEvents == null) UpdateEventSelectorPreview();
        revealingMore.Release();
    }

    private void UpdateEventSelectorPreview()
    {
        if (programDocument == null)
        {
            PreviewedEventTexts = null;
            EventSelectorHasError = false;
            return;
        }

        try
        {
            previewedEvents = programDocument.SelectEvents(venue).Skip(SkipEvents).Take(TakeEvents).ToArray();
            PreviewedEventTexts = previewedEvents.Select(e => e.TextContent.NormalizeWhitespace()).ToArray();
            scrapeJobEditors.ForEach(e => e.UpdatePreview());
            EventSelectorHasError = false;
        }
        catch (Exception ex)
        {
            previewedEvents = null;
            PreviewedEventTexts = [ex.Message];
            EventSelectorHasError = true;
        }
    }

    [RelayCommand]
    private void Save()
    {
        // reset empty optional scrape jobs
        foreach (var editor in scrapeJobEditors.Where(e => e.IsOptional))
        {
            var property = typeof(Venue.EventScrapeJob).GetProperty(editor.EventProperty)!;
            property.SetValue(venue.Event, editor.IsEmpty ? null : editor.ScrapeJob);
        }

        awaiter.SetResult(Actions.Saved);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        bool isConfirmed = await App.CurrentPage.DisplayAlert("Confirm Deletion",
            $"Are you sure you want to delete the venue {venue.Name}?",
            "Yes", "No");

        if (isConfirmed) awaiter.SetResult(Actions.Deleted);
    }

    internal enum Actions { Saved, Deleted }

    public partial class Page : ContentPage
    {
        private readonly VenueEditor model;
        private readonly ScrollView form;

        public Page(VenueEditor model)
        {
            this.model = model;
            BindingContext = model;
            Title = model.isDeletable ? "Edit " + model.originalVenueName : "Add a venue";

            // Step 1: Venue Name and Program URL
            var venueFields = VenueFields();

            // Step 2: Event Selector
            var eventContainer = EventContainerSelector().BindVisible(nameof(ShowEventContainer));

            // Step 3: Event Details (Name, Date)
            var requiredEventFields = VStack(0,
                new ScrapeJobEditor.View(model.eventName, RelativeSelectorEntry),
                new ScrapeJobEditor.View(model.eventDate, RelativeSelectorEntry))
                .BindVisible(nameof(ShowRequiredEventFields));

            // Step 4: Additional Event Details
            var optionalEventFields = OptionalEventFields().BindVisible(nameof(ShowOptionalEventFields));

            // form controls
            var saveButton = Btn("💾 Save this venue", nameof(SaveCommand))
                .BindVisible(nameof(ShowOptionalEventFields));

            var deleteButton = Btn("🗑 Delete this venue", nameof(DeleteCommand)).IsVisible(model.isDeletable);

            form = new ScrollView
            {
                Content = VStack(20,
                    //progress,
                    venueFields, eventContainer, requiredEventFields, optionalEventFields, saveButton, deleteButton)
                    .Padding(20)
            };

            visualSelector = CreateVisualSelector();
            Content = Grd(cols: [Star], rows: [Star, Auto], spacing: 0, form, visualSelector.Row(1));
        }

        private Grid VenueFields()
        {
            var urlEntry = Entr(nameof(ProgramUrl), placeholder: "Program page URL");
            var nameEntry = Entr(nameof(VenueName), placeholder: "Venue name");

            var location = new Entry { Placeholder = "Location, contacts or other helpful info" }
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            var loadingIndicator = new ActivityIndicator { IsRunning = true }
                .BindVisible(new Binding(nameof(ProgramUrl), converter: Converters.IsSignificant),
                    Converters.And, new Binding(nameof(IsEventPageLoading)));

            var reload = Btn("⟳").TapGesture(Reload)
                .BindVisible(new Binding(nameof(ProgramUrl), converter: Converters.IsSignificant),
                    Converters.And, new Binding(nameof(IsEventPageLoading), converter: Converters.Not));

            return Grd(cols: [Auto, Star, Auto], rows: [Auto, Auto, Auto], spacing: 5,
                FldLbl("🕸"), urlEntry.Column(1), loadingIndicator.Column(2), reload.Column(2),
                FldLbl("🏷").Row(1), nameEntry.Row(1).Column(1).ColumnSpan(2),
                FldLbl("📍").Row(2), location.Row(2).Column(1).ColumnSpan(2));

            static Label FldLbl(string Text) => Lbl(Text).CenterVertical();
        }

        private VerticalStackLayout EventContainerSelector()
        {
            Label help = new();
            var selectorText = Entr(nameof(EventSelector), placeholder: "event container selector");

            selectorText.InlineTooltipOnFocus(
                "The selector to the event containers - of which there are probably multiple on the page," +
                " each containing as many of one event's details as possible - but only of a single event." +
                "\n\nSome event pages for example display multiple events on the same day in a group." +
                " If you see it, use skip/take to try it out on such a group and choose a container that contains only one of their details" +
                " - otherwise only the first event on any given day will be retrieved." +
                "\nYou'll be able to select the date or other excluded event details from outside your selected container later.",
                help, async (vis, focused) =>
                {
                    if (focused) model.EventSelectorRelatedHasFocus = true;
                    else
                    {
                        await Task.Delay(300); // to allow for using the skip/take steppers without flickering
                        if (!selectorText.IsFocused) model.EventSelectorRelatedHasFocus = false;
                    }
                    /*  Only propagate the loss of focus to the property
                        if entry has not currently opened the visualSelector
                        to keep the help visible while working there */
                }, cancelFocusChanged: (vis, focused) => !focused && model.visualSelectorHost == vis);

            var containerSelector = SelectorEntry(selectorText, pickRelativeTo: () => (selector: "body", pickDescendant: true));
            (Switch Switch, Grid Wrapper) waitForJsRendering = Toggle(nameof(WaitForJsRendering));

            waitForJsRendering.Switch.InlineTooltipOnFocus(
                "You may want to try this option if your event selector doesn't match anything without it even though it should*." +
                "\nIt will load the page and wait for an element matching your selector to become available," +
                " return when it does and time out if it doesn't after 10s." +
                "\n\nThis works around pages that lazy-load events." +
                " Some web servers only return an empty template of a page on the first request to improve the response time," +
                " then fetch more data asynchronously and render it into the placeholders using a script running in your browser." +
                "\n\n* To test selectors, load the page in your browser and start up a" +
                " [developer console](https://developer.mozilla.org/en-US/docs/Learn_web_development/Howto/Tools_and_setup/What_are_browser_developer_tools#the_javascript_console)." +
                " In there, use [document.querySelectorAll('.css-selector')](https://www.w3schools.com/jsref/met_document_queryselectorall.asp)" +
                " or [document.evaluate('//xpath/selector', document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null).snapshotLength](https://developer.mozilla.org/en-US/docs/Web/API/Document/evaluate)" +
                " depending on your chosen selector syntax.",
                help);

            Picker pagingStrategy = new()
            {
                ItemsSource = model.PagingStrategies.ConvertAll(e => e.GetDescription()),
                SelectedIndex = model.PagingStrategies.IndexOf(model.venue.Event.PagingStrategy)
            };

            pagingStrategy.SelectedIndexChanged += (s, e) =>
            {
                if (pagingStrategy.SelectedIndex >= 0)
                    model.venue.Event.PagingStrategy = model.PagingStrategies[pagingStrategy.SelectedIndex];
            };

            var nextPageSelector = SelectorEntry(
                Entr(nameof(NextEventPageSelector)).Placeholder("next page")
                    .InlineTooltipOnFocus("The selector of the element to click or link to navigate to load more or different events.", help,
                        cancelFocusChanged: (vis, focused) => !focused && model.visualSelectorHost == vis),
                pickRelativeTo: () => (selector: "body", pickDescendant: true))
                .BindVisible(nameof(Picker.SelectedIndex), pagingStrategy,
                    Converters.Func<int>(i => model.PagingStrategies[i].RequiresNextPageSelector()));

            var previewOrErrors = ScrapeJobEditor.View.PreviewOrErrorList(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(EventSelectorRelatedHasFocus),
                hasError: nameof(EventSelectorHasError), source: model);

            return VStack(spacing: 5,
                Lbl("How to dig a gig").StyleClass(Styles.Label.SubHeadline),
                help,
                HWrap(5,
                    Lbl("Event container").Bold(), containerSelector,
                    Lbl("wait for JS rendering"), waitForJsRendering.Wrapper,
                    Lbl("loading"), pagingStrategy,
                    nextPageSelector,
                    Lbl("Preview events").Bold(),
                    LabeledStepper("skipping", nameof(SkipEvents), max: 100, onValueChanged: () => selectorText.Focus()),
                    LabeledStepper("and taking", nameof(TakeEvents), max: 10, onValueChanged: () => selectorText.Focus())).View,
                previewOrErrors);

            (Switch Switch, Grid Wrapper) Toggle(string isToggledPropertyPath)
            {
                (Switch Switch, Grid Wrapper) toggle = Swtch(isToggledPropertyPath);

                toggle.Switch.OnFocusChanged(async (vis, focused) =>
                {
                    if (focused) model.EventSelectorRelatedHasFocus = focused;
                    else
                    {
                        await Task.Delay(300); // to allow for using the skip/take steppers without flickering
                        if (!selectorText.IsFocused) model.EventSelectorRelatedHasFocus = focused;
                    }
                });

                return toggle;
            }
        }

        private VerticalStackLayout OptionalEventFields()
        {
            var evt = model.venue.Event;

            return VStack(0,
                OptionalScrapeJob("‼ Subtitle", evt.SubTitle, nameof(Venue.EventScrapeJob.SubTitle)),
                OptionalScrapeJob("📜 Description", evt.Description, nameof(Venue.EventScrapeJob.Description)),
                OptionalScrapeJob("🎶 Genres", evt.Genres, nameof(Venue.EventScrapeJob.Genres)),
                OptionalScrapeJob("🏛 Stage", evt.Stage, nameof(Venue.EventScrapeJob.Stage)),
                OptionalScrapeJob("🚪 Doors", evt.DoorsTime, nameof(Venue.EventScrapeJob.DoorsTime)),
                OptionalScrapeJob("🎼 Start", evt.StartTime, nameof(Venue.EventScrapeJob.StartTime)),
                OptionalScrapeJob("💳 Pre-sale price", evt.PresalePrice, nameof(Venue.EventScrapeJob.PresalePrice)),
                OptionalScrapeJob("💵 Door price", evt.DoorsPrice, nameof(Venue.EventScrapeJob.DoorsPrice)),
                OptionalScrapeJob("📰 Event page 📡", evt.Url, nameof(Venue.EventScrapeJob.Url), defaultAttribute: "href"),
                OptionalScrapeJob("🖼 Image", evt.ImageUrl, nameof(Venue.EventScrapeJob.ImageUrl), defaultAttribute: "src"),
                OptionalScrapeJob("🎫 Tickets 📡", evt.TicketUrl, nameof(Venue.EventScrapeJob.TicketUrl), defaultAttribute: "href"));

            ScrapeJobEditor.View OptionalScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty, string? defaultAttribute = null)
               => new(model.ScrapeJob(label, scrapeJob, eventProperty, isOptional: true, defaultAttribute), RelativeSelectorEntry);
        }

        private static HorizontalStackLayout LabeledStepper(string label, string valueProperty, int max, Action onValueChanged)
        {
            var stepper = new Stepper() { Minimum = 0, Maximum = max, Increment = 1 }.Bind(Stepper.ValueProperty, valueProperty);
            stepper.ValueChanged += (o, e) => onValueChanged(); // because Un/Focus events aren't firing
            return HStack(5, Lbl(label), BndLbl(valueProperty), stepper);
        }

        private HorizontalStackLayout SelectorEntry(Entry entry, Func<(string selector, bool pickDescendant)> pickRelativeTo)
        {
            Label lbl = Lbl("🖽").ToolTip("🥢 pluck from the page").FontSize(20).CenterVertical()
                .StyleClass(Styles.Label.Clickable)
                .TapGesture(async () =>
                {
                    (string selector, bool pickDescendant) = pickRelativeTo.Invoke();
                    await ShowVisualSelectorForAsync(entry, selector, pickDescendant);
                });

            return HStack(0, entry, lbl.Margins(left: -5));
        }

        private HorizontalStackLayout RelativeSelectorEntry(Entry entry, Func<string?>? maybeGetDescendantOfClosest)
            => SelectorEntry(entry, pickRelativeTo: () =>
            {
                /*  if maybeGetDescendantOfClosest is set, we're selecting the descendant
                 *  and prefer selecting from the Closest expression over the EventSelector */
                bool picksDescendant = maybeGetDescendantOfClosest != null;
                string selector = picksDescendant ? maybeGetDescendantOfClosest!() ?? model.EventSelector : model.EventSelector;
                return (selector, picksDescendant);
            });

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (!model.awaiter.Task.IsCompleted) model.awaiter.SetResult(null); // to signal cancellation
        }
    }
}
