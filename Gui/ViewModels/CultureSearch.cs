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
    private readonly bool selectsSingle;

    private readonly ImmutableList<CultureView> availableCultures
        = [.. CultureInfo.GetCultures(CultureTypes.AllCultures).OrderBy(c => c.DisplayName).Select(c => new CultureView(c))];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    public ObservableCollection<CultureView> Filtered { get; } = [];
    internal DataTemplate ItemTemplate { get; }

    /// <summary>Fired when a <see cref="CultureView"/> was toggled</summary>
    internal event Action<CultureView>? CultureToggled;

    internal CultureSearch(bool selectsSingle, CultureInfo[]? selected = null)
    {
        this.selectsSingle = selectsSingle;

        if (selected != null) // init selection state
            foreach (var culture in availableCultures)
                if (selected.Contains(culture.Culture))
                    culture.Selected = true;

        ItemTemplate = new DataTemplate(() =>
            BndLbl(nameof(CultureView.DisplayName)).Padding(10)
                .StyleClass(Styles.VisualElement.SelectableListItem)
                .Bind(Selection.IsSelectedProperty, nameof(CultureView.Selected))
                .BindTapGesture(nameof(ToggleSelectedCommand), commandSource: this, parameterPath: "."));
    }

    internal IEnumerable<CultureView> GetSelected() => availableCultures.Where(c => c.Selected);

    partial void OnSearchTextChanged(string value)
    {
        Filtered.Clear();
        if (value.IsNullOrWhiteSpace()) return;
        var terms = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;

        var filtered = availableCultures.Where(c =>
            c.Culture.Name.ContainsAll(terms, comparison) ||
            c.Culture.NativeName.ContainsAll(terms, comparison) ||
            c.Culture.EnglishName.ContainsAll(terms, comparison) ||
            c.DisplayName.ContainsAll(terms, comparison));

        foreach (var c in filtered)
            Filtered.Add(c);
    }

    [RelayCommand]
    public void ToggleSelected(CultureView culture)
    {
        // selecting one - deselect all other selected
        if (selectsSingle && !culture.Selected)
            foreach (var other in availableCultures)
                if (other.Selected)
                    Toggle(other);

        Toggle(culture);
    }

    private void Toggle(CultureView culture)
    {
        culture.Selected = !culture.Selected;
        CultureToggled?.Invoke(culture);
    }

    internal static SearchBar Input(CultureSearch model, string placeholder)
        => new SearchBar() { Placeholder = placeholder }.CenterHorizontal()
            .Bind(SearchBar.TextProperty, nameof(SearchText), source: model);

    internal static FlexLayout Result(CultureSearch model)
        => HWrap().View.ItemsSource(model.Filtered).ItemTemplate(model.ItemTemplate);

    public partial class CultureView(CultureInfo culture) : ObservableObject
    {
        public CultureInfo Culture => culture;
        public string DisplayName => culture.DisplayName;
        [ObservableProperty] public partial bool Selected { get; set; }
    }
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

        model.PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(Model.Selected))
                useSelected.IsEnabled = model.Selected != null;
        };

        var addToPreferred = new Button().CenterHorizontal()
            .Bind(Button.TextProperty, nameof(Model.AddToPreferredLabel))
            .BindCommand(nameof(Model.AddToPreferredCommand))
            .BindVisible(nameof(Model.AddToPreferredShows));

        Content = VStack(5,
            CultureSearch.Input(model.Search, "search a culture"),
            CultureSearch.Result(model.Search),
            addToPreferred);
    }

    public partial class Model : ObservableObject
    {
        internal CultureSearch Search { get; } = new CultureSearch(selectsSingle: true);
        public CultureInfo? Selected { get; private set; }
        public string AddToPreferredLabel => $"add {Selected?.DisplayName ?? "selected"} to preferred date cultures";
        public bool AddToPreferredShows => Selected != null && !PreferredDateCultures.Remembered.Contains(Selected);

        public Model()
        {
            Search.CultureToggled += (culture) =>
            {
                Selected = culture.Selected ? culture.Culture : null;
                OnPropertyChanged(nameof(Selected));
                OnPropertyChanged(nameof(AddToPreferredLabel));
                OnPropertyChanged(nameof(AddToPreferredShows));
                AddToPreferredCommand.NotifyCanExecuteChanged();
            };
        }

        internal bool CanAddToPreferred() => AddToPreferredShows;

        [RelayCommand(CanExecute = nameof(CanAddToPreferred))]
        public void AddToPreferred()
        {
            PreferredDateCultures.AddRemembered(Selected!);
            AddToPreferredCommand.NotifyCanExecuteChanged();
        }
    }
}