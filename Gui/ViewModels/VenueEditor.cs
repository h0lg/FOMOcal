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
    private readonly INavigation navigation;
    private readonly string[] existingVenueNames;
    private readonly List<ScrapeJobEditor> scrapeJobEditors = [];
    private readonly Debouncer debouncedRevealMore;
    private readonly Venue venue;
    private readonly ScrapeJobEditor eventName, eventDate;

    private InputView? visualSelectorHost;
    private IDomDocument? programDocument;

    [ObservableProperty] public partial bool HasRequiredInfo { get; set; }
    [ObservableProperty] public partial bool IsVenueNameTaken { get; set; }
    [ObservableProperty] public partial bool ShowRequiredEventFields { get; set; }
    [ObservableProperty] public partial bool ShowOptionalEventFields { get; set; }
    [ObservableProperty] public partial bool ShowEncoding { get; set; }
    [ObservableProperty] public partial double Progress { get; set; } = 0;

    public string VenueName
    {
        get => venue.Name;
        set
        {
            value = value.Trim();
            if (value == venue.Name) return;
            venue.Name = value;
            OnPropertyChanged();
            IsVenueNameTaken = existingVenueNames.Contains(value);
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

    internal VenueEditor(Venue venue, Scraper scraper, TaskCompletionSource<Actions?> awaiter,
        INavigation navigation, IReadOnlyList<Venue> existingVenues)
    {
        this.venue = venue;
        this.scraper = scraper;
        this.awaiter = awaiter;
        this.navigation = navigation;
        isDeletable = venue.ProgramUrl.IsSignificant();
        originalVenueName = venue.Name;
        EditingProgramUrl = venue.ProgramUrl;
        existingVenueNames = [.. existingVenues.Select(v => v.Name)];

        debouncedRevealMore = new(TimeSpan.FromMilliseconds(100), UndebouncedRevealMore,
            async ex => await ErrorReport.WriteAsyncAndShare(ex.ToString(), "revealing more of the venue editor"));

        var evt = this.venue.Event;
        eventName = ScrapeJob("❗ Name", evt.Name, nameof(Venue.EventScrapeJob.Name));
        eventDate = ScrapeJob(Glyphs.Date + "Date", evt.Date, nameof(Venue.EventScrapeJob.Date));
        eventName.IsValidAsRequiredChanged += (_, _) => RevealMore();
        eventDate.IsValidAsRequiredChanged += (_, _) => RevealMore();

        // only load scrape logs if ProgramUrl is set; use isDeletable as indicator
        ScrapeLogs = new(isDeletable ? ScrapeLogFile.GetAll(venue) : []);

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

        HasRequiredInfo = hasName && hasProgramUrl && !IsVenueNameTaken;
        ShowRequiredEventFields = HasRequiredInfo && EventSelector.IsSignificant();

        // always show optional fields if any are filled - even if a missing internet connection prevents required fields from validating
        ShowOptionalEventFields = scrapeJobEditors.Any(e => e.IsOptional && !e.IsEmpty)
            // otherwise only after required fields are filled to grow the form with progress
            || (ShowRequiredEventFields
                // skip validation without internet access
                && (!App.HasInternet
                    // otherwise make sure required fields have values
                    || (eventName.IsValidAsRequired && eventDate.IsValidAsRequired)));

        Progress = (ShowOptionalEventFields ? 3 : ShowRequiredEventFields ? 2 : HasRequiredInfo ? 1 : 0) / 3d;
        if (ShowRequiredEventFields && previewedEvents == null) UpdateEventContainerPreview();
    }

    private void SetDocument(IDomDocument? document)
    {
        programDocument?.Dispose();
        programDocument = document;
        previewedEvents = null;
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    private void Save()
    {
        // reset empty optional scrape jobs
        foreach (var editor in scrapeJobEditors.Where(e => e.IsOptional))
        {
            var property = typeof(Venue.EventScrapeJob).GetProperty(editor.EventProperty)!;
            property.SetValue(venue.Event, editor.IsEmpty ? null : editor.ScrapeJob);
        }

        foreach (var editor in scrapeJobEditors.WithValue())
            editor.ScrapeJob.ResetInsignificantStrings(); // to reduce noise in serialized JSON

        SetActionTaken(Actions.Saved);
    }

    private void Delete() => SetActionTaken(Actions.Deleted);

    [RelayCommand]
    private async Task CancelFromNavBarAsync()
    {
        SignalCancelation();
        await navigation.PopAsync(); // to unify with OnBackButtonPressed behavior; awaiter expects popped navigation stack
    }

    private void SignalCancelation() => SetActionTaken(null);
    private bool HasTakenAction() => awaiter.Task.IsCompleted;

    private void SetActionTaken(Actions? action)
    {
        if (!HasTakenAction()) awaiter.SetResult(action);
    }

    internal enum Actions { Saved, Deleted }

    public partial class Page : ContentPage
    {
        private readonly VenueEditor model;
        private readonly ScrollView form;

        public Page(VenueEditor model)
        {
            // unify NavBar Back button with OnBackButtonPressed behavior triggered via key or swipe gesture on Android
            Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = model.CancelFromNavBarCommand });

            this.model = model;
            BindingContext = model;
            Title = model.isDeletable ? "Edit " + model.originalVenueName : "Add a venue";
            if (Shell.Current != null) Shell.SetTabBarIsVisible(this, false);
            Shell.SetNavBarIsVisible(this, true); // to show ToolbarItems and Title

            // Progress Indicator
            var progress = new ProgressBar().Bind(ProgressBar.ProgressProperty, nameof(Progress))
                .ToolTip("your progress towards the minimum required configuration to make this venue scrapable");

            Shell.SetTitleView(this, VStack(null,
                Lbl(Title).StyleClass(Styles.Label.Headline),
                progress));

            ToolbarItems.Add(new ToolbarItem("💾 Save", null, model.Save)
                .Bind(MenuItem.IsEnabledProperty, nameof(HasRequiredInfo)));

            if (model.isDeletable) ToolbarItems.Add(new ToolbarItem(
                Glyphs.Delete + " Delete", null, model.Delete, ToolbarItemOrder.Secondary));

            /* CreateVisualSelector before EventContainer because the pageView 
             * created by the former is referenced as a command arg in the latter */
            visualSelector = CreateVisualSelector();

            // Step 1: Venue Name and Program URL
            var venueFields = VenueFields();

            // Step 2: Event container
            var eventContainer = EventContainer().BindVisible(nameof(HasRequiredInfo));

            // Step 3: Event Details (Name, Date)
            const string showRequired = nameof(ShowRequiredEventFields);

            var requiredEventFields = VStack(0,
                new ScrapeJobEditor.View(model.eventName, RelativeSelectorEntry, () => model.visualSelectorHost),
                new ScrapeJobEditor.View(model.eventDate, RelativeSelectorEntry, () => model.visualSelectorHost))
                .BindVisible(showRequired);

            // Step 4: Additional Event Details
            const string showOptional = nameof(ShowOptionalEventFields);
            var optionalEventFields = OptionalEventFields().BindVisible(showOptional);

            form = new ScrollView
            {
                Content = VStack(20, venueFields, eventContainer,
                    PagingControls().BindVisible(showRequired),
                    requiredEventFields, optionalEventFields,
                    EncodingOverride().BindVisible(showRequired),
                    ScrapeLogs(model).BindVisible(showOptional),
                    ScriptLog(model).BindVisible(showOptional))
                    .Padding(20)
            };

            Content = Grd(cols: [Star], rows: [Star, Auto], spacing: 0, form, visualSelector.Row(1));
        }

        private Grid VenueFields()
        {
            ProgramUrlControls(out SearchBar urlSearch, out Editor urlEditor, out Label searchHelp,
                out ActivityIndicator loadingIndicator, out Button reload,
                out Button openUrl, out Label noInternetIndicator);

            var nameEntry = Entr(nameof(VenueName), placeholder: "Venue name");

            var nameTakenIndicator = ErrorLbl("That venue name is taken already. Choose a different one.")
                .BindVisible(nameof(IsVenueNameTaken));

            var comment = Edtr(nameof(Comment), placeholder: "explain this config or something about it").ToolTip(HelpTexts.Comment);

            var location = new Editor { Placeholder = "Location, contacts or other helpful info" }
                .Bind(Editor.TextProperty,
                    getter: static vm => vm.venue.Location,
                    setter: static (VenueEditor vm, string? value) => vm.venue.Location = value);

            return Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Auto, Auto, Auto, Auto, Auto], spacing: 5,
                FldLbl("🕸"), urlSearch.Column(1), urlEditor.Column(1), loadingIndicator.Column(2), reload.Column(2), openUrl.Column(3),
                searchHelp.Row(1).ColumnSpan(4), noInternetIndicator.Row(1).ColumnSpan(4),
                FldLbl("🏷").Row(2), nameEntry.Row(2).Column(1).ColumnSpan(3),
                nameTakenIndicator.Row(3).ColumnSpan(4),
                FldLbl("📍").Row(4), location.Row(4).Column(1).ColumnSpan(3),
                FldLbl(Glyphs.Comment).Row(5), comment.Row(5).Column(1).ColumnSpan(3));

            static Label FldLbl(string Text) => Lbl(Text).CenterVertical();
        }

        private static FlexLayout EncodingOverride()
        {
            const string show = nameof(ShowEncoding);
            var encoding = Entr(nameof(Encoding), placeholder: "encoding override").BindVisible(show);
            var (help, helper) = HelpLabel(isPlaceholder: false);
            help.Text = HelpTexts.Encoding;
            helper.BindVisible(show);
            return HWrap(5, Lbl("🔣 Encoding").Bold(), Swtch(show).Wrapper, encoding, helper).View;
        }

        private static Label ErrorLbl(string text) => Lbl(Glyphs.Error + " " + text).TextCenter();

        private VerticalStackLayout OptionalEventFields()
        {
            var evt = model!.venue.Event;

            (ScrapeJobEditor.View editor, bool empty)[] fields = [
                OptionalScrapeJob("‼ Subtitle", evt.SubTitle, nameof(Venue.EventScrapeJob.SubTitle)),
                OptionalScrapeJob("📜 Description", evt.Description, nameof(Venue.EventScrapeJob.Description)),
                OptionalScrapeJob(Glyphs.Genres + "Genres", evt.Genres, nameof(Venue.EventScrapeJob.Genres)),
                OptionalScrapeJob(Glyphs.Stage + "Stage", evt.Stage, nameof(Venue.EventScrapeJob.Stage)),
                OptionalScrapeJob(Glyphs.Doors + "Doors", evt.DoorsTime, nameof(Venue.EventScrapeJob.DoorsTime)),
                OptionalScrapeJob(Glyphs.Start + "Start", evt.StartTime, nameof(Venue.EventScrapeJob.StartTime)),
                OptionalScrapeJob(Glyphs.PresalePrice + "Pre-sale price", evt.PresalePrice, nameof(Venue.EventScrapeJob.PresalePrice)),
                OptionalScrapeJob(Glyphs.DoorPrice + "Door price", evt.DoorsPrice, nameof(Venue.EventScrapeJob.DoorsPrice)),
                OptionalScrapeJob(Glyphs.EventPage + "Event page " + Glyphs.Link, evt.Url, nameof(Venue.EventScrapeJob.Url), defaultAttribute: "href"),
                OptionalScrapeJob("🖼 Image", evt.ImageUrl, nameof(Venue.EventScrapeJob.ImageUrl), defaultAttribute: "src"),
                OptionalScrapeJob(Glyphs.Tickets + "Tickets " + Glyphs.Link, evt.TicketUrl, nameof(Venue.EventScrapeJob.TicketUrl), defaultAttribute: "href")
            ];

            return VStack(0, [.. fields.OrderBy(f => f.empty).Select(f => f.editor)]); // order empty editors last

            (ScrapeJobEditor.View editor, bool empty) OptionalScrapeJob(string label, ScrapeJob? scrapeJob, string eventProperty, string? defaultAttribute = null)
               => (new(model.ScrapeJob(label, scrapeJob, eventProperty, isOptional: true, defaultAttribute),
                    RelativeSelectorEntry, () => model.visualSelectorHost), scrapeJob == null);
        }

        private Grid SelectorInput(InputView input, Func<(string selector, bool pickDescendant)> pickRelativeTo)
        {
            Border button = new()
            {
                StyleClass = [Styles.Border.EndingEntryButton],
                Content = Lbl("🥢").StyleClass(Styles.Label.EndingEntryButton)
            };

            button.ToolTip("🥢 pluck from the page").TapGesture(async () =>
            {
                if (App.HasInternet)
                {
                    (string selector, bool pickDescendant) = pickRelativeTo.Invoke();
                    await ShowVisualSelectorForAsync(input, selector, pickDescendant);
                }
                else await App.CurrentPage.DisplayAlertAsync("Connect to the internet and retry",
                    "Loading the event listing requires internet access.", "OK");
            });

            return Grd(cols: [Star, Auto], rows: [Auto], 0, input, button.Column(1));
        }

        private Grid RelativeSelectorEntry(InputView input, Func<string?>? maybeGetDescendantOfClosest)
            => SelectorInput(input, pickRelativeTo: () =>
            {
                /*  if maybeGetDescendantOfClosest is set, we're selecting the descendant
                 *  and prefer selecting from the Closest expression over the EventSelector */
                bool picksDescendant = maybeGetDescendantOfClosest != null;
                string selector = picksDescendant ? maybeGetDescendantOfClosest!() ?? model!.EventSelector : model!.EventSelector;
                return (selector, picksDescendant);
            });

        protected override bool OnBackButtonPressed()
        {
            model!.SignalCancelation();
            return base.OnBackButtonPressed();
        }
    }
}
