using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
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
    private readonly Scraper scraper; // singleton, disposed of by the service provider
    private readonly TaskCompletionSource<Actions?> awaiter;
    private readonly List<ScrapeJobEditor> scrapeJobEditors = [];
    private readonly Debouncer debouncedRevealMore;
    private readonly Venue venue;
    private readonly ScrapeJobEditor eventName, eventDate;

    private Entry? visualSelectorHost;
    private IDomDocument? programDocument;
    private IDomElement[]? previewedEvents;

    [ObservableProperty] public partial bool IsEventPageLoading { get; set; } = true;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SaveCommand))] public partial bool HasRequiredInfo { get; set; }
    [ObservableProperty] public partial bool ShowRequiredEventFields { get; set; }
    [ObservableProperty] public partial bool ShowOptionalEventFields { get; set; }
    [ObservableProperty] public partial double Progress { get; set; } = 0;

    [ObservableProperty] public partial bool PreviewRelatedHasFocus { get; set; } // for when related controls have focus
    [ObservableProperty] public partial bool EventSelectorHasError { get; set; }
    [ObservableProperty] public partial string[]? PreviewedEventTexts { get; set; }
    [ObservableProperty] public partial ushort SkipEvents { get; set; }
    [ObservableProperty] public partial ushort TakeEvents { get; set; } = 5;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))] public partial int SelectedEventCount { get; set; } = 0;
    [ObservableProperty] public partial int FilteredEventCount { get; set; } = 0;

    public string ProgramUrl
    {
        get => venue.ProgramUrl;
        set
        {
            if (value == venue.ProgramUrl) return;
            venue.ProgramUrl = value;
            SetDocument(null);
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

    public bool SaveScrapeLogs
    {
        get => venue.SaveScrapeLogs;
        set
        {
            if (value == venue.SaveScrapeLogs) return;
            venue.SaveScrapeLogs = value;
            OnPropertyChanged();
        }
    }

    public int? LastEventCount => venue.LastEventCount;
    public DateTime? LastRefreshed => venue.LastRefreshed;

    public ObservableCollection<ScrapeLogFile.ForVenue> ScrapeLogs { get; }

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

        // only load scrape logs if ProgramUrl is set; use isDeletable as indicator
        ScrapeLogs = new(isDeletable ? ScrapeLogFile.GetAll(venue): []);

        PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(SkipEvents)
                || e.PropertyName == nameof(TakeEvents)
                || e.PropertyName == nameof(EventFilter))
                UpdateEventContainerPreview();
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

        HasRequiredInfo = hasName && hasProgramUrl;
        ShowRequiredEventFields = HasRequiredInfo && EventSelector.IsSignificant();
        ShowOptionalEventFields = ShowRequiredEventFields && eventName.IsValidAsRequired && eventDate.IsValidAsRequired;
        Progress = (ShowOptionalEventFields ? 3 : ShowRequiredEventFields ? 2 : HasRequiredInfo ? 1 : 0) / 3d;
        if (ShowRequiredEventFields && previewedEvents == null) UpdateEventContainerPreview();
    }

    private void UpdateEventContainerPreview()
    {
        if (programDocument == null)
        {
            PreviewedEventTexts = null;
            EventSelectorHasError = false;
            return;
        }

        try
        {
            var selectedEvents = programDocument.SelectEvents(venue).ToArray();
            var filtered = selectedEvents.FilterEvents(venue).ToArray();
            SelectedEventCount = selectedEvents.Length;
            FilteredEventCount = filtered.Length;
            previewedEvents = [.. filtered.Skip(SkipEvents).Take(TakeEvents)];
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

    private void SetDocument(IDomDocument? document)
    {
        programDocument?.Dispose();
        programDocument = document;
        previewedEvents = null;
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private static async Task OpenUrlAsync(string url)
        => await WebViewPage.OpenUrlAsync(url);

    [RelayCommand]
    private static async Task OpenFileAsync(string path) => await FileHelper.OpenFileAsync(path);

    [RelayCommand]
    private void DeleteScrapeLog(ScrapeLogFile.ForVenue log)
    {
        File.Delete(log.Path);
        ScrapeLogs!.Remove(log);
    }

    private bool CanLoadMore()
        => SelectedEventCount > 0
            && venue.Event.PagingStrategy != Venue.PagingStrategy.AllOnOne
            && programDocument?.CanLoadMore(venue) == true;

    [RelayCommand(CanExecute = nameof(CanLoadMore))]
    private async Task LoadMoreAsync(AutomatedEventPageView loader)
        => await scraper.LoadMoreAsync(loader, venue, programDocument!);

    private bool CanSave() => HasRequiredInfo;

    [RelayCommand(CanExecute = nameof(CanSave))]
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
        bool isConfirmed = await App.CurrentPage.DisplayAlertAsync("Confirm Deletion",
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

            /* CreateVisualSelector before EventContainer because the pageView 
             * created by the former is referenced as a command arg in the latter */
            visualSelector = CreateVisualSelector();

            // Step 1: Venue Name and Program URL
            var venueFields = VenueFields();

            // Step 2: Event container
            var eventContainer = EventContainer().BindVisible(nameof(HasRequiredInfo));

            // Step 3: Event Details (Name, Date)
            var requiredEventFields = VStack(0,
                new ScrapeJobEditor.View(model.eventName, RelativeSelectorEntry, () => model.visualSelectorHost),
                new ScrapeJobEditor.View(model.eventDate, RelativeSelectorEntry, () => model.visualSelectorHost))
                .BindVisible(nameof(ShowRequiredEventFields));

            // Step 4: Additional Event Details
            const string showOptionalEventFields = nameof(ShowOptionalEventFields);
            var optionalEventFields = OptionalEventFields().BindVisible(showOptionalEventFields);

            // Progress Indicator
            var progress = new ProgressBar().Bind(ProgressBar.ProgressProperty, nameof(Progress))
                .ToolTip("your progress towards the minimum required configuration to make this venue scrapable");

            var formControls = Grd(cols: [Star, Auto, Auto, Auto, Auto], rows: [Auto], spacing: 10,
                progress,
                Btn("💾 Save", nameof(SaveCommand)).Column(1),
                Lbl("or").CenterVertical().IsVisible(model.isDeletable).Column(2),
                Btn("🗑 Delete", nameof(DeleteCommand)).IsVisible(model.isDeletable).Column(3),
                Lbl("this venue").CenterVertical().Column(4));

            form = new ScrollView
            {
                Content = VStack(20, venueFields, eventContainer,
                    requiredEventFields, optionalEventFields, ScrapeLogs(model), formControls)
                    .Padding(20)
            };

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

            var selectorText = Entr(nameof(EventSelector), placeholder: "event container selector");

            var containerSelector = SelectorEntry(selectorText, pickRelativeTo: () => (selector: "body", pickDescendant: true));
            (Switch Switch, Grid Wrapper) lazyLoaded = Swtch(nameof(LazyLoaded));

            var eventFilter = Entr(nameof(EventFilter), placeholder: "text or XPath");

            var previewOrErrors = ScrapeJobEditor.View.PreviewOrErrorList(
                itemsSource: nameof(PreviewedEventTexts), hasFocus: nameof(PreviewRelatedHasFocus),
                hasError: nameof(EventSelectorHasError), source: model);

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
                cancelFocusChanged: (vis, focused) => !focused && model.visualSelectorHost == vis);

            eventFilter.InlineTooltipOnFocus(string.Format(HelpTexts.EventContainerFilterFormat, FomoCal.ScrapeJob.XPathSelectorPrefix),
                help, onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            lazyLoaded.Switch.InlineTooltipOnFocus(HelpTexts.LazyLoaded, help,
                onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            skip.Entry.InlineTooltipOnFocus("how many selected events to skip for the preview", help,
                onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            take.Entry.InlineTooltipOnFocus("the maximum number of selected events to show in the preview", help,
                onFocusChanged: async (_, focused) => await TogglePreviewRelatedFocus(focused));

            return Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Auto, Auto, Auto, Auto, Auto], spacing: 5,
                Lbl("How to dig a gig").StyleClass(Styles.Label.SubHeadline),
                scrapeConfigInfo.CenterVertical().Column(1),
                lastEventCount.Column(2),
                lastRefreshed.Column(3),
                help.layout.Row(1).ColumnSpan(4),
                controls.View.Row(2).ColumnSpan(4),
                PagingControls(help).Row(3).ColumnSpan(4),
                previewControls.View.Row(4).ColumnSpan(4),
                previewOrErrors.Row(5).ColumnSpan(4));

            async Task TogglePreviewRelatedFocus(bool focused)
            {
                if (focused) model.PreviewRelatedHasFocus = true;
                else
                {
                    await Task.Delay(300); // to allow for using the skip/take steppers without flickering
                    if (!previewRelated.Any(vis => vis.IsFocused)) model.PreviewRelatedHasFocus = false;
                }
            }
        }

        private FlexLayout PagingControls((Label label, Border layout) help)
        {
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
                    model.LoadMoreCommand.NotifyCanExecuteChanged();
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

            var test = Btn("▶", nameof(LoadMoreCommand), parameterSource: pageView).ToolTip(HelpTexts.TestPagingStrategy);
            return HWrap(5, Lbl("Loading").Bold(), pagingStrategy, nextPageSelector, test).View;

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

        private static FlexLayout ScrapeLogs(VenueEditor model)
        {
            var save = Swtch(nameof(SaveScrapeLogs));
            save.Switch.ToolTip(HelpTexts.SaveScrapLogs);

            DataTemplate itemTemplate = new(() =>
            {
                var deleteBtn = Lbl("🗑").BindTapGesture(nameof(DeleteScrapeLogCommand),
                    commandSource: model, parameterPath: ".");

                var label = BndLbl(nameof(ScrapeLogFile.ForVenue.TimeStamp)).Padding(10)
                    .BindTapGesture(nameof(OpenFileCommand), commandSource: model,
                        parameterPath: nameof(ScrapeLogFile.ForVenue.Path));

                return HStack(5, deleteBtn, label).View;
            });

            var logs = new CollectionView
            {
                ItemsSource = model.ScrapeLogs,
                ItemsLayout = LinearItemsLayout.Horizontal,
                ItemTemplate = itemTemplate
            }
                .ToolTip("Tap any log to open it.");

            return HWrap(5, Lbl("📜 Scrape logs").Bold(), Lbl("save"), save.Wrapper, logs).View;
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

            return HStack(0, entry, layout).View;
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
