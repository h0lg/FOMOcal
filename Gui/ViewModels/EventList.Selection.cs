using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    private readonly HashSet<EventView> selected = []; // remembers selection across search filter changes
    private bool suspendSelectionChange; // prevents updates to selected when SelectedEvents changes OnSelectionChanged
    private Action? FixDisplayedSelectedState;

    [ObservableProperty] public partial IList<object> SelectedEvents { get; set; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ToggleViewMode))]
    public partial bool ViewSelectedOnly { get; set; }

    public string ToggleViewMode => ViewSelectedOnly ? "👁 all" : $"👁 {SelectedEventCount} selected";
    public int SelectedEventCount => selected.Count;
    public bool HasSelection => SelectedEventCount > 0;

    /// <summary>Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
    /// for read-only properties downstream of <see cref="SelectedEvents"/> - and for that property itself if
    /// <paramref name="forSelectedEvents"/> is true to notify the CollectionView.</summary>
    private void NotifySelectionChanged(bool forSelectedEvents = true)
    {
        if (forSelectedEvents) OnPropertyChanged(nameof(SelectedEvents));
        OnPropertyChanged(nameof(SelectedEventCount));
        OnPropertyChanged(nameof(HasSelection));
        if (!ViewSelectedOnly) OnPropertyChanged(nameof(ToggleViewMode));
    }

    private async Task DeleteSelectedEventsAsync()
    {
        bool isConfirmed = await App.CurrentPage.DisplayAlertAsync("Confirm Deletion",
            "Are you sure you want to delete the selected events?",
            "Yes", "No");

        if (!isConfirmed) return;

        foreach (var evt in selected)
            allEvents!.Remove(evt);

        selected.Clear();
        SelectedEvents.Clear();
        ViewSelectedOnly = false;
        NotifySelectionChanged();
        await OnEventsUpdated();
    }

    private void SelectFilteredEvents()
    {
        foreach (var evt in FilteredEvents)
            selected.Add(evt);

        ReapplySelection(); // because selected changed
    }

    private void DeselectFilteredEvents()
    {
        foreach (var evt in FilteredEvents)
            selected.Remove(evt);

        SwitchBackToViewingAllIfSelectionIsEmpty();
        ReapplySelection(); // because selected changed
    }

    [RelayCommand]
    private async Task ShareSelectedEventsAsync()
    {
        if (!HasSelection) return;
        ShareEventsPage export = new();
        var format = await export.GetResult(navigation);
        if (format == null) return;
        var events = selected.GetEvents();

        switch (format.Value)
        {
            case ShareEventsPage.Format.iCalendar:
                await events.ExportToIcal();
                break;
            case ShareEventsPage.Format.HTML:
                await events.ExportToHtml();
                break;
            case ShareEventsPage.Format.text:
                await events.ExportToText(Export.TextAlignedWithHeaders);
                break;
            case ShareEventsPage.Format.CSV:
                await events.ExportToCsv();
                break;
            default:
                break;
        }
    }

    /// <summary>Syncs the visible <see cref="SelectedEvents"/> with <see cref="selected"/> after the latter changed.
    /// Restores the remembered selection after <see cref="FilteredEvents"/> changed,
    /// clearing selection of visible elements.</summary>
    private void ReapplySelection()
    {
        SelectedEvents.Clear();

        foreach (var evt in selected)
            if (FilteredEvents.Contains(evt))
                SelectedEvents.Add(evt);

        NotifySelectionChanged();
        FixDisplayedSelectedState?.Invoke();
    }

    /// <summary>Syncs the remembered <see cref="selected"/> events with <see cref="SelectedEvents"/>
    /// after selection changed in the CollectionView - unless <see cref="suspendSelectionChange"/> is true
    /// or the number of selected items has not changed
    /// (e.g. after calling <see cref="NotifySelectionChanged(bool)"/> with true).</summary>
    private void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        if (suspendSelectionChange
            || e.PreviousSelection.Count == e.CurrentSelection.Count) return;

        if (e.PreviousSelection.Count < e.CurrentSelection.Count)
            foreach (var evt in e.CurrentSelection.OfType<EventView>())
                selected.Add(evt);
        else
        {
            foreach (var evt in e.PreviousSelection.OfType<EventView>())
                if (!e.CurrentSelection.Contains(evt))
                    selected.Remove(evt);

            SwitchBackToViewingAllIfSelectionIsEmpty();
        }

        /* SelectedEvents are up to date with e.CurrentSelection here, no need to call ReapplySelection.
         * Don't trigger change for SelectedEvents because we react to the CollectionView here - it doesn't need a call back. */
        NotifySelectionChanged(forSelectedEvents: false);
    }

    partial void OnViewSelectedOnlyChanged(bool value)
    {
        ApplyFilter(); // to switch list source
        ReapplySelection(); // because FilteredEvents changed
    }

    private void SwitchBackToViewingAllIfSelectionIsEmpty()
    {
        if (selected.Count == 0) ViewSelectedOnly = false;
    }

    partial class View
    {
        private static HorizontalStackLayout SelectionMenu(EventList model)
            => HStack(5,
                new Button().Bind(Button.TextProperty, nameof(ToggleViewMode)).BindVisible(nameof(HasSelection))
                    .ToolTip("toggle between viewing all and only selected events")
                    .TapGesture(() => model.ViewSelectedOnly = !model.ViewSelectedOnly)).View;
    }
}
