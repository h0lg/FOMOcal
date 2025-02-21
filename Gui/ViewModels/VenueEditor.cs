using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueEditor : ObservableObject
{
    private readonly Scraper scraper;
    private readonly JsonFileRepository<Venue> venueRepo;
    private readonly List<ScrapeJobEditor> scrapeJobEditors = [];
    private readonly SemaphoreSlim revealingMore = new(1, 1);

    [ObservableProperty] private bool showEventContainer;
    [ObservableProperty] private bool showRequiredEventFields;
    [ObservableProperty] private bool showOptionalEventFields;
    [ObservableProperty] private string? previewedEventCount;

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

    public VenueEditor(Venue venue, Scraper scraper, JsonFileRepository<Venue> venueRepo)
    {
        this.venue = venue;
        this.scraper = scraper;
        this.venueRepo = venueRepo;

        var evt = venue.Event;
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
            previewedEvents = programDocument.SelectEvents(venue).Take(5).ToArray();
            PreviewedEventCount = previewedEvents.Length.ToString();
            scrapeJobEditors.ForEach(e => e.UpdatePreview());
        }

        revealingMore.Release();
    }

    [RelayCommand]
    private async Task Save()
    {
        // reset empty optional scrape jobs
        foreach (var editor in scrapeJobEditors.Where(e => e.IsOptional))
        {
            var property = typeof(Venue.EventScrapeJob).GetProperty(editor.EventProperty)!;
            property.SetValue(venue.Event, editor.IsEmpty ? null : editor.ScrapeJob);
        }

        var venues = await venueRepo.LoadAllAsync();
        if (venues.Contains(venue)) venues.Remove(venue);
        venues.Add(venue);
        await venueRepo.SaveAllAsync(venues);
    }

    public partial class Page : ContentPage
    {
        public Page(VenueEditor model)
        {
            BindingContext = model;

            // Step 1: Venue Name and Program URL
            var urlEntry = new Entry { Placeholder = "Program URL" }
                .Bind(Entry.TextProperty, nameof(ProgramUrl));

            var nameEntry = new Entry { Placeholder = "Venue Name" }
                .Bind(Entry.TextProperty, nameof(VenueName));

            var location = new Entry { Placeholder = "Location" }
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            var venueFields = new StackLayout { Children = { urlEntry, nameEntry, location } };

            // Step 2: Event Selector
            var selectorText = new Entry { Placeholder = "Event Selector" }
                .Bind(Entry.TextProperty, nameof(EventSelector));

            var eventCountPreview = new Label().Bind(Label.TextProperty, nameof(PreviewedEventCount));

            var eventContainer = new StackLayout { Children = { selectorText, eventCountPreview } }
                .BindVisible(nameof(ShowEventContainer));

            // Step 3: Event Details (Name, Date)
            ScrapeJobEditor.View eventName = new(model.eventName);
            ScrapeJobEditor.View eventDate = new(model.eventDate);

            var requiredEventFields = new StackLayout { Children = { eventName, eventDate } }
                .BindVisible(nameof(ShowRequiredEventFields));

            // Step 4: Additional Event Details
            var evt = model.venue.Event;

            var optionalEventFields = new StackLayout
            {
                Children = {
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
                    OptionalScrapeJob("Tickets", evt.TicketUrl, nameof(Venue.EventScrapeJob.TicketUrl), defaultAttribute: "href")
                }
            }.BindVisible(nameof(ShowOptionalEventFields));

            // form controls
            var saveButton = Btn("💾 Save this venue", nameof(SaveCommand))
                .BindVisible(nameof(ShowOptionalEventFields));

            Content = new ScrollView
            {
                Content = new StackLayout
                {
                    Spacing = 20,
                    Padding = 20,
                    Children = {
                        venueFields, eventContainer, requiredEventFields, optionalEventFields, saveButton
                    }
                }
            };

            ScrapeJobEditor.View OptionalScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty, string? defaultAttribute = null)
               => new(model.ScrapeJob(label, scrapeJob, eventProperty, isOptional: true, defaultAttribute));
        }
    }
}
