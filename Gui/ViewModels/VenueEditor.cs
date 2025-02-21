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

    [ObservableProperty] private bool showEventSelectorStep;
    [ObservableProperty] private bool showRequiredEventDetailsStep;
    [ObservableProperty] private bool showAdditionalEventDetailsStep;
    [ObservableProperty] private string? previewedEventCount;

    private AngleSharp.Dom.IDocument? programDocument;
    private AngleSharp.Dom.IElement[]? previewedEvents;

    private Venue venue;
    private ScrapeJobEditor eventName, eventDate;

    public string VenueName
    {
        get => venue.Name;
        set
        {
            venue.Name = value;
            OnPropertyChanged(nameof(VenueName));
            RevealMore();
        }
    }

    public string EventSelector
    {
        get => venue.Event.Selector;
        set
        {
            venue.Event.Selector = value;
            OnPropertyChanged(nameof(EventSelector));
            previewedEvents = null;
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

        eventName.IsValidChanged += (o, e) => RevealMore();
        eventDate.IsValidChanged += (o, e) => RevealMore();
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
        bool hasProgramUrl = venue.ProgramUrl.HasSignificantValue();
        bool hasName = VenueName.HasSignificantValue();

        if (hasProgramUrl && programDocument == null)
            programDocument = await scraper.GetDocument(venue);

        if (programDocument != null && !hasName && programDocument.Title.HasSignificantValue())
        {
            VenueName = programDocument.Title!;
            hasName = true;
        }

        ShowEventSelectorStep = hasName && hasProgramUrl;
        ShowRequiredEventDetailsStep = ShowEventSelectorStep && EventSelector.HasSignificantValue();
        ShowAdditionalEventDetailsStep = ShowRequiredEventDetailsStep && eventName.IsValid && eventDate.IsValid;

        if (ShowRequiredEventDetailsStep && programDocument != null && previewedEvents == null)
        {
            previewedEvents = programDocument.SelectEvents(venue).Take(5).ToArray();
            PreviewedEventCount = previewedEvents.Length.ToString();
            scrapeJobEditors.ForEach(e => e.UpdatePreview());
        }
    }

    [RelayCommand]
    private async Task Save()
    {
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
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.ProgramUrl,
                    setter: static (VenueEditor vm, string value) => vm.venue.ProgramUrl = value)
                .OnTextChanged(_ => model.RevealMore());

            var nameEntry = new Entry { Placeholder = "Venue Name" }
                .Bind(Entry.TextProperty, nameof(VenueName));

            var location = new Entry { Placeholder = "Location" }
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            var step1 = new StackLayout { Children = { urlEntry, nameEntry, location } };

            // Step 2: Event Selector
            var eventSelectorEntry = new Entry { Placeholder = "Event Selector" }
                .Bind(Entry.TextProperty, nameof(EventSelector));

            var eventCountPreview = new Label().Bind(Label.TextProperty, getter: static (VenueEditor vm) => vm.PreviewedEventCount);

            var step2 = new StackLayout { Children = { eventSelectorEntry, eventCountPreview } }
                .Bind(IsVisibleProperty, getter: static (VenueEditor vm) => vm.ShowEventSelectorStep);

            // Step 3: Event Details (Name, Date)
            ScrapeJobEditor.View eventName = new(model.eventName);
            ScrapeJobEditor.View eventDate = new(model.eventDate);

            var step3 = new StackLayout { Children = { eventName, eventDate } }
                .Bind(IsVisibleProperty, getter: static (VenueEditor vm) => vm.ShowRequiredEventDetailsStep);

            // Step 4: Additional Event Details
            var evt = model.venue.Event;

            var step4 = new StackLayout
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
            }.Bind(IsVisibleProperty, nameof(ShowAdditionalEventDetailsStep));

            var saveButton = Button("💾 Save this venue", nameof(SaveCommand))
                .Bind(IsVisibleProperty, nameof(ShowAdditionalEventDetailsStep));

            Content = new ScrollView
            {
                Content = new StackLayout
                {
                    Spacing = 20,
                    Padding = 20,
                    Children = {
                        step1, step2, step3, step4, saveButton
                    }
                }
            };

            ScrapeJobEditor.View OptionalScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty, string? defaultAttribute = null)
               => new(model.ScrapeJob(label, scrapeJob, eventProperty, isOptional: true, defaultAttribute));
        }
    }
}
