using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    [ObservableProperty] public partial IList<object> SelectedEvents { get; set; } = [];
    public int SelectedEventCount => SelectedEvents.Count;
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
        foreach (var view in GetSelected())
            allEvents!.Remove(view);

        SelectedEvents.Clear();
        NotifySelectionChanged();
        await OnEventsUpdated();
    }

    [RelayCommand]
    private void SelectAllEvents()
    {
        if (SelectedEvents.Count == FilteredEvents.Count)
            SelectedEvents.Clear(); // toggle selection, de-selecting all
        else
        {
            SelectedEvents.Clear();

            foreach (var evt in FilteredEvents)
                SelectedEvents.Add(evt);
        }

        NotifySelectionChanged();
    }

    private IEnumerable<EventView> GetSelected() => SelectedEvents.Cast<EventView>();

    partial class View
    {
        private static HorizontalStackLayout SelectionMenu()
            => HStack(5,
                Btn("✨ de/select all", nameof(SelectAllEventsCommand))
                    .ToolTip("Or 👆 tap individual events in the list below to de/select them."),
                BndLbl(nameof(SelectedEventCount), stringFormat: "{0} selected").BindVisible(nameof(HasSelection)),
                Btn("🗑", nameof(DeleteSelectedEventsCommand)).BindVisible(nameof(HasSelection))).View;
    }
}
