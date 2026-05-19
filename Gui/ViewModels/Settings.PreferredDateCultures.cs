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

    internal static void AddRemembered(CultureInfo culture)
    {
        var memory = remembered.Get();
        if (memory.Contains(culture.Name)) return;
        remembered.Set(memory.Append(culture.Name));
    }

    public ObservableCollection<CultureInfo> Selected { get; }
    public bool HasSelection => Selected.Any();
    public CultureSearch Search { get; } = new CultureSearch();

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
        DataTemplate itemTemplate = CultureSearch.ItemTemplate(tapCommand: nameof(ToggleCultureCommand), tapCommandSource: model);

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
            CultureSearch.Input(model.Search, "search cultures to add"),
            CultureSearch.Result(model.Search, itemTemplate));

        expander.BindingContext = model;
        return expander;
    }
}
