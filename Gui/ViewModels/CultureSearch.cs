using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

public partial class PickDateCulturePage : PickerPage<CultureInfo>
{
    internal PickDateCulturePage()
    {
        Title = "Pick the date culture";
        if (Shell.Current != null) Shell.SetTabBarIsVisible(this, false); // to avoid navigating to another tab
        var model = new Model();
        BindingContext = model;
        ToolbarItem useSelected = new(Glyphs.Target + "Use selected culture", null, () => SetResult(model.Selected!)) { IsEnabled = false };
        ToolbarItems.Add(useSelected);
        var itemTemplate = CultureSearch.ItemTemplate(nameof(Model.SelectCultureCommand), tapCommandSource: model);

        model.PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(Model.Selected))
                useSelected.IsEnabled = model.Selected != null;
        };

        Content = VStack(5,
            CultureSearch.Input(model.Search, "search a culture"),
            CultureSearch.Result(model.Search, itemTemplate),
            BndLbl(nameof(Model.Display)).CenterHorizontal().StyleClass(Styles.Label.SubHeadline)
                .BindVisible(nameof(Model.ShowDisplay)),
            Btn("add to preferred date cultures", nameof(Model.AddToPreferredCommand))
                .CenterHorizontal().BindVisible(nameof(Model.ShowDisplay)));
    }

    public partial class Model : ObservableObject
    {
        internal CultureSearch Search { get; } = new CultureSearch();
        public CultureInfo? Selected { get; private set; }
        public string Display => Selected == null ? "select one" : Selected.DisplayName + " selected";
        public bool ShowDisplay => Search.Filtered.Count > 0;

        [RelayCommand]
        public void SelectCulture(CultureInfo culture)
        {
            Selected = culture;
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(ShowDisplay));
            AddToPreferredCommand.NotifyCanExecuteChanged();
        }

        internal bool CanAddToPreferred() => Selected != null && !PreferredDateCultures.Remembered.Contains(Selected);

        [RelayCommand(CanExecute = nameof(CanAddToPreferred))]
        public void AddToPreferred()
        {
            PreferredDateCultures.AddRemembered(Selected!);
            AddToPreferredCommand.NotifyCanExecuteChanged();
        }
    }
}