using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueEditor : ObservableObject
{
    private readonly bool isDeletable;
    private readonly string? originalVenueName;
    private readonly Scraper scraper;
    private readonly TaskCompletionSource<Result?> awaiter;
    private readonly List<ScrapeJobEditor> scrapeJobEditors = [];
    private readonly SemaphoreSlim revealingMore = new(1, 1);
    private Entry? visualSelectorHost;

    [ObservableProperty] private bool showEventContainer;
    [ObservableProperty] private bool showRequiredEventFields;
    [ObservableProperty] private bool showOptionalEventFields;

    [ObservableProperty] private bool eventSelectorHasFocus;
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

    internal VenueEditor(Venue? venue, Scraper scraper, TaskCompletionSource<Result?> awaiter)
    {
        isDeletable = venue != null;
        originalVenueName = venue?.Name;

        this.venue = venue ?? new Venue
        {
            Name = "",
            ProgramUrl = "",
            Event = new() { Selector = "", Name = new(), Date = new() }
        };

        this.scraper = scraper;
        this.awaiter = awaiter;

        var evt = this.venue.Event;
        eventName = ScrapeJob("Name", evt.Name, nameof(Venue.EventScrapeJob.Name));
        eventDate = ScrapeJob("Date", evt.Date, nameof(Venue.EventScrapeJob.Date));
        eventName.IsValidChanged += (_, _) => RevealMore();
        eventDate.IsValidChanged += (_, _) => RevealMore();

        PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(SkipEvents) || e.PropertyName == nameof(TakeEvents))
                UpdateEventSelectorPreview();
        };
    }

    private ScrapeJobEditor ScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty,
        bool isOptional = false, string? defaultAttribute = null)
    {
        ScrapeJobEditor editor = new(label, scrapeJob,
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

        awaiter.SetResult(new Result(venue, Result.Actions.Saved, originalVenueName));
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        bool isConfirmed = await App.CurrentPage.DisplayAlert("Confirm Deletion",
            $"Are you sure you want to delete the venue {venue.Name}?",
            "Yes", "No");

        if (isConfirmed) awaiter.SetResult(new Result(venue, Result.Actions.Deleted, originalVenueName));
    }

    internal record Result
    {
        public Venue Venue { get; }
        public Actions Action { get; }
        public string? OriginalVenueName { get; }

        internal Result(Venue venue, Actions action, string? originalVenueName)
        {
            Venue = venue;
            Action = action;
            OriginalVenueName = originalVenueName;
        }

        internal enum Actions { Saved, Deleted }
    }

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

        private static VerticalStackLayout VenueFields()
        {
            var urlEntry = Entr(nameof(ProgramUrl), placeholder: "Program page URL");
            var nameEntry = Entr(nameof(VenueName), placeholder: "Venue name");

            var location = new Entry { Placeholder = "Location, contacts or other helpful info" }
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            return VStack(0, urlEntry, nameEntry, location);
        }

        private VerticalStackLayout EventContainerSelector()
        {
            Action<VisualElement, bool> setEventSelectorRelatedFocused = (vis, focused) => model.EventSelectorRelatedHasFocus = focused;

            var selectorText = Entr(nameof(EventSelector), placeholder: "event container selector")
                .OnFocusChanged((vis, focused) =>
                {
                    setEventSelectorRelatedFocused(vis, focused);

                    /*  Only propagate the loss of focus to the property
                        if entry has not currently opened the visualSelector
                        to keep the help visible while working there */
                    if (focused || model.visualSelectorHost != vis)
                        model.EventSelectorHasFocus = focused;
                });

            var containerSelector = SelectorEntry(selectorText, pickRelativeTo: () => (selector: "body", pickDescendant: true));
            (Switch Switch, Grid Wrapper) waitForJsRendering = Swtch(nameof(WaitForJsRendering));
            waitForJsRendering.Switch.OnFocusChanged(setEventSelectorRelatedFocused);

            var previewOrErrors = ScrapeJobEditor.View.PreviewOrErrorList(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(EventSelectorRelatedHasFocus),
                hasError: nameof(EventSelectorHasError), source: model);

            return VStack(spacing: 5,
                Lbl("How to dig a gig").FontSize(16).Bold().CenterVertical(),
                Lbl("The CSS selector to the event containers - of which there are probably multiple on the page," +
                    " each containing as many of the event details as possible - but only of a single event." +
                    " If you see that the event page groups multiple events on the same day into a group, try it out on those" +
                    " and choose a container that contains only one of their details." +
                    " You'll be able to select the date or other excluded event details from outside the container later.")
                    .BindVisible(nameof(EventSelectorHasFocus)) // display if entry is focused
                    .TextColor(Colors.Yellow),
                Lbl("You may want to try this option if your event selector doesn't match anything without it." +
                    " It will load the page and wait for an element matching your selector to become available," +
                    " return when it does and time out if it doesn't after 10s. This works around pages that lazy-load events." +
                    " Some web servers only return an empty template of a page on the first request to improve the response time," +
                    " then fetch more data asynchronously and render it into the placeholders using a script running in your browser.")
                    .BindVisible(nameof(IsFocused), source: waitForJsRendering.Switch) // display if checkbox is focused
                    .TextColor(Colors.Yellow),
                HWrap(5,
                    Lbl("Event container").Bold(), containerSelector,
                    Lbl("wait for JS rendering"), waitForJsRendering.Wrapper,
                    Lbl("Preview events").Bold(),
                    LabeledStepper("skipping", nameof(SkipEvents), max: 100),
                    LabeledStepper("and taking", nameof(TakeEvents), max: 10)),
                previewOrErrors);
        }

        private VerticalStackLayout OptionalEventFields()
        {
            var evt = model.venue.Event;

            return VStack(0,
                OptionalScrapeJob("Subtitle", evt.SubTitle, nameof(Venue.EventScrapeJob.SubTitle)),
                OptionalScrapeJob("Description", evt.Description, nameof(Venue.EventScrapeJob.Description)),
                OptionalScrapeJob("Genres", evt.Genres, nameof(Venue.EventScrapeJob.Genres)),
                OptionalScrapeJob("Stage", evt.Stage, nameof(Venue.EventScrapeJob.Stage)),
                OptionalScrapeJob("Doors", evt.DoorsTime, nameof(Venue.EventScrapeJob.DoorsTime)),
                OptionalScrapeJob("Start", evt.StartTime, nameof(Venue.EventScrapeJob.StartTime)),
                OptionalScrapeJob("Pre-sale price", evt.PresalePrice, nameof(Venue.EventScrapeJob.PresalePrice)),
                OptionalScrapeJob("Door price", evt.DoorsPrice, nameof(Venue.EventScrapeJob.DoorsPrice)),
                OptionalScrapeJob("Event page", evt.Url, nameof(Venue.EventScrapeJob.Url), defaultAttribute: "href"),
                OptionalScrapeJob("Image", evt.ImageUrl, nameof(Venue.EventScrapeJob.ImageUrl), defaultAttribute: "src"),
                OptionalScrapeJob("Tickets", evt.TicketUrl, nameof(Venue.EventScrapeJob.TicketUrl), defaultAttribute: "href"));

            ScrapeJobEditor.View OptionalScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty, string? defaultAttribute = null)
               => new(model.ScrapeJob(label, scrapeJob, eventProperty, isOptional: true, defaultAttribute), RelativeSelectorEntry);
        }

        private static HorizontalStackLayout LabeledStepper(string label, string valueProperty, int max)
        {
            Stepper stepper = new() { Minimum = 0, Maximum = max, Increment = 1 };
            stepper.Bind(Stepper.ValueProperty, valueProperty);
            var display = BndLbl(nameof(Stepper.Value), source: stepper);
            return HStack(5, Lbl(label), stepper, display);
        }

        private HorizontalStackLayout SelectorEntry(Entry entry, Func<(string selector, bool pickDescendant)> pickRelativeTo)
        {
            Label lbl = Lbl("🖽").ToolTip("🥢 pluck from the page").FontSize(20).CenterVertical()
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
