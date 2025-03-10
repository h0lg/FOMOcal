using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class ScrapeJobEditor : ObservableObject
{
    private readonly string label;
    private readonly Func<AngleSharp.Dom.IElement[]?> getEventsForPreview;
    private readonly string? defaultAttribute;
    internal readonly bool IsOptional;

    internal ScrapeJob? ScrapeJob { get; private set; }
    public string EventProperty { get; }

    [ObservableProperty] private string?[]? previewResults;
    [ObservableProperty] private bool hasErrors;

    #region ScrapeJob proxy properties
    public string? Closest
    {
        get => ScrapeJob?.Closest;
        set
        {
            if (ScrapeJob == null || ScrapeJob.Closest == value) return;
            ScrapeJob.Closest = value;
            OnPropertyChanged();
        }
    }

    public string Selector
    {
        get => ScrapeJob?.Selector ?? "";
        set
        {
            if (ScrapeJob == null && value.IsSignificant())
            {
                ScrapeJob = new();
                Attribute = defaultAttribute;
            }

            if (ScrapeJob == null || ScrapeJob.Selector == value) return;
            ScrapeJob.Selector = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(DisplayInputs));
        }
    }

    public bool IgnoreNestedText
    {
        get => ScrapeJob?.IgnoreNestedText ?? false;
        set
        {
            if (ScrapeJob == null || ScrapeJob.IgnoreNestedText == value) return;
            ScrapeJob.IgnoreNestedText = value;
            OnPropertyChanged();
        }
    }

    public string? Attribute
    {
        get => ScrapeJob?.Attribute;
        set
        {
            if (ScrapeJob == null || ScrapeJob.Attribute == value) return;
            ScrapeJob.Attribute = value;
            OnPropertyChanged();
        }
    }

    public string? Match
    {
        get => ScrapeJob?.Match;
        set
        {
            if (ScrapeJob == null || ScrapeJob.Match == value) return;
            ScrapeJob.Match = value;
            OnPropertyChanged();
        }
    }
    #endregion

    public bool IsEmpty => Selector.IsNullOrWhiteSpace();

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

    internal ScrapeJobEditor(string label, ScrapeJob? scrapeJob, Func<AngleSharp.Dom.IElement[]?> getEventsForPreview,
        string eventProperty, bool isOptional, string? defaultAttribute = null)
    {
        this.label = label;
        this.getEventsForPreview = getEventsForPreview;
        this.defaultAttribute = defaultAttribute;
        ScrapeJob = scrapeJob;
        IsOptional = isOptional;
        EventProperty = eventProperty;

        string[] scrapeJobProperties = [nameof(Closest), nameof(Selector), nameof(IgnoreNestedText),
            nameof(Attribute), nameof(Match)];

        PropertyChanged += (o, e) =>
        {
            if (scrapeJobProperties.Contains(e.PropertyName)) UpdatePreview();
        };
    }

    private void Validate() => IsValid = !HasErrors && PreviewResults?.Length == getEventsForPreview()?.Length;

    private Guid? focusedId; // tracks the child that currently has focus
    [ObservableProperty] private bool hasFocus;
    [ObservableProperty] private string? help;

    internal async ValueTask SetFocusAsync(VisualElement visual, bool focused)
    {
        if (focused)
        {
            HasFocus = true;
            Help = ToolTipProperties.GetText(visual)?.ToString();
            focusedId = visual.Id; // assign this visual the component's focus token
            return;
        }

        /*  Only unfocus the component if after a short while no other child has taken focus.
            This enables binding the IsVisibleProperty of empty child controls to the component focus
            while still allowing to [Tab] into them by keeping them visible just long enough. */
        await Task.Delay(10);

        if (visual.Id == focusedId)
        {
            HasFocus = false;
            focusedId = null;
            Help = null;
        }
    }

    internal void UpdatePreview()
    {
        if (ScrapeJob == null) return;

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
                    var value = defaultAttribute == null ? ScrapeJob!.GetValue(e) : ScrapeJob!.GetUrl(e);
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
        public View(ScrapeJobEditor model)
        {
            BindingContext = model;
            Spacing = 5;

            var helper = BndLbl(nameof(Help)).TextColor(Colors.Yellow)
                .BindIsVisibleToValueOf(nameof(Help));

            var form = new HorizontalStackLayout { Spacing = 5 };
            const string closest = nameof(Closest);

            List<IView> children = [
                Lbl(model.label).Bold(),
                Check(nameof(DisplayInputs))
                    .ForwardFocusTo(model)
                    .BindVisible(nameof(IsEmpty)),

                Lbl("closest").DisplayWithSignificant(closest),
                Entr(closest)
                    .DisplayWithSignificant(closest)
                    .ToolTip("An optional CSS selector to an ancestor of the event container to select the event detail from" +
                        " - for when the event detail is outside of the event container.")
                    .ForwardFocusTo(model),

                Lbl("selector").DisplayWithSignificant(nameof(Selector)),
                Entr(nameof(Selector))
                    .DisplayWithSignificant(nameof(Selector))
                    .ToolTip("A CSS selector to the element containing the text of the event detail." +
                        " See https://www.w3schools.com/cssref/css_selectors.php and https://www.w3schools.com/cssref/css_ref_pseudo_classes.php")
                    .ForwardFocusTo(model),

                Lbl("ignore nested text").DisplayWithChecked(nameof(IgnoreNestedText)),
                Check(nameof(IgnoreNestedText))
                    .DisplayWithChecked(nameof(IgnoreNestedText))
                    .ToolTip("Whether to ignore the text of nested elements and only extract direct text nodes from the HTML." +
                        " Does not apply if an attribute is set.")
                    .ForwardFocusTo(model),

                Lbl("attribute").DisplayWithSignificant(nameof(Attribute)),
                Entr(nameof(Attribute))
                    .DisplayWithSignificant(nameof(Attribute))
                    .ToolTip("The name of the attribute of the selected element to extract the text from.")
                    .ForwardFocusTo(model),

                Lbl("match").DisplayWithSignificant(nameof(Match)),
                Entr(nameof(Match))
                    .DisplayWithSignificant(nameof(Match))
                    .ToolTip("A pattern (Regular Expression https://en.wikipedia.org/wiki/Regular_expression in .NET flavour) that matches the part of text to extract." +
                        " You may want to do this to extract text that is not cleanly selectable." +
                        " https://regex101.com/ is great to debug your RegEx, learn and find existing patterns." +
                        " If you can't be bothered or are struggling - ask a chat bot for help, they're pretty good at this.")
                    .ForwardFocusTo(model)
            ];

            if (model.ScrapeJob is DateScrapeJob dateScrapeJob)
            {
                /* No need to bind visibility to DisplayInputs or handle model.scrapeJob being initialized lazily.
                 * We currently only have one DateScrapeJob and it is required i.e. initialized. */

                children.AddRange(
                    Lbl("date format"),
                    new Entry().Text(dateScrapeJob.Format)
                        .Bind(Entry.TextProperty,
                            getter: (ScrapeJobEditor _) => dateScrapeJob.Format,
                            setter: (ScrapeJobEditor _, string value) => dateScrapeJob.Format = value)
                        .OnTextChanged(_ => model.UpdatePreview())
                        .ToolTip("The .NET date format used to parse the date." +
                            " See https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings")
                        .ForwardFocusTo(model),

                    Lbl("culture"),
                    new Entry().Text(dateScrapeJob.Culture)
                        .Bind(Entry.TextProperty,
                            getter: (ScrapeJobEditor _) => dateScrapeJob.Culture,
                            setter: (ScrapeJobEditor _, string value) => dateScrapeJob.Culture = value)
                        .OnTextChanged(_ => model.UpdatePreview())
                        .ToolTip("The language/country code used to parse the date in ISO 639 (en) or ISO 3166 format (en-US)." +
                            " See https://en.wikipedia.org/wiki/Language_code")
                        .ForwardFocusTo(model)
                );
            }

            foreach (var child in children)
            {
                if (child is Label label) label.CenterVertical();
                form.Children.Add(child);
            }

            Children.Add(helper);
            Children.Add(form);

            Children.Add(PreviewOrErrorList(itemsSource: nameof(PreviewResults),
                hasFocus: nameof(HasFocus), hasError: nameof(HasErrors), source: model));

            model.UpdatePreview();
        }

        internal static VerticalStackLayout PreviewOrErrorList(string itemsSource, string hasFocus, string hasError, object source)
            => new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, verticalSize: 10) }
                .Bind(BindableLayout.ItemsSourceProperty, itemsSource)
                .Bind<VerticalStackLayout, bool, bool, bool>(IsVisibleProperty, binding1: new Binding(hasError), binding2: new Binding(hasFocus),
                    convert: static values => values.Item1 || values.Item2) // display if either has error or focus
                .ItemTemplate(() => BndLbl().Bind(Label.TextColorProperty, hasError,
                    convert: static (bool err) => err ? Colors.IndianRed : Colors.ForestGreen, source: source));
    }
}

internal static class ScopeJobEditorExtensions
{
    internal static T ForwardFocusTo<T>(this T vis, ScrapeJobEditor model) where T : VisualElement
        => vis.OnFocusChanged(async (vis, focused) => await model.SetFocusAsync(vis, focused));

    internal static T DisplayWithSignificant<T>(this T vis, string textPropertyName) where T : VisualElement
        => vis.Bind<T, bool, string, bool>(VisualElement.IsVisibleProperty,
            binding1: new Binding(nameof(ScrapeJobEditor.HasFocus)),
            binding2: new Binding(textPropertyName),
            convert: values => values.Item1 || values.Item2.IsSignificant());

    internal static T DisplayWithChecked<T>(this T vis, string boolPropertyName) where T : VisualElement
        => vis.Bind<T, bool, bool, bool>(VisualElement.IsVisibleProperty,
            binding1: new Binding(nameof(ScrapeJobEditor.HasFocus)),
            binding2: new Binding(boolPropertyName),
            convert: values => values.Item1 || values.Item2);
}
