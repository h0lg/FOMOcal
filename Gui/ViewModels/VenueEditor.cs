using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueEditor : ObservableObject
{
    private readonly Scraper scraper;
    private readonly TaskCompletionSource<Result?> awaiter;
    private readonly List<ScrapeJobEditor> scrapeJobEditors = [];

    [ObservableProperty] private bool showEventSelectorStep;
    [ObservableProperty] private bool showRequiredEventDetailsStep;
    [ObservableProperty] private bool showAdditionalEventDetailsStep;
    [ObservableProperty] private bool eventSelectorHasFocus;
    [ObservableProperty] private bool eventSelectorHasError;
    [ObservableProperty] private string[]? previewedEventTexts;

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

    internal VenueEditor(Venue? venue, Scraper scraper, TaskCompletionSource<Result?> awaiter)
    {
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
            try
            {
                previewedEvents = programDocument.SelectEvents(venue).Take(5).ToArray();
                PreviewedEventTexts = previewedEvents.Select(e => e.TextContent.NormalizeWhitespace()).ToArray();
                scrapeJobEditors.ForEach(e => e.UpdatePreview());
                EventSelectorHasError = false;
            }
            catch (Exception ex)
            {
                previewedEvents = [];
                PreviewedEventTexts = [ex.Message];
                EventSelectorHasError = true;
            }
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

        awaiter.SetResult(new Result() { Action = Result.Actions.Saved, Venue = venue });
    }

    internal record Result
    {
        public required Actions Action { get; set; }
        public required Venue Venue { get; set; }
        internal enum Actions { Saved }
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
            var eventSelectorEntry = new Entry { Placeholder = "event container selector" }
                .Bind(Entry.TextProperty, nameof(EventSelector))
                .OnFocusChanged((_, focused) => model.EventSelectorHasFocus = focused);

            var previewedEventTexts = ScrapeJobEditor.View.ScrapeJobPreviewOrErrorList(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(EventSelectorHasFocus),
                hasError: nameof(EventSelectorHasError), source: model);

            var step2 = new StackLayout { Children = { eventSelectorEntry, previewedEventTexts } }
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            var awaiter = ((VenueEditor)BindingContext).awaiter;
            if (!awaiter.Task.IsCompleted) awaiter.SetResult(null); // to signal cancellation
        }
    }
}
