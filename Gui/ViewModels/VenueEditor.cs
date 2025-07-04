﻿using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Maui.Markup.LeftToRight;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FomoCal.Gui.Resources;
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
    private readonly Debouncer debouncedRevealMore;
    private Entry? visualSelectorHost;

    [ObservableProperty] public partial bool IsEventPageLoading { get; set; } = true;
    [ObservableProperty] public partial bool ShowEventContainer { get; set; }
    [ObservableProperty] public partial bool ShowRequiredEventFields { get; set; }
    [ObservableProperty] public partial bool ShowOptionalEventFields { get; set; }

    [ObservableProperty] public partial bool EventSelectorRelatedHasFocus { get; set; } // for when related controls have focus
    [ObservableProperty] public partial bool EventSelectorHasError { get; set; }
    [ObservableProperty] public partial string[]? PreviewedEventTexts { get; set; }
    [ObservableProperty] public partial int SkipEvents { get; set; }
    [ObservableProperty] public partial int TakeEvents { get; set; } = 5;

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

    public string? Encoding
    {
        get => venue.Encoding;
        set
        {
            if (value == venue.Encoding) return;
            venue.Encoding = value;
            OnPropertyChanged();
        }
    }

    public string? Comment
    {
        get => venue.Event.Comment;
        set
        {
            if (value == venue.Event.Comment) return;
            venue.Event.Comment = value;
            OnPropertyChanged();
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

        debouncedRevealMore = new(TimeSpan.FromMilliseconds(100), UndebouncedRevealMore,
            async ex => await ErrorReport.WriteAsyncAndShare(ex.ToString(), "revealing more of the venue editor"));

        var evt = this.venue.Event;
        eventName = ScrapeJob("❗ Name", evt.Name, nameof(Venue.EventScrapeJob.Name));
        eventDate = ScrapeJob("📆 Date", evt.Date, nameof(Venue.EventScrapeJob.Date));
        eventName.IsValidAsRequiredChanged += (_, _) => RevealMore();
        eventDate.IsValidAsRequiredChanged += (_, _) => RevealMore();

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

    private void RevealMore() => debouncedRevealMore.Run();

    private void UndebouncedRevealMore()
    {
        bool hasProgramUrl = venue.ProgramUrl.IsSignificant();
        bool hasName = VenueName.IsSignificant();

        if (programDocument != null && !hasName && programDocument.Title.IsSignificant())
        {
            VenueName = programDocument.Title!;
            hasName = true;
        }

        ShowEventContainer = hasName && hasProgramUrl;
        ShowRequiredEventFields = ShowEventContainer && EventSelector.IsSignificant();
        ShowOptionalEventFields = ShowRequiredEventFields && eventName.IsValidAsRequired && eventDate.IsValidAsRequired;
        if (ShowRequiredEventFields && previewedEvents == null) UpdateEventSelectorPreview();
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
            previewedEvents = [.. programDocument.SelectEvents(venue).Skip(SkipEvents).Take(TakeEvents)];
            PreviewedEventTexts = [.. previewedEvents.Select(e => e.TextContent.NormalizeWhitespace())];
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
    private static async Task OpenUrlAsync(string url)
        => await WebViewPage.OpenUrlAsync(url);

    [RelayCommand]
    private void Save()
    {
        // reset empty optional scrape jobs
        foreach (var editor in scrapeJobEditors.Where(e => e.IsOptional))
        {
            var property = typeof(Venue.EventScrapeJob).GetProperty(editor.EventProperty)!;
            property.SetValue(venue.Event, editor.IsEmpty ? null : editor.ScrapeJob);
        }

        foreach (var editor in scrapeJobEditors.WithValue())
            editor.ResetInsignificantValues(); // to reduce noise in serialized JSON

        SetActionTaken(Actions.Saved);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        bool isConfirmed = await App.CurrentPage.DisplayAlert("Confirm Deletion",
            $"Are you sure you want to delete the venue {venue.Name}?",
            "Yes", "No");

        if (isConfirmed) SetActionTaken(Actions.Deleted);
    }

    private void SetActionTaken(Actions? action)
    {
        if (!awaiter.Task.IsCompleted) awaiter.SetResult(action);
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
                new ScrapeJobEditor.View(model.eventName, RelativeSelectorEntry, () => model.visualSelectorHost),
                new ScrapeJobEditor.View(model.eventDate, RelativeSelectorEntry, () => model.visualSelectorHost))
                .BindVisible(nameof(ShowRequiredEventFields));

            // Step 4: Additional Event Details
            const string showOptionalEventFields = nameof(ShowOptionalEventFields);
            var optionalEventFields = OptionalEventFields().BindVisible(showOptionalEventFields);

            var formControls = HStack(10,
                Btn("💾 Save", nameof(SaveCommand)).BindVisible(showOptionalEventFields),
                Lbl("or").BindVisible(showOptionalEventFields),
                Btn("🗑 Delete", nameof(DeleteCommand)).IsVisible(model.isDeletable),
                Lbl("this venue")).Right();

            form = new ScrollView
            {
                Content = VStack(20,
                    //progress,
                    venueFields, eventContainer, requiredEventFields, optionalEventFields, formControls)
                    .Padding(20)
            };

            visualSelector = CreateVisualSelector();
            Content = Grd(cols: [Star], rows: [Star, Auto], spacing: 0, form, visualSelector.Row(1));
        }

        private Grid VenueFields()
        {
            const string programUrl = nameof(ProgramUrl);
            var urlEntry = Entr(programUrl, placeholder: "Program page URL");
            var nameEntry = Entr(nameof(VenueName), placeholder: "Venue name");
            var encoding = Entr(nameof(Encoding), placeholder: "encoding override").ToolTip(HelpTexts.Encoding);
            var comment = Entr(nameof(Comment), placeholder: "explain this config or something about it").ToolTip(HelpTexts.Comment);

            var location = new Entry { Placeholder = "Location, contacts or other helpful info" }
                .Bind(Entry.TextProperty,
                    getter: static vm => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            var loadingIndicator = new ActivityIndicator { IsRunning = true }
                .BindVisible(new Binding(programUrl, converter: Converters.IsSignificant),
                    Converters.And, new Binding(nameof(IsEventPageLoading)));

            var reload = Btn("⟳").TapGesture(Reload)
                .BindVisible(new Binding(programUrl, converter: Converters.IsSignificant),
                    Converters.And, new Binding(nameof(IsEventPageLoading), converter: Converters.Not));

            var openUrl = Btn("📡", nameof(OpenUrlCommand), source: model, parameterPath: programUrl)
                .BindIsVisibleToValueOf(programUrl);

            return Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Auto, Auto, Auto, Auto], spacing: 5,
                FldLbl("🕸"), urlEntry.Column(1), loadingIndicator.Column(2), reload.Column(2), openUrl.Column(3),
                FldLbl("🏷").Row(1), nameEntry.Row(1).Column(1).ColumnSpan(3),
                FldLbl("📍").Row(2), location.Row(2).Column(1).ColumnSpan(3),
                FldLbl("🔣").Row(3), encoding.Row(3).Column(1).ColumnSpan(3),
                FldLbl("💬").Row(4), comment.Row(4).Column(1).ColumnSpan(3));

            static Label FldLbl(string Text) => Lbl(Text).CenterVertical();
        }

        private Grid EventContainerSelector()
        {
            var help = HelpLabel();
            Label scrapeConfigInfo = Lbl("ⓘ");
            string scrapeConfigInfoText = string.Format(HelpTexts.ScrapeConfigInfoFormat, AppInfo.Name);

            scrapeConfigInfo.TapGesture(async () =>
                await help.InlineHelpTextAsync(scrapeConfigInfoText, host: scrapeConfigInfo,
                    focused: help.label.BindingContext != scrapeConfigInfo)); // close help if already opened

            var selectorText = Entr(nameof(EventSelector), placeholder: "event container selector");

            selectorText.InlineTooltipOnFocus(HelpTexts.EventContainerSelector, help, async (_, focused) =>
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
            waitForJsRendering.Switch.InlineTooltipOnFocus(HelpTexts.WaitForJsRendering, help);

            Picker pagingStrategy = new()
            {
                ItemsSource = model.PagingStrategies.ConvertAll(e => e.GetDescription()),
                SelectedIndex = model.PagingStrategies.IndexOf(model.venue.Event.PagingStrategy)
            };

            pagingStrategy.OnFocusChanged(async (_, focused) => await SyncPagingStrategyHelp(focused));

            pagingStrategy.SelectedIndexChanged += async (s, e) =>
            {
                if (pagingStrategy.SelectedIndex >= 0)
                {
                    model.venue.Event.PagingStrategy = model.PagingStrategies[pagingStrategy.SelectedIndex];
                    await SyncPagingStrategyHelp(focused: true);
                }
                else await SyncPagingStrategyHelp(focused: false);
            };

            var nextPageSelector = SelectorEntry(
                Entr(nameof(NextEventPageSelector)).Placeholder("next page")
                    .InlineTooltipOnFocus(HelpTexts.NextEventPageSelector, help,
                        cancelFocusChanged: (vis, focused) => !focused && model.visualSelectorHost == vis),
                pickRelativeTo: () => (selector: "body", pickDescendant: true))
                .BindVisible(nameof(Picker.SelectedIndex), pagingStrategy,
                    Converters.Func<int>(i => model.PagingStrategies[i].RequiresNextPageSelector()));

            var previewOrErrors = ScrapeJobEditor.View.PreviewOrErrorList(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(EventSelectorRelatedHasFocus),
                hasError: nameof(EventSelectorHasError), source: model);

            var controls = HWrap(5,
                Lbl("Event container").Bold(), containerSelector,
                Lbl("wait for JS rendering"), waitForJsRendering.Wrapper,
                Lbl("loading"), pagingStrategy,
                nextPageSelector,
                Lbl("Preview events").Bold(),
                // focus selectorText as the closest match to display the help text of while keeping the previewOrErrors open
                LabeledStepper("skipping", nameof(SkipEvents), max: 100, onValueChanged: () => selectorText.Focus()),
                LabeledStepper("and taking", nameof(TakeEvents), max: 10, onValueChanged: () => selectorText.Focus()));

            return Grd(cols: [Auto, Star], rows: [Auto, Auto, Auto, Auto], spacing: 5,
                Lbl("How to dig a gig").StyleClass(Styles.Label.SubHeadline),
                scrapeConfigInfo.CenterVertical().Column(1),
                help.layout.Row(1).ColumnSpan(2),
                controls.View.Row(2).ColumnSpan(2),
                previewOrErrors.Row(3).ColumnSpan(2));

            (Switch Switch, Grid Wrapper) Toggle(string isToggledPropertyPath)
            {
                (Switch Switch, Grid Wrapper) toggle = Swtch(isToggledPropertyPath);

                toggle.Switch.OnFocusChanged(async (_, focused) =>
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

            Task SyncPagingStrategyHelp(bool focused) =>
                help.InlineHelpTextAsync(model.venue.Event.PagingStrategy.GetHelp()!, pagingStrategy, focused);
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
               => new(model.ScrapeJob(label, scrapeJob, eventProperty, isOptional: true, defaultAttribute),
                    RelativeSelectorEntry, () => model.visualSelectorHost);
        }

        private static HorizontalStackLayout LabeledStepper(string label, string valueProperty, int max, Action onValueChanged)
        {
            var stepper = new Stepper() { Minimum = 0, Maximum = max, Increment = 1 }.Bind(Stepper.ValueProperty, valueProperty);
            stepper.ValueChanged += (o, e) => onValueChanged(); // because Un/Focus events aren't firing
            return HStack(5, Lbl(label), BndLbl(valueProperty), stepper);
        }

        private HorizontalStackLayout SelectorEntry(Entry entry, Func<(string selector, bool pickDescendant)> pickRelativeTo)
        {
            Border layout = new()
            {
                StyleClass = ["showVisualSelector"],
                Content = Lbl("🖽").StyleClass("showVisualSelectorLabel")
            };

            layout.ToolTip("🥢 pluck from the page").TapGesture(async () =>
            {
                (string selector, bool pickDescendant) = pickRelativeTo.Invoke();
                await ShowVisualSelectorForAsync(entry, selector, pickDescendant);
            });

            return HStack(0, entry, layout);
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

        protected override bool OnBackButtonPressed()
        {
            model.SetActionTaken(null); // to signal cancellation
            return base.OnBackButtonPressed();
        }
    }
}
