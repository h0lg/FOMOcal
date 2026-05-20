using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

public partial class ScrapeJobEditor : ObservableObject
{
    private readonly string label;
    private readonly Func<IDomElement[]?> getEventsForPreview;
    private readonly Func<VisualElement?> getVisualSelectorHost;
    private readonly string? defaultAttribute;
    internal readonly bool IsOptional;

    internal ScrapeJob ScrapeJob { get; private set; }
    internal DateScrapeJob? DateScrapeJob => ScrapeJob as DateScrapeJob;
    public string EventProperty { get; }

    [ObservableProperty] public partial ValuePreview[]? PreviewResults { get; set; }
    [ObservableProperty] public partial string? PreviewSummary { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    private bool displayInputs;
    public bool DisplayInputs
    {
        get => displayInputs || HasFocus;
        set
        {
            if (displayInputs != value)
            {
                displayInputs = value;
                OnPropertyChanged();
            }
        }
    }

    internal event EventHandler<bool>? IsValidAsRequiredChanged;

    private bool isValidAsRequired;

    /// <summary>Indicates whether the <see cref="ScrapeJob"/> yields <see cref="PreviewResults"/>
    /// for each event returned by <see cref="getEventsForPreview"/>,
    /// which is necessary for <c>required</c> <see cref="FomoCal.ScrapeJob"/>s in <see cref="Venue.EventScrapeJob"/>.</summary>
    internal bool IsValidAsRequired
    {
        get => isValidAsRequired;
        private set
        {
            if (isValidAsRequired != value)
            {
                isValidAsRequired = value;
                IsValidAsRequiredChanged?.Invoke(this, isValidAsRequired);
            }
        }
    }

#if DEBUG
    static ScrapeJobEditor()
    {
        var properties = typeof(ScrapeJobEditor).GetProperties();

        foreach (var required in DateScrapeJob.Properties)
        {
            if (!properties.Any(p => p.Name == required.Name)) throw new NotImplementedException(
                $"{nameof(ScrapeJobEditor)} requires property {required.Name} for property change comparison to work.");
        }
    }
#endif

    internal ScrapeJobEditor(string label, ScrapeJob scrapeJob,
        Func<IDomElement[]?> getEventsForPreview, Func<VisualElement?> getVisualSelectorHost,
        string eventProperty, bool isOptional, string? defaultAttribute = null)
    {
        this.label = label;
        this.getEventsForPreview = getEventsForPreview;
        this.getVisualSelectorHost = getVisualSelectorHost;
        this.defaultAttribute = defaultAttribute;
        ScrapeJob = scrapeJob;
        IsOptional = isOptional;
        EventProperty = eventProperty;

        PropertyChanged += (o, e) =>
        {
            // use DateScrapeJob properties as the definitive collection; it inherits and enhances ScrapeJob properties
            if (!DateScrapeJob.PropertyNames.Contains(e.PropertyName)) return;

            if (DateScrapeJob.StringPropertyNames.Contains(e.PropertyName))
            {
                UpdateEmpty();

                if (!IsEmpty && e.PropertyName != nameof(Attribute) && Attribute.IsNullOrWhiteSpace())
                    Attribute = defaultAttribute; // init attribute with default
            }

            UpdatePreview();
        };

        UpdateEmpty(); // to initialize it correctly
    }

    private void UpdateEmpty() => IsEmpty = ScrapeJob.IsEmpty();

    private void ValidateAsRequired() =>
        IsValidAsRequired = PreviewResults?.CountSucceeded(true) == getEventsForPreview()?.Length;

    private Guid? focusedId; // tracks the child that currently has focus
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayInputs))] public partial bool HasFocus { get; set; }

    internal async ValueTask SetFocusAsync(VisualElement visual, bool focused)
    {
        if (focused)
        {
            focusedId = visual.Id; // assign this visual the component's focus token
            HasFocus = true;
            return;
        }

        /*  Only unfocus the component if after a short while no other child has taken focus.
            This enables binding the IsVisibleProperty of empty child controls to the component focus
            while still allowing to [Tab] into them by keeping them visible just long enough. */
        await Task.Delay(10);

        /*  Only propagate the loss of focus to the component if
            a) it concerns the element currently holding the focus token and
            b) has not currently opened the visualSelector

            a) supports the mechanism described with above Delay
            b) allows the help to stay visible while working in the visualSelector */
        if (visual.Id == focusedId && visual != getVisualSelectorHost())
        {
            focusedId = null;
            HasFocus = false;
        }
    }

    private IEnumerable<string> GetPreviewValues(ScrapeJob.Step? before = null)
    {
        var events = getEventsForPreview();
        if (events == null || events.Length == 0) return [];

        var results = events.Select(e =>
        {
            try { return ScrapeJob.PreviewValue(e, before); }
            catch { return null; }
        }).ToArray();

        return results.WithValue().Distinct();
    }

    internal void UpdatePreview()
    {
        if (IsEmpty) return;

        try
        {
            var events = getEventsForPreview();

            if (events == null || events.Length == 0)
            {
                PreviewResults = [];
                PreviewSummary = "❔";
                if (!IsOptional) ValidateAsRequired();
                return;
            }

            var results = events.Select<IDomElement, (string? value, Exception? error)>(e =>
            {
                try
                {
                    // use defaultAttribute as an indicator to scrape a URL
                    var value = defaultAttribute == null ? ScrapeJob.GetValue(e) : ScrapeJob.GetUrl(e);
                    return (value, null);
                }
                catch (Exception ex)
                {
                    return (null, ex);
                }
            }).ToArray();

            PreviewResults = [.. results.Select(r => r.error == null ? ValuePreview.Create(r.value) : ValuePreview.Error(r.error))];
        }
        catch (Exception ex)
        {
            PreviewResults = [ValuePreview.Error(ex)];
        }

        UpdatePreviewSummary();
    }

    private void UpdatePreviewSummary()
    {
        int errors = PreviewResults!.CountSucceeded(false);
        int successes = PreviewResults!.CountSucceeded(true);

        string?[] states = [(errors > 0 ? errors + Glyphs.Error : null),
             (successes > 0 ? successes + "✅" : null)];

        PreviewSummary = states.Join(" ");
        if (!IsOptional) ValidateAsRequired();
    }

    private void GuessDateFormat()
    {
        if (DateScrapeJob == null || !Format.IsNullOrWhiteSpace() || !Culture.IsNullOrWhiteSpace())
            return;

        (string? culture, string? format)[]? guesses;

        try
        {
            guesses = DateFormat.Guess([.. GetPreviewValues()], [.. PreferredDateCultures.Remembered],
                reportError: async error => await ErrorReport.WriteAsyncAndShare(error, "guessing date"));
        }
        catch (Exception ex)
        {
            PreviewResults = [ValuePreview.Error(ex)];
            UpdatePreviewSummary();
            return;
        }

        if (guesses.Length == 0) return;

        (string? culture, string? format) pick;

        if (guesses.Length > 1)
        {
            pick = guesses[0];
        }
        else pick = guesses[0];

        if (pick.culture != null) Culture = pick.culture;
        if (pick.format != null) Format = pick.format;
    }
}
