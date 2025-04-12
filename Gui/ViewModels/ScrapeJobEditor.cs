using System.Reflection;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using FomoCal.Gui.Resources;
using Microsoft.Maui.Layouts;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class ScrapeJobEditor : ObservableObject
{
    private readonly string label;
    private readonly Func<AngleSharp.Dom.IElement[]?> getEventsForPreview;
    private readonly Func<VisualElement?> getVisualSelectorHost;
    private readonly string? defaultAttribute;
    internal readonly bool IsOptional;

    internal ScrapeJob ScrapeJob { get; private set; }
    internal DateScrapeJob? DateScrapeJob => ScrapeJob as DateScrapeJob;
    public string EventProperty { get; }

    [ObservableProperty] private string?[]? previewResults;
    [ObservableProperty] private bool hasErrors;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayInputs))] private bool isEmpty;

    #region ScrapeJob proxy properties
    private static readonly string[] scrapeJobStringPropertyNames = [nameof(Closest), nameof(Selector), nameof(Attribute), nameof(Replace), nameof(Match)];
    private static readonly string[] scrapeJobPropertyNames = [.. scrapeJobStringPropertyNames, nameof(IgnoreNestedText), nameof(Format), nameof(Culture)];

    private static readonly PropertyInfo[] scrapeJobStringProperties = typeof(ScrapeJobEditor).GetProperties()
        .Where(p => scrapeJobStringPropertyNames.Contains(p.Name)).ToArray();

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

    public string Format
    {
        /* No need to handle model.scrapeJob being initialized lazily.
         * We currently only have one DateScrapeJob and it is required i.e. initialized. */
        get => DateScrapeJob!.Format;
        set
        {
            if (DateScrapeJob!.Format == value) return;
            DateScrapeJob.Format = value;
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
        get => displayInputs || !IsEmpty;
        set
        {
            if (displayInputs != value)
            {
                displayInputs = value;
                OnPropertyChanged();
            }
        }
    }

    internal event EventHandler<bool>? IsValidChanged;

    private bool isValid;
    internal bool IsValid
    {
        get => isValid;
        private set
        {
            if (isValid != value)
            {
                isValid = value;
                IsValidChanged?.Invoke(this, isValid);
            }
        }
    }

    internal ScrapeJobEditor(string label, ScrapeJob scrapeJob,
        Func<AngleSharp.Dom.IElement[]?> getEventsForPreview, Func<VisualElement?> getVisualSelectorHost,
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
            if (!scrapeJobPropertyNames.Contains(e.PropertyName)) return;

            if (scrapeJobStringPropertyNames.Contains(e.PropertyName))
            {
                UpdateEmpty();
                if (!IsEmpty && Attribute.IsNullOrWhiteSpace()) Attribute = defaultAttribute;
            }

            UpdatePreview();
        };

        UpdateEmpty(); // to initialize it correctly
    }

    private void UpdateEmpty() => IsEmpty = !scrapeJobStringProperties.Any(p => ((string?)p.GetValue(this, null)).IsSignificant());
    private void Validate() => IsValid = !HasErrors && PreviewResults?.Length == getEventsForPreview()?.Length;

    private Guid? focusedId; // tracks the child that currently has focus
    [ObservableProperty] private bool hasFocus;

    internal async ValueTask SetFocusAsync(VisualElement visual, bool focused)
    {
        if (focused)
        {
            HasFocus = true;
            focusedId = visual.Id; // assign this visual the component's focus token
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
            HasFocus = false;
            focusedId = null;
        }
    }

    internal void UpdatePreview()
    {
        if (IsEmpty) return;

        try
        {
            var events = getEventsForPreview();

            if (events == null || events.Length == 0)
            {
                PreviewResults = ["Event Selector matched no events for preview."];
                HasErrors = true;
                Validate();
                return;
            }

            var results = events.Select<AngleSharp.Dom.IElement, (string? value, Exception? error)>(e =>
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

            Exception[] errors = results.Where(r => r.error != null).Select(r => r.error!).Distinct().ToArray();

            if (errors.Length > 0)
            {
                PreviewResults = errors.Select(ex => ex.Message).ToArray();
                HasErrors = true;
                Validate();
            }
            else
            {
                PreviewResults = results.Select(r => r.value).ToArray();
                HasErrors = false;
                Validate();
            }
        }
        catch (Exception ex)
        {
            PreviewResults = [ex.Message];
            HasErrors = true;
        }
    }

    public partial class View : VerticalStackLayout
    {
        private readonly Label help = new();
        private readonly ScrapeJobEditor model;
        private readonly Func<Entry, Func<string?>?, HorizontalStackLayout> createVisualSelectorEntry;

        public View(ScrapeJobEditor model, Func<Entry, Func<string?>?, HorizontalStackLayout> createVisualSelectorEntry)
        {
            this.model = model;
            this.createVisualSelectorEntry = createVisualSelectorEntry;
            BindingContext = model;
            Spacing = 5;

            FlexLayout form = new() { Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center };
            (Switch Switch, Grid Wrapper) displayInputs = Swtch(nameof(DisplayInputs));
            (Switch Switch, Grid Wrapper) ignoreNestedText = Swtch(nameof(IgnoreNestedText));
            displayInputs.Switch.ForwardFocusTo(model);
            HintedInput(ignoreNestedText.Switch, HelpTexts.ScrapeJobIgnoreNestedText);

            List<IView> children = [
                HStack(5, Lbl(model.label).Bold(), displayInputs.Wrapper.BindVisible(nameof(IsEmpty))),

                SelectorEntry("closest", nameof(Closest), null, // for picking common ancestor
                    HelpTexts.ScrapeJobClosest),

                SelectorEntry("selector", nameof(Selector), () => model.Closest, // for picking descendant, preferrably from Closest
                    HelpTexts.ScrapeJobSelector),

                LbldView("ignore nested text", ignoreNestedText.Wrapper).DisplayWithChecked(nameof(IgnoreNestedText)),
                TextEntry("attribute", nameof(Attribute), HelpTexts.ScrapeJobAttribute),
                TextEntry("replace", nameof(Replace), HelpTexts.ScrapeJobReplace),
                TextEntry("match", nameof(Match), HelpTexts.ScrapeJobMatch)
            ];

            if (model.DateScrapeJob is not null) children.AddRange(
                TextEntry("date format", nameof(Format), HelpTexts.DateScrapeJobFormat),
                TextEntry("culture", nameof(Culture), HelpTexts.DateScrapeJobCulture));

            foreach (Microsoft.Maui.Controls.View child in children)
            {
                child.Margins(left: 10);
                form.Children.Add(child);
            }

            Children.Add(help);
            Children.Add(form);

            Children.Add(PreviewOrErrorList(itemsSource: nameof(PreviewResults),
                hasFocus: nameof(HasFocus), hasError: nameof(HasErrors), source: model));

            model.UpdatePreview(); // once initially
        }

        private HorizontalStackLayout SelectorEntry(string label, string property, Func<string?>? maybeGetDescendantOfClosest, string tooltip)
        {
            var input = createVisualSelectorEntry(HintedInput(Entr(property), tooltip), maybeGetDescendantOfClosest);
            return LbldView(label, input).DisplayWithSignificant(property);
        }

        private HorizontalStackLayout TextEntry(string label, string property, string tooltip)
            => LabeledInput(label, Entr(property), tooltip).DisplayWithSignificant(property);

        private HorizontalStackLayout LabeledInput(string label, Microsoft.Maui.Controls.View view, string tooltip)
            => LbldView(label, HintedInput(view, tooltip));

        private T HintedInput<T>(T vis, string tooltip) where T : VisualElement
            => vis.InlineTooltipOnFocus(tooltip, help, async (vis, focused) => await model.SetFocusAsync(vis, focused));

        internal static VerticalStackLayout PreviewOrErrorList(string itemsSource, string hasFocus, string hasError, object source)
        {
            var observable = source as ObservableObject;
            var hasErrorProperty = observable!.GetType().GetProperty(hasError)!;

            var list = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, verticalSize: 10) }
                .Bind(BindableLayout.ItemsSourceProperty, itemsSource)
                .BindVisible(new Binding(hasError), Converters.Or, new Binding(hasFocus)) // display if either has error or focus
                .ItemTemplate(() => SetItemClass(BndLbl(), HasError(hasErrorProperty, source))); // bind item with correct class on construction

            // attaching event handler to set StyleClass on Label children because that property is not bindable
            observable.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == hasError)
                {
                    bool hasErr = HasError(hasErrorProperty, source);
                    foreach (Label label in list.Children) SetItemClass(label, hasErr);
                }
            };

            return list;

            static bool HasError(PropertyInfo hasErrorProperty, object source) => (bool)hasErrorProperty.GetValue(source)!;
            static Label SetItemClass(Label label, bool hasErr) => label.StyleClass(hasErr ? Styles.Label.Error : Styles.Label.Success);
        }
    }
}

internal static class ScopeJobEditorExtensions
{
    internal static T ForwardFocusTo<T>(this T vis, ScrapeJobEditor model) where T : VisualElement
        => vis.OnFocusChanged(async (vis, focused) => await model.SetFocusAsync(vis, focused));

    internal static T DisplayWithSignificant<T>(this T vis, string textPropertyName) where T : VisualElement
        => vis.BindVisible(new Binding(nameof(ScrapeJobEditor.HasFocus)), Converters.Or,
            new Binding(textPropertyName, converter: Converters.IsSignificant));

    internal static T DisplayWithChecked<T>(this T vis, string boolPropertyName) where T : VisualElement
        => vis.BindVisible(new Binding(nameof(ScrapeJobEditor.HasFocus)),
            Converters.Or, new Binding(boolPropertyName));
}
