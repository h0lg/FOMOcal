using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using FomoCal.Gui.Resources;
using Microsoft.Maui.Layouts;
using static FomoCal.Gui.ViewModels.Widgets;

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

    #region ScrapeJob proxy properties
    public string? Closest
    {
        get => ScrapeJob.Closest;
        set
        {
            if (ScrapeJob.Closest == value) return;
            ScrapeJob.Closest = value;
            OnPropertyChanged();
        }
    }

    public string? Selector
    {
        get => ScrapeJob.Selector;
        set
        {
            if (ScrapeJob.Selector == value) return;
            ScrapeJob.Selector = value;
            OnPropertyChanged();
        }
    }

    public bool IgnoreNestedText
    {
        get => ScrapeJob.IgnoreNestedText;
        set
        {
            if (ScrapeJob.IgnoreNestedText == value) return;
            ScrapeJob.IgnoreNestedText = value;
            OnPropertyChanged();
        }
    }

    public string? Attribute
    {
        get => ScrapeJob.Attribute;
        set
        {
            if (ScrapeJob.Attribute == value) return;
            ScrapeJob.Attribute = value;
            OnPropertyChanged();
        }
    }

    public string? Replace
    {
        get => ScrapeJob.Replace;
        set
        {
            if (ScrapeJob.Replace == value) return;
            ScrapeJob.Replace = value;
            OnPropertyChanged();
        }
    }

    public string? Match
    {
        get => ScrapeJob.Match;
        set
        {
            if (ScrapeJob.Match == value) return;
            ScrapeJob.Match = value;
            OnPropertyChanged();
        }
    }

    public string? Comment
    {
        get => ScrapeJob.Comment;
        set
        {
            if (ScrapeJob.Comment == value) return;
            ScrapeJob.Comment = value;
            OnPropertyChanged();
        }
    }

    // DateScrapeJob proxies
    public string Format
    {
        /* No need to handle model.scrapeJob being initialized lazily.
         * We currently only have one DateScrapeJob and it is required i.e. initialized. */
        get => DateScrapeJob!.Format;
        set
        {
            if (DateScrapeJob!.Format == value) return;
            DateScrapeJob.UpdateFormat(value);
            OnPropertyChanged();
        }
    }

    public string Culture
    {
        get => DateScrapeJob!.Culture;
        set
        {
            if (DateScrapeJob!.Culture == value) return;
            DateScrapeJob.Culture = value;
            OnPropertyChanged();
        }
    }
    #endregion

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
        IsValidAsRequired = PreviewResults?.Count(p => p.State == ValuePreview.States.Success) == getEventsForPreview()?.Length;

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

    private IEnumerable<string> GetPreviewValues(ScrapeJob.Step? before)
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

        int errors = PreviewResults.Count(p => p.State == ValuePreview.States.Error);
        int successes = PreviewResults.Count(p => p.State == ValuePreview.States.Success);

        string?[] states = [(errors > 0 ? errors + Glyphs.Error : null),
             (successes > 0 ? successes + "✅" : null)];

        PreviewSummary = states.Join(" ");
        if (!IsOptional) ValidateAsRequired();
    }

    public partial class View : Border
    {
        private readonly (Label label, Border layout) help;
        private readonly ScrapeJobEditor model;
        private readonly Func<Entry, Func<string?>?, HorizontalStackLayout> createVisualSelectorEntry;
        private readonly Func<Entry?> getVisualSelectorHost;

        public View(ScrapeJobEditor model,
            Func<Entry, Func<string?>?, HorizontalStackLayout> createVisualSelectorEntry,
            Func<Entry?> getVisualSelectorHost)
        {
            this.model = model;
            this.createVisualSelectorEntry = createVisualSelectorEntry;
            this.getVisualSelectorHost = getVisualSelectorHost;
            help = HelpLabel();
            StyleClass = [Styles.Border.RoundedSection];
            BindingContext = model;

            (Switch Switch, Grid Wrapper) displayInputs = Swtch(nameof(DisplayInputs),
                BindingMode.OneWayToSource); // avoids triggering set by reaction to PropertyChanged, falsifying field value

            (Switch Switch, Grid Wrapper) ignoreNestedText = Swtch(nameof(IgnoreNestedText));
            HintedInput(ignoreNestedText.Switch, HelpTexts.ScrapeJobIgnoreNestedText);

            List<IView> children = [
                HStack(5, Lbl(model.label).Bold(), displayInputs.Wrapper.BindVisible(nameof(IsEmpty))),

                SelectorEntry("closest", nameof(Closest),
                    null, // for picking common ancestor of clicked element and event container
                    HelpTexts.ScrapeJobClosest),

                SelectorEntry("selector", nameof(Selector),
                    () => model.Closest, // for picking descendant, preferably from Closest if set
                    HelpTexts.ScrapeJobSelector),

                LbldView("ignore nested text", ignoreNestedText.Wrapper).Wrapper.DisplayWithChecked(nameof(IgnoreNestedText)),
                TextEntry("attribute", nameof(Attribute), HelpTexts.ScrapeJobAttribute),

                TextEntry("replace", nameof(Replace), HelpTexts.ScrapeJobReplace, placeholder: "a }} b",
                    regex101DeepLink: ScrapeJob.Step.Replacements),

                TextEntry("match", nameof(Match), HelpTexts.ScrapeJobMatch,
                    regex101DeepLink: ScrapeJob.Step.Match)
            ];

            if (model.DateScrapeJob is not null) children.AddRange(
                TextEntry(Glyphs.Date + "format", nameof(Format), HelpTexts.DateScrapeJobFormat),
                TextEntry("culture", nameof(Culture), HelpTexts.DateScrapeJobCulture));

            children.Add(TextEntry(Glyphs.Comment, nameof(Comment), HelpTexts.Comment, multiLine: true));

            FlexLayout form = new() { Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center };

            foreach (var child in children.Cast<Microsoft.Maui.Controls.View>())
            {
                child.Margins(left: 10);
                form.Children.Add(child);
            }

            var previewSummary = BndLbl(nameof(PreviewSummary))
                // display if PreviewSummary has value. hide if editor has focus because ValuePreview.List is then shown
                .BindVisible(new Binding(nameof(PreviewSummary), converter: Converters.NotNull),
                    Converters.And, new Binding(nameof(HasFocus), converter: Converters.Not));

            form.Children.Add(previewSummary.End().Grow(1));

            Content = VStack(5, help.layout, form,
                ValuePreview.List(itemsSource: nameof(PreviewResults),
                    hasFocus: nameof(HasFocus), source: model, editor: model));

            model.UpdatePreview(); // once initially
        }

        private Grid SelectorEntry(string label, string property, Func<string?>? maybeGetDescendantOfClosest, string tooltip)
        {
            var input = createVisualSelectorEntry(HintedInput(Entr(property), tooltip,
                cancelFocusChanged: (vis, focused) => !focused && getVisualSelectorHost() == vis),
                maybeGetDescendantOfClosest);

            return LbldView(label, input).Wrapper.DisplayWithSignificant(property);
        }

        private Grid TextEntry(string label, string property, string tooltip,
            bool multiLine = false, string? placeholder = null, ScrapeJob.Step? regex101DeepLink = null)
        {
            InputView input = multiLine ? Edtr(property) : Entr(property);
            HintedInput(input, tooltip).Placeholder(placeholder);

            Microsoft.Maui.Controls.View editor = regex101DeepLink == null ? input
                : HStack(0, input, Regex101.DeepLink(regex101DeepLink.Value, input, model.GetPreviewValues));

            (Grid wrapper, Label _) = LbldView(label, editor);

            if (multiLine)
            {
                FlexLayout.SetGrow(wrapper, 1); // grow multiLine Editor to use the remaining space
                FlexLayout.SetShrink(wrapper, 1); // but shrink it to fit in the same row if possible
            }

            return wrapper.DisplayWithSignificant(property);
        }

        private T HintedInput<T>(T vis, string tooltip,
            Func<VisualElement, bool, bool>? cancelFocusChanged = null) where T : VisualElement
            => vis.InlineTooltipOnFocus(tooltip, help, async (vis, focused) => await model.SetFocusAsync(vis, focused), cancelFocusChanged);
    }
}

internal static class ScopeJobEditorExtensions
{
    internal static T ForwardFocusTo<T>(this T vis, ScrapeJobEditor model) where T : VisualElement
        => vis.OnFocusChanged(async (vis, focused) => await model.SetFocusAsync(vis, focused));

    internal static T DisplayWithSignificant<T>(this T vis, string textPropertyName) where T : VisualElement
        => vis.BindVisible(new Binding(nameof(ScrapeJobEditor.DisplayInputs)), Converters.Or,
            new Binding(textPropertyName, converter: Converters.IsSignificant));

    internal static T DisplayWithChecked<T>(this T vis, string boolPropertyName) where T : VisualElement
        => vis.BindVisible(new Binding(nameof(ScrapeJobEditor.DisplayInputs)),
            Converters.Or, new Binding(boolPropertyName));
}
