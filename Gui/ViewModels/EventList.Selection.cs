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
    }

    [RelayCommand]
    private async Task DeleteSelectedEvents()
    {
        foreach (var evt in selected)
            allEvents!.Remove(evt);

        selected.Clear();
        SelectedEvents.Clear();
        NotifySelectionChanged();
        await OnEventsUpdated();
    }

    [RelayCommand]
    private void SelectAllEvents()
    {
        // if all visisble are selected, toggle selection, de-selecting visible
        if (FilteredEvents.All(selected.Contains))
            foreach (var evt in FilteredEvents)
                selected.Remove(evt);
        else // otherwise select all visible
            foreach (var evt in FilteredEvents)
                selected.Add(evt);

        ReapplySelection(); // because selected changed
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
            foreach (var evt in e.PreviousSelection.OfType<EventView>())
                if (!e.CurrentSelection.Contains(evt))
                    selected.Remove(evt);

        /* SelectedEvents are up to date with e.CurrentSelection here, no need to call ReapplySelection.
         * Don't trigger change for SelectedEvents because we react to the CollectionView here - it doesn't need a call back. */
        NotifySelectionChanged(forSelectedEvents: false);
    }

    partial class View
    {
        private static HorizontalStackLayout SelectionMenu()
            => HStack(5,
                Btn("✨ de/select all", nameof(SelectAllEventsCommand))
                    .ToolTip("...events included by the filter in the list below. Or tap and toggle them separately."),
                BndLbl(nameof(SelectedEventCount), stringFormat: "{0} selected").BindVisible(nameof(HasSelection)),
                Btn("🗑", nameof(DeleteSelectedEventsCommand)).BindVisible(nameof(HasSelection))).View;
    }
}
