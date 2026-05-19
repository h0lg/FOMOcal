using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public ObservableCollection<CultureSearch.CultureView> Selected { get; }
    public bool HasSelection => Selected.Any();
    public CultureSearch Search { get; }

    internal PreferredDateCultures()
    {
        var selected = Remembered.ToArray();
        Search = new CultureSearch(selectsSingle: false, selected);
        Selected = new(Search.GetSelected());

        Search.CultureToggled += culture =>
        {
            if (culture.Selected) Selected.Add(culture);
            else Selected.Remove(culture);

            OnPropertyChanged(nameof(HasSelection));
            remembered.Set(Selected.Select(c => c.Culture.Name));
        };
    }

    internal static Border Expander(PreferredDateCultures model)
    {
        var selected = new CollectionView
        {
            ItemsSource = model.Selected,
            ItemsLayout = LinearItemsLayout.Horizontal,
            ItemTemplate = model.Search.ItemTemplate,
            CanReorderItems = true
        }.CenterHorizontal();

        Border expander = Expndr(Settings.Page.Headline(Glyphs.Date + "Preferred date cultures"),
            Settings.Page.ContextLabel(HelpTexts.PreferredDateCultures),
            selected,
            Settings.Page.ContextLabel("✊ Drag cultures to ⇆ re-order them, tap one to remove it.")
                .BindVisible(nameof(HasSelection)),
            CultureSearch.Input(model.Search, "search cultures to add"),
            CultureSearch.Result(model.Search));

        expander.BindingContext = model;
        return expander;
    }
}
