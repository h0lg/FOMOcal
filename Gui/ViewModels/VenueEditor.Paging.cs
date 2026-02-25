using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.Input;
using FomoCal.Gui.Resources;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    public string? NextEventPageSelector
    {
        get => venue.Event.NextPageSelector;
        set
        {
            if (value == venue.Event.NextPageSelector) return;
            venue.Event.NextPageSelector = value;
            OnPropertyChanged();
        }
    }

    public List<Venue.PagingStrategy> PagingStrategies { get; } = [.. Enum.GetValues<Venue.PagingStrategy>()];

    private bool CanLoadMore()
        => SelectedEventCount > 0
            && venue.Event.PagingStrategy != Venue.PagingStrategy.AllOnOne
            && programDocument?.CanLoadMore(venue) == true;

    [RelayCommand(CanExecute = nameof(CanLoadMore))]
    private async Task LoadMoreAsync(AutomatedEventPageView loader)
        => await scraper.LoadMoreAsync(loader, venue, programDocument!);

    partial class Page
    {
        private FlexLayout PagingControls((Label label, Border layout) help)
        {
            Picker pagingStrategy = new()
            {
                ItemsSource = model!.PagingStrategies.ConvertAll(e => e.GetDescription()),
                SelectedIndex = model.PagingStrategies.IndexOf(model.venue.Event.PagingStrategy)
            };

            pagingStrategy.OnFocusChanged(async (_, focused) => await SyncPagingStrategyHelp(focused));

            pagingStrategy.SelectedIndexChanged += async (s, e) =>
            {
                if (pagingStrategy.SelectedIndex >= 0)
                {
                    model.venue.Event.PagingStrategy = model.PagingStrategies[pagingStrategy.SelectedIndex];
                    model.LoadMoreCommand.NotifyCanExecuteChanged();
                    await SyncPagingStrategyHelp(focused: true);
                }
                else await SyncPagingStrategyHelp(focused: false);
            };

            var nextPageSelector = SelectorInput(
                Edtr(nameof(NextEventPageSelector)).Placeholder("next page")
                    .InlineTooltipOnFocus(HelpTexts.NextEventPageSelector, help,
                        cancelFocusChanged: (vis, focused) => !focused && model.visualSelectorHost == vis),
                pickRelativeTo: () => (selector: "body", pickDescendant: true))
                .BindVisible(nameof(Picker.SelectedIndex), pagingStrategy,
                    Converters.Predicate<int>(i => model.PagingStrategies[i].RequiresNextPageSelector()));

            var test = Btn("▶", nameof(LoadMoreCommand), parameterSource: pageView).ToolTip(HelpTexts.TestPagingStrategy);
            return HWrap(5, Lbl("Loading").Bold(), pagingStrategy, nextPageSelector, test).View;

            Task SyncPagingStrategyHelp(bool focused) =>
                help.InlineHelpTextAsync(model.venue.Event.PagingStrategy.GetHelp()!, pagingStrategy, focused);
        }
    }
}
