﻿using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using PuppeteerSharp;

namespace FomoCal.Gui.ViewModels;

public partial class ScrapeJobEditor : ObservableObject
{
    private readonly string label;
    private readonly Func<IElementHandle[]?> getEventsForPreview;
    private readonly string? defaultAttribute;
    internal readonly bool IsOptional;

    internal ScrapeJob? ScrapeJob { get; private set; }
    public string EventProperty { get; }

    [ObservableProperty] private string?[]? previewResults;
    [ObservableProperty] private bool hasErrors;

    public string Selector
    {
        get => ScrapeJob?.Selector ?? "";
        set
        {
            if (ScrapeJob == null && value.HasSignificantValue())
            {
                ScrapeJob = new();
                Attribute = defaultAttribute;
            }

            if (ScrapeJob != null && ScrapeJob.Selector != value)
            {
                ScrapeJob.Selector = value;
                OnPropertyChanged(nameof(Selector));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(DisplayInputs));
                UpdatePreviewAsync();
            }
        }
    }

    public string? Attribute
    {
        get => ScrapeJob?.Attribute;
        set
        {
            if (ScrapeJob != null && ScrapeJob.Attribute != value)
            {
                ScrapeJob.Attribute = value;
                OnPropertyChanged(nameof(Attribute));
                UpdatePreviewAsync();
            }
        }
    }

    public string? Match
    {
        get => ScrapeJob?.Match;
        set
        {
            if (ScrapeJob != null && ScrapeJob.Match != value)
            {
                ScrapeJob.Match = value;
                OnPropertyChanged(nameof(Match));
                UpdatePreviewAsync();
            }
        }
    }

    public bool IgnoreNestedText
    {
        get => ScrapeJob?.IgnoreNestedText ?? false;
        set
        {
            if (ScrapeJob != null && ScrapeJob.IgnoreNestedText != value)
            {
                ScrapeJob.IgnoreNestedText = value;
                OnPropertyChanged(nameof(IgnoreNestedText));
                UpdatePreviewAsync();
            }
        }
    }

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
                OnPropertyChanged(nameof(DisplayInputs));
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

    public ScrapeJobEditor(string label, ScrapeJob? scrapeJob, Func<IElementHandle[]?> getEventsForPreview,
        string eventProperty, bool isOptional, string? defaultAttribute = null)
    {
        this.label = label;
        ScrapeJob = scrapeJob;
        this.getEventsForPreview = getEventsForPreview;
        this.defaultAttribute = defaultAttribute;
        IsOptional = isOptional;
        EventProperty = eventProperty;
    }

    private void Validate() => IsValid = !HasErrors && PreviewResults?.Length == getEventsForPreview()?.Length;

    private Guid? focusedId;
    [ObservableProperty] private bool hasFocus;
    [ObservableProperty] private string? help;

    internal async ValueTask SetFocusAsync(VisualElement visual, bool focused)
    {
        if (focused)
        {
            HasFocus = true;
            Help = ToolTipProperties.GetText(visual)?.ToString();
            focusedId = visual.Id;
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

    internal async Task UpdatePreviewAsync()
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

            var resultGetters = events.Select<IElementHandle, Task<(string? value, Exception? error)>>(async e =>
            {
                try
                {
                    // use defaultAttribute as an indicator to scrape a URL
                    var getValue = defaultAttribute == null ? ScrapeJob!.GetValueAsync(e) : ScrapeJob!.GetUrlAsync(e);
                    string? value = await getValue;
                    return (value, null);
                }
                catch (Exception ex)
                {
                    return (null, ex);
                }
            }).ToArray();

            await Task.WhenAll(resultGetters);
            Exception[] errors = resultGetters.Select(t => t.Result).Where(r => r.error != null).Select(r => r.error!).Distinct().ToArray();

            if (errors.Length > 0)
            {
                PreviewResults = errors.Select(ex => ex.Message).ToArray();
                HasErrors = true;
                Validate();
            }
            else
            {
                PreviewResults = resultGetters.Select(r => r.Result.value).ToArray();
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
            Spacing = 8;

            var helper = new Label()
                .Bind(Label.TextProperty, nameof(Help)).TextColor(Colors.Yellow)
                .BindIsVisibleToValueOf(nameof(Help));

            var form = new HorizontalStackLayout { Spacing = 8 };

            List<IView> children = [
                new Label().Text(model.label).Bold(),
                new CheckBox()
                    .Bind(CheckBox.IsCheckedProperty, nameof(DisplayInputs))
                    .ForwardFocusTo(model)
                    .Bind(IsVisibleProperty, nameof(IsEmpty)),

                new Label().Text("selector").DisplayWithSignificant(nameof(Selector)),
                new Entry().Bind(Entry.TextProperty, nameof(Selector))
                    .DisplayWithSignificant(nameof(Selector))
                    .ToolTip("A CSS selector to the element containing the text of the event detail." +
                        " See https://www.w3schools.com/cssref/css_selectors.php and https://www.w3schools.com/cssref/css_ref_pseudo_classes.php")
                    .ForwardFocusTo(model),

                new Label().Text("ignore nested text").DisplayWithChecked(nameof(IgnoreNestedText)),
                new CheckBox().Bind(CheckBox.IsCheckedProperty, nameof(IgnoreNestedText))
                    .DisplayWithChecked(nameof(IgnoreNestedText))
                    .ToolTip("Whether to ignore the text of nested elements and only extract direct text nodes from the HTML." +
                        " Does not apply if an attribute is set.")
                    .ForwardFocusTo(model),

                new Label().Text("attribute").DisplayWithSignificant(nameof(Attribute)),
                new Entry().Bind(Entry.TextProperty, nameof(Attribute))
                    .DisplayWithSignificant(nameof(Attribute))
                    .ToolTip("The name of the attribute of the selected element to extract the text from.")
                    .ForwardFocusTo(model),

                new Label().Text("match").DisplayWithSignificant(nameof(Match)),
                new Entry().Bind(Entry.TextProperty, nameof(Match))
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
                    new Label().Text("date format"),
                    new Entry().Text(dateScrapeJob.Format)
                        .Bind(Entry.TextProperty,
                            getter: (ScrapeJobEditor _) => dateScrapeJob.Format,
                            setter: (ScrapeJobEditor _, string value) => dateScrapeJob.Format = value)
                        .OnTextChanged(_ => model.UpdatePreviewAsync())
                        .ToolTip("The .NET date format used to parse the date." +
                            " See https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings")
                        .ForwardFocusTo(model),

                    new Label().Text("culture"),
                    new Entry().Text(dateScrapeJob.Culture)
                        .Bind(Entry.TextProperty,
                            getter: (ScrapeJobEditor _) => dateScrapeJob.Culture,
                            setter: (ScrapeJobEditor _, string value) => dateScrapeJob.Culture = value)
                        .OnTextChanged(_ => model.UpdatePreviewAsync())
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

            Children.Add(ScrapeJobPreviewOrErrorList(itemsSource: nameof(PreviewResults),
                hasFocus: nameof(HasFocus), hasError: nameof(HasErrors), source: model));

            model.UpdatePreviewAsync();
        }

        internal static VerticalStackLayout ScrapeJobPreviewOrErrorList(string itemsSource, string hasFocus, string hasError, object source)
            => new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, verticalSize: 10) }
                .Bind(BindableLayout.ItemsSourceProperty, itemsSource)
                .Bind<VerticalStackLayout, bool, bool, bool>(IsVisibleProperty, binding1: new Binding(hasError), binding2: new Binding(hasFocus),
                    convert: static values => values.Item1 || values.Item2) // display if either has error or focus
                .ItemTemplate(() =>
                    new Label()
                        .Bind(Label.TextColorProperty, hasError, convert: static (bool err) => err ? Colors.IndianRed : Colors.ForestGreen, source: source)
                        .Bind(Label.TextProperty, path: "."));
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
            convert: values => values.Item1 || values.Item2.HasSignificantValue());

    internal static T DisplayWithChecked<T>(this T vis, string boolPropertyName) where T : VisualElement
        => vis.Bind<T, bool, bool, bool>(VisualElement.IsVisibleProperty,
            binding1: new Binding(nameof(ScrapeJobEditor.HasFocus)),
            binding2: new Binding(boolPropertyName),
            convert: values => values.Item1 || values.Item2);
}
