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
    [ObservableProperty] private string[]? errors;
    [ObservableProperty] private bool hasFocus;

    #region ScrapeJob proxy properties
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

        string[] scrapeJobProperties = [nameof(Selector), nameof(IgnoreNestedText), nameof(Attribute), nameof(Match)];

        PropertyChanged += (o, e) =>
        {
            if (scrapeJobProperties.Contains(e.PropertyName)) UpdatePreview();
        };
    }

    private void Validate() => IsValid = Errors == null && PreviewResults?.Length == getEventsForPreview()?.Length;

    internal void UpdatePreview()
    {
        if (ScrapeJob == null) return;

        try
        {
            var events = getEventsForPreview();

            if (events == null || events.Length == 0)
            {
                PreviewResults = null;
                Errors = ["Event Selector matched no events for preview."];
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
                PreviewResults = null;
                Errors = errors.Select(ex => ex.Message).ToArray();
                Validate();
            }
            else
            {
                PreviewResults = results.Select(r => r.value).ToArray();
                Errors = null;
                Validate();
            }
        }
        catch (Exception ex)
        {
            PreviewResults = null;
            Errors = [ex.Message];
            throw;
        }
    }

    public partial class View : VerticalStackLayout
    {
        public View(ScrapeJobEditor model)
        {
            BindingContext = model;
            Spacing = 8;

            List<IView> children = [
                new Label().Text(model.label).Bold(),
                Check(nameof(DisplayInputs))
                    .ForwardFocusTo(model)
                    .BindVisible(nameof(IsEmpty)),

                new Label().Text("Selector").DisplayWithSignificant(nameof(Selector)),
                new Entry().Bind(Entry.TextProperty, nameof(Selector))
                    .DisplayWithSignificant(nameof(Selector))
                    .ForwardFocusTo(model),

                new Label().Text("Ignore nested text").DisplayWithChecked(nameof(IgnoreNestedText)),
                Check(nameof(IgnoreNestedText))
                    .DisplayWithChecked(nameof(IgnoreNestedText))
                    .ForwardFocusTo(model),

                new Label().Text("Attribute").DisplayWithSignificant(nameof(Attribute)),
                new Entry().Bind(Entry.TextProperty, nameof(Attribute))
                    .DisplayWithSignificant(nameof(Attribute))
                    .ForwardFocusTo(model),

                new Label().Text("Match (Regex)").DisplayWithSignificant(nameof(Match)),
                new Entry().Bind(Entry.TextProperty, nameof(Match))
                    .DisplayWithSignificant(nameof(Match))
                    .ForwardFocusTo(model)
            ];

            if (model.ScrapeJob is DateScrapeJob dateScrapeJob)
            {
                /* No need to bind visibility to DisplayInputs or handle model.scrapeJob being initialized lazily.
                 * We currently only have one DateScrapeJob and it is required i.e. initialized. */

                children.AddRange(
                    new Label().Text("Date Format"),
                    new Entry().Text(dateScrapeJob.Format)
                        .Bind(Entry.TextProperty,
                            getter: (ScrapeJobEditor _) => dateScrapeJob.Format,
                            setter: (ScrapeJobEditor _, string value) => dateScrapeJob.Format = value)
                        .OnTextChanged(_ => model.UpdatePreview())
                        .ForwardFocusTo(model),

                    new Label().Text("Culture"),
                    new Entry().Text(dateScrapeJob.Culture)
                        .Bind(Entry.TextProperty,
                            getter: (ScrapeJobEditor _) => dateScrapeJob.Culture,
                            setter: (ScrapeJobEditor _, string value) => dateScrapeJob.Culture = value)
                        .OnTextChanged(_ => model.UpdatePreview())
                        .ForwardFocusTo(model)
                );
            }

            var form = new HorizontalStackLayout { Spacing = 8 };

            foreach (var child in children)
            {
                if (child is Label label) label.CenterVertical();
                form.Children.Add(child);
            }

            Children.Add(form);

            // **Live Preview Section**
            Children.Add(
                new VerticalStackLayout()
                    .Bind(BindableLayout.ItemsSourceProperty, nameof(PreviewResults))
                    .ItemTemplate(() => new Label().TextColor(Colors.Green).Bind(Label.TextProperty, path: "."))
                    .Bind(IsVisibleProperty, nameof(HasFocus)));

            Children.Add(
                new VerticalStackLayout()
                    .Bind(BindableLayout.ItemsSourceProperty, nameof(Errors))
                    .ItemTemplate(() => new Label().TextColor(Colors.Red).Bind(Label.TextProperty, path: ".")));

            model.UpdatePreview();
        }
    }
}

internal static class ScopeJobEditorExtensions
{
    internal static T ForwardFocusTo<T>(this T vis, ScrapeJobEditor model) where T : VisualElement
    {
        vis.Focused += (_, _) => model.HasFocus = true;
        vis.Unfocused += (_, _) => model.HasFocus = false;
        return vis;
    }

    internal static T DisplayWithSignificant<T>(this T vis, string textPropertyName) where T : VisualElement
        => vis.Bind<T, bool, string, double>(VisualElement.OpacityProperty,
            binding1: new Binding(nameof(ScrapeJobEditor.HasFocus)),
            binding2: new Binding(textPropertyName),
            convert: values => values.Item1 || values.Item2.IsSignificant() ? 1 : 0);

    internal static T DisplayWithChecked<T>(this T vis, string boolPropertyName) where T : VisualElement
        => vis.Bind<T, bool, bool, double>(VisualElement.OpacityProperty,
            binding1: new Binding(nameof(ScrapeJobEditor.HasFocus)),
            binding2: new Binding(boolPropertyName),
            convert: values => values.Item1 || values.Item2 ? 1 : 0);
}
