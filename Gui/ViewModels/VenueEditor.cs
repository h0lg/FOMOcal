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

    [ObservableProperty] private bool showEventContainer;
    [ObservableProperty] private bool showRequiredEventFields;
    [ObservableProperty] private bool showOptionalEventFields;
    [ObservableProperty] private bool eventSelectorHasFocus;
    [ObservableProperty] private bool eventSelectorHasError;
    [ObservableProperty] private string[]? previewedEventTexts;

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
            OnPropertyChanged();
            RevealMore();
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
    }

    private ScrapeJobEditor ScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty,
        bool isOptional = false, string? defaultAttribute = null)
    {
        ScrapeJobEditor editor = new(label, scrapeJob, () => previewedEvents, eventProperty, isOptional, defaultAttribute);
        scrapeJobEditors.Add(editor);
        return editor;
    }

    private async void RevealMore()
    {
        await revealingMore.WaitAsync();
        bool hasProgramUrl = venue.ProgramUrl.IsSignificant();
        bool hasName = VenueName.IsSignificant();

        if (hasProgramUrl && programDocument == null)
            programDocument = await scraper.GetDocumentAsync(venue);

        if (programDocument != null && !hasName && programDocument.Title.IsSignificant())
        {
            VenueName = programDocument.Title!;
            hasName = true;
        }

        ShowEventContainer = hasName && hasProgramUrl;
        ShowRequiredEventFields = ShowEventContainer && EventSelector.IsSignificant();
        ShowOptionalEventFields = ShowRequiredEventFields && eventName.IsValid && eventDate.IsValid;

        if (ShowRequiredEventFields && programDocument != null && previewedEvents == null)
        {
            try
            {
                previewedEvents = programDocument.SelectEvents(venue).Take(5).ToArray();
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

        revealingMore.Release();
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
        bool isConfirmed = await Application.Current!.Windows[0].Page!.DisplayAlert("Confirm Deletion",
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
        public Page(VenueEditor model)
        {
            BindingContext = model;
            Title = model.isDeletable ? "Edit " + model.originalVenueName : "Add a venue";

            // Step 1: Venue Name and Program URL
            var urlEntry = Entr(nameof(ProgramUrl), placeholder: "Program page URL");
            var nameEntry = Entr(nameof(VenueName), placeholder: "Venue name");

            var location = new Entry { Placeholder = "Location, contacts or other helpful info" }
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            var venueFields = VStack(0, urlEntry, nameEntry, location);

            // Step 2: Event Selector
            var selectorText = Entr(nameof(EventSelector), placeholder: "event container selector")
                .OnFocusChanged((_, focused) => model.EventSelectorHasFocus = focused);

            var previewOrErrors = ScrapeJobEditor.View.PreviewOrErrorList(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(EventSelectorHasFocus),
                hasError: nameof(EventSelectorHasError), source: model);

            var eventContainer = Grd(cols: [Auto, Star], rows: [Auto, Auto, Auto, Auto], spacing: 5,
                Lbl("How to dig a gig").FontSize(16).Bold().CenterVertical(),
                Lbl("The CSS selector to the HTML elements containing as many of the event details as possible - but only of a single event.")
                    .BindVisible(nameof(IsFocused), source: selectorText) // display if entry is focused
                    .TextColor(Colors.Yellow).Row(1).ColumnSpan(2),
                Lbl("Event container").Bold().CenterVertical().Row(2), selectorText.Row(2).Column(1),
                previewOrErrors.Row(3).ColumnSpan(2))
                .BindVisible(nameof(ShowEventContainer));

            // Step 3: Event Details (Name, Date)
            var requiredEventFields = VStack(0,
                new ScrapeJobEditor.View(model.eventName),
                new ScrapeJobEditor.View(model.eventDate))
                .BindVisible(nameof(ShowRequiredEventFields));

            // Step 4: Additional Event Details
            var evt = model.venue.Event;

            var optionalEventFields = VStack(0,
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
                OptionalScrapeJob("Tickets", evt.TicketUrl, nameof(Venue.EventScrapeJob.TicketUrl), defaultAttribute: "href"))
                .BindVisible(nameof(ShowOptionalEventFields));

            // form controls
            var saveButton = Btn("💾 Save this venue", nameof(SaveCommand))
                .BindVisible(nameof(ShowOptionalEventFields));

            var deleteButton = Btn("🗑 Delete this venue", nameof(DeleteCommand)).IsVisible(model.isDeletable);

            Content = new ScrollView
            {
                Content = VStack(20,
                    venueFields, eventContainer, requiredEventFields, optionalEventFields, saveButton, deleteButton)
                    .Padding(20)
            };

            ScrapeJobEditor.View OptionalScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty, string? defaultAttribute = null)
               => new(model.ScrapeJob(label, scrapeJob, eventProperty, isOptional: true, defaultAttribute));
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            var awaiter = ((VenueEditor)BindingContext).awaiter;
            if (!awaiter.Task.IsCompleted) awaiter.SetResult(null); // to signal cancellation
        }
    }
}
