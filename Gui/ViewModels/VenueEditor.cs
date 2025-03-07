﻿using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PuppeteerSharp;
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

    [ObservableProperty] private bool showEventContainer;
    [ObservableProperty] private bool showRequiredEventFields;
    [ObservableProperty] private bool showOptionalEventFields;
    [ObservableProperty] private bool eventSelectorHasFocus;
    [ObservableProperty] private bool eventSelectorHasError;
    [ObservableProperty] private string[]? previewedEventTexts;

    private IPage? programPage;
    private IElementHandle[]? previewedEvents;

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

        if (hasProgramUrl && programPage == null)
            programPage = await scraper.GetProgramPageAsync(venue);

        if (programPage != null)
        {
            string title = await programPage!.GetTitleAsync();

            if (!hasName && title.HasSignificantValue())
            {
                VenueName = title;
                hasName = true;
            }
        }

        ShowEventContainer = hasName && hasProgramUrl;
        ShowRequiredEventFields = ShowEventContainer && EventSelector.HasSignificantValue();
        ShowOptionalEventFields = ShowRequiredEventFields && eventName.IsValid && eventDate.IsValid;

        if (ShowRequiredEventFields && programPage != null && previewedEvents == null)
        {
            try
            {
                previewedEvents = (await programPage.SelectEventsAsync(venue)).Take(5).ToArray();
                Task<string>[] previewing = previewedEvents.Select(async e => (await e.GetTextContentAsync()).NormalizeWhitespace()).ToArray();
                await Task.WhenAll(previewing);
                PreviewedEventTexts = previewing.Select(t => t.Result).ToArray();
                scrapeJobEditors.ForEach(async e => await e.UpdatePreviewAsync());
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
            Title = model.isDeletable ? "Edit venue" : "Add a venue";

            // Step 1: Venue Name and Program URL
            var urlEntry = new Entry { Placeholder = "Program page URL" }
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.ProgramUrl,
                    setter: static (VenueEditor vm, string value) => vm.venue.ProgramUrl = value)
                .OnTextChanged(_ => model.RevealMore());

            var nameEntry = new Entry { Placeholder = "Venue name" }
                .Bind(Entry.TextProperty, nameof(VenueName));

            var location = new Entry { Placeholder = "Location, contacts or other helpful info" }
                .Bind(Entry.TextProperty,
                    getter: static (VenueEditor vm) => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            var venueFields = new StackLayout { Children = { urlEntry, nameEntry, location } };

            // Step 2: Event Selector
            var eventSelectorEntry = new Entry { Placeholder = "event container selector" }
                .Bind(Entry.TextProperty, nameof(EventSelector))
                .OnFocusChanged((_, focused) => model.EventSelectorHasFocus = focused);

            var previewedEventTexts = ScrapeJobEditor.View.ScrapeJobPreviewOrErrorList(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(EventSelectorHasFocus),
                hasError: nameof(EventSelectorHasError), source: model);

            var eventContainer = new Grid
            {
                ColumnSpacing = 5,
                RowSpacing = 5,
                ColumnDefinitions = Columns.Define(Auto, Star),
                RowDefinitions = Rows.Define(Auto, Auto, Auto, Auto),
                Children = {
                    new Label().Text("How to dig a gig").FontSize(16).Bold().CenterVertical(),
                    new Label().Text("The CSS selector to the HTML elements containing the details for one event each.")
                        .Bind(IsVisibleProperty, nameof(IsFocused), source: eventSelectorEntry) // display if entry is focused
                        .TextColor(Colors.Yellow).Row(1).ColumnSpan(2),
                    new Label().Text("Event container").Bold().CenterVertical().Row(2), eventSelectorEntry.Row(2).Column(1),
                    previewedEventTexts.Row(3).ColumnSpan(2) }
            }
                .Bind(IsVisibleProperty, getter: static (VenueEditor vm) => vm.ShowEventContainer);

            // Step 3: Event Details (Name, Date)
            var requiredEventFields = new StackLayout
            {
                Children = {
                    new ScrapeJobEditor.View(model.eventName),
                    new ScrapeJobEditor.View(model.eventDate) }
            }
                .Bind(IsVisibleProperty, getter: static (VenueEditor vm) => vm.ShowRequiredEventFields);

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
            }.Bind(IsVisibleProperty, nameof(ShowOptionalEventFields));

            var saveButton = Button("💾 Save this venue", nameof(SaveCommand))
                .Bind(IsVisibleProperty, nameof(ShowOptionalEventFields));

            var deleteButton = Button("🗑 Delete this venue", nameof(DeleteCommand))
                .IsVisible(model.isDeletable);

            Content = new ScrollView
            {
                Content = new StackLayout
                {
                    Spacing = 20,
                    Padding = 20,
                    Children = {
                        venueFields, eventContainer, requiredEventFields, optionalEventFields, saveButton, deleteButton
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
