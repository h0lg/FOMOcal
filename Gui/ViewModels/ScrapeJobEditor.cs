﻿using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;

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
                UpdatePreview();
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
                UpdatePreview();
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
                UpdatePreview();
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
                UpdatePreview();
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

    public ScrapeJobEditor(string label, ScrapeJob? scrapeJob, Func<AngleSharp.Dom.IElement[]?> getEventsForPreview,
        string eventProperty, bool isOptional, string? defaultAttribute = null)
    {
        this.label = label;
        ScrapeJob = scrapeJob;
        this.getEventsForPreview = getEventsForPreview;
        this.defaultAttribute = defaultAttribute;
        IsOptional = isOptional;
        EventProperty = eventProperty;
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
                new CheckBox().Bind(CheckBox.IsCheckedProperty, nameof(DisplayInputs)).Bind(IsVisibleProperty, nameof(IsEmpty)),

                new Label().Text("Selector").ToggleWithInputs(),
                new Entry().Bind(Entry.TextProperty, nameof(Selector))
                    .ToggleWithInputs(),

                new Label().Text("Ignore nested text").ToggleWithInputs(),
                new CheckBox().Bind(CheckBox.IsCheckedProperty, nameof(IgnoreNestedText))
                    .ToggleWithInputs(),

                new Label().Text("Attribute").ToggleWithInputs(),
                new Entry().Bind(Entry.TextProperty, nameof(Attribute))
                    .ToggleWithInputs(),

                new Label().Text("Match (Regex)").ToggleWithInputs(),
                new Entry().Bind(Entry.TextProperty, nameof(Match))
                    .ToggleWithInputs()
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
                        .OnTextChanged(_ => model.UpdatePreview()),

                    new Label().Text("Culture"),
                    new Entry().Text(dateScrapeJob.Culture)
                        .Bind(Entry.TextProperty,
                            getter: (ScrapeJobEditor _) => dateScrapeJob.Culture,
                            setter: (ScrapeJobEditor _, string value) => dateScrapeJob.Culture = value)
                        .OnTextChanged(_ => model.UpdatePreview())
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
                    .ItemTemplate(() => new Label().TextColor(Colors.Green).Bind(Label.TextProperty, path: ".")));

            Children.Add(
                new VerticalStackLayout()
                    .Bind(BindableLayout.ItemsSourceProperty, nameof(Errors))
                    .ItemTemplate(() => new Label().TextColor(Colors.Red).Bind(Label.TextProperty, path: ".")));

            model.UpdatePreview();
        }
    }
}

internal static class ScopeJobEdidorExtensions
{
    internal static T ToggleWithInputs<T>(this T vis) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, nameof(ScrapeJobEditor.DisplayInputs));
}
