using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class CultureSearch : ObservableObject
{
    private readonly ImmutableList<CultureInfo> availableCultures
        = [.. CultureInfo.GetCultures(CultureTypes.AllCultures).OrderBy(c => c.DisplayName)];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    public ObservableCollection<CultureInfo> Filtered { get; } = [];

    partial void OnSearchTextChanged(string value)
    {
        Filtered.Clear();
        if (value.IsNullOrWhiteSpace()) return;
        var terms = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;

        var filtered = availableCultures.Where(c =>
            c.Name.ContainsAll(terms, comparison) ||
            c.NativeName.ContainsAll(terms, comparison) ||
            c.EnglishName.ContainsAll(terms, comparison) ||
            c.DisplayName.ContainsAll(terms, comparison));

        foreach (var c in filtered)
            Filtered.Add(c);
    }

    internal static DataTemplate ItemTemplate(string tapCommand, object tapCommandSource)
        => new(() => BndLbl(nameof(CultureInfo.DisplayName)).Padding(10)
            .BindTapGesture(tapCommand, commandSource: tapCommandSource, parameterPath: "."));

    internal static SearchBar Input(CultureSearch model, string placeholder)
        => new SearchBar() { Placeholder = placeholder }.CenterHorizontal()
            .Bind(SearchBar.TextProperty, nameof(SearchText), source: model);

    internal static FlexLayout Result(CultureSearch model, DataTemplate itemTemplate)
        => HWrap().View.ItemsSource(model.Filtered).ItemTemplate(itemTemplate);
}
