using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FomoCal.Gui.Resources;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class PreferredDateCultures : ObservableObject
{
    private static readonly RememberedStrings remembered = new("PreferredDateCultures.remembered", "📆");
    internal static IEnumerable<CultureInfo> Remembered => remembered.Get().Select(name => new CultureInfo(name));

    private readonly ImmutableList<CultureInfo> availableCultures
        = [.. CultureInfo.GetCultures(CultureTypes.AllCultures).OrderBy(c => c.DisplayName)];

    public ObservableCollection<CultureInfo> Selected { get; }
    public ObservableCollection<CultureInfo> Filtered { get; } = [];
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    public bool HasSelection => Selected.Any();

    partial void OnSearchTextChanged(string value)
    {
        Filtered.Clear();
        if (value.IsNullOrWhiteSpace()) return;
        var comparison = StringComparison.OrdinalIgnoreCase;
        var terms = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var filtered = availableCultures.Where(c =>
            c.Name.ContainsAll(terms, comparison) ||
            c.NativeName.ContainsAll(terms, comparison) ||
            c.EnglishName.ContainsAll(terms, comparison) ||
            c.DisplayName.ContainsAll(terms, comparison));

        foreach (var c in filtered)
            Filtered.Add(c);
    }

    internal PreferredDateCultures()
    {
        Selected = new(Remembered);
        Selected.CollectionChanged += (o, e) => remembered.Set(Selected.Select(c => c.Name));
    }

    [RelayCommand]
    public void ToggleCulture(CultureInfo culture)
    {
        if (!Selected.Remove(culture))
            Selected.Add(culture);

        OnPropertyChanged(nameof(HasSelection));
    }

    internal static Border Expander(PreferredDateCultures model)
    {
        DataTemplate itemTemplate = new(() => BndLbl(nameof(CultureInfo.DisplayName)).Padding(10)
            .BindTapGesture(nameof(ToggleCultureCommand), commandSource: model, parameterPath: "."));

        var selected = new CollectionView
        {
            ItemsSource = model.Selected,
            ItemsLayout = LinearItemsLayout.Horizontal,
            ItemTemplate = itemTemplate,
            CanReorderItems = true
        }.CenterHorizontal();

        Border expander = Expndr(Settings.Page.Headline(Glyphs.Date + "Preferred date cultures"),
            Settings.Page.ContextLabel(HelpTexts.PreferredDateCultures),
            selected,
            Settings.Page.ContextLabel("✊ Drag cultures to ⇆ re-order them, tap one to remove it.")
                .BindVisible(nameof(HasSelection)),
            new SearchBar() { Placeholder = "search cultures to add" }.CenterHorizontal()
                .Bind(SearchBar.TextProperty, nameof(SearchText)),
            HWrap().View.ItemsSource(model.Filtered).ItemTemplate(itemTemplate));

        expander.BindingContext = model;
        return expander;
    }
}
