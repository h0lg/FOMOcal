using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class LuckyUrlSearchSettings : ObservableObject
{
    private static readonly RememberedStrings engines = new("LuckyUrlSearchSettings.engines", "🤞");
    private static readonly RememberedString suffix = new("LuckyUrlSearchSettings.suffix", "concerts");
    internal static string Suffix => suffix.Get()!;

    internal static IEnumerable<LuckyUrlSearch.Engine> Engines
        => engines.Get().Select(Enum.Parse<LuckyUrlSearch.Engine>);

    public string EditableSuffix { get => suffix.Get()!; set => suffix.Set(value); }
    public ObservableCollection<SelectableEngine> Selected { get; }
    public ObservableCollection<SelectableEngine> Deselected { get; }
    public bool HasSelected => Selected.Any();
    public bool HasDeselected => Deselected.Any();

    internal LuckyUrlSearchSettings()
    {
        LuckyUrlSearch.Engine[] allEngines = Enum.GetValues<LuckyUrlSearch.Engine>();
        var selected = Engines;
        if (!selected.Any()) selected = allEngines;
        Selected = new(MapEngines(selected));
        Selected.CollectionChanged += (o, e) => engines.Set(Selected.Select(c => c.Engine.ToString()));
        Deselected = new(MapEngines(allEngines.Except(selected)));
    }

    private static IEnumerable<SelectableEngine> MapEngines(IEnumerable<LuckyUrlSearch.Engine> engines)
        => engines.Select(engine => new SelectableEngine { Engine = engine, Label = engine.GetLabel() });

    [RelayCommand]
    private void ToggleEngine(SelectableEngine engine)
    {
        if (Selected.Remove(engine)) Deselected.Add(engine);
        else
        {
            Selected.Add(engine);
            Deselected.Remove(engine);
        }

        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(HasDeselected));
    }

    public record SelectableEngine
    {
        public LuckyUrlSearch.Engine Engine { get; init; }
        public required string Label { get; init; }
    }

    internal static Border Expander(LuckyUrlSearchSettings model)
    {
        DataTemplate itemTemplate = new(() => BndLbl(nameof(SelectableEngine.Label)).Padding(10)
            .BindTapGesture(nameof(ToggleEngineCommand), commandSource: model, parameterPath: "."));

        var selected = new CollectionView
        {
            ItemsSource = model.Selected,
            ItemsLayout = LinearItemsLayout.Horizontal,
            ItemTemplate = itemTemplate,
            CanReorderItems = true
        }.CenterHorizontal();

        var deselected = new CollectionView
        {
            ItemsSource = model.Deselected,
            ItemsLayout = LinearItemsLayout.Horizontal,
            ItemTemplate = itemTemplate
        }.CenterHorizontal();

        var expander = Expndr(Settings.Page.Headline(Glyphs.Lucky + "Lucky event listing address search"),
            Settings.Page.ContextLabel("Used to find the event listing address when adding a new venue by name and city."),
            selected,
            Settings.Page.ContextLabel("✊ Drag engines to ⇆ re-order them, tap one to remove it.")
                .BindVisible(nameof(HasSelected)),
            deselected,
            Settings.Page.ContextLabel("Tap an engine to use it.")
                .BindVisible(nameof(HasDeselected)),
            LbldView("Suffix", Entr(nameof(EditableSuffix), "concerts")).BindVisible(nameof(HasSelected)),
            Settings.Page.ContextLabel("This is automatically appended to the search terms after venue name and city - to find the event listing addresses of venues rather than their home pages.")
                .BindVisible(nameof(HasSelected)));

        expander.BindingContext = model;
        return expander;
    }
}
