using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    private readonly HashSet<EventView> selected = []; // remembers selection across search filter changes

    [ObservableProperty,
        NotifyPropertyChangedFor(nameof(EventCounters)),
        NotifyPropertyChangedFor(nameof(SelectedEventCounters))]
    public partial bool ViewSelectedOnly { get; set; }

    public string EventCounters =>
        !ViewSelectedOnly && FilteredEvents.Count < allEvents?.Count // has filtered all events view
            ? $"{FilteredEvents.Count} of {allEvents?.Count}"
            : $"{allEvents?.Count ?? 0} total";

    public string SelectedEventCounters =>
        ViewSelectedOnly && FilteredEvents.Count < SelectedEventCount // has filtered selected only view
            ? $"{FilteredEvents.Count} of {SelectedEventCount} selected"
            : $"{SelectedEventCount} selected";

    public int SelectedEventCount => selected.Count;
    public bool HasSelection => SelectedEventCount > 0;

    private bool ToggleSelected(EventView item)
    {
        bool wasSelected = selected.Contains(item);
        item.IsSelected = !wasSelected;

        if (wasSelected)
        {
            selected.Remove(item);
            SwitchBackToViewingAllIfSelectionIsEmpty();
        }
        else selected.Add(item);

        NotifySelectionChanged();
        return !wasSelected; // return current, flipped selection state
    }

    /// <summary>Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
    /// for read-only properties downstream of <see cref="selected"/>.</summary>
    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedEventCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedEventCounters));
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
        ViewSelectedOnly = false;
        await OnEventsUpdated(); // calls NotifySelectionChanged
    }

    private void SelectFilteredEvents()
    {
        foreach (var evt in FilteredEvents)
        {
            evt.IsSelected = true;
            selected.Add(evt);
        }

        NotifySelectionChanged(); // because selected changed
    }

    private void DeselectFilteredEvents()
    {
        foreach (var evt in FilteredEvents)
        {
            evt.IsSelected = false;
            selected.Remove(evt);
        }

        SwitchBackToViewingAllIfSelectionIsEmpty();
        NotifySelectionChanged(); // because selected changed
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
                await events.ExportToHtml([.. ExportSettings.EventFieldsForHtml]);
                break;
            case ShareEventsPage.Format.text:
                await events.ExportToText([.. ExportSettings.EventFieldsForText], ExportSettings.TextAlignedWithHeaders);
                break;
            case ShareEventsPage.Format.CSV:
                await events.ExportToCsv();
                break;
            default:
                break;
        }
    }

    partial void OnViewSelectedOnlyChanged(bool value)
    {
        ApplyFilter(); // to switch list source
        NotifySelectionChanged(); // because FilteredEvents changed
    }

    private void SwitchBackToViewingAllIfSelectionIsEmpty()
    {
        if (selected.Count == 0) ViewSelectedOnly = false;
    }

    partial class View
    {
        private static Border SelectionMenu() => new()
        {
            StyleClass = [nameof(SelectionMenu)],
            Content = HStack(0,
                BndLbl(nameof(EventCounters)).Margins(right: 2).TextCenterVertical(),
                Swtch(nameof(ViewSelectedOnly)).Wrapper.BindVisible(nameof(HasSelection))
                    .ToolTip("toggle between viewing all and only selected events"),
                HStack(5, BndLbl(nameof(SelectedEventCounters)),
                    Btn(Glyphs.Export + " Share", nameof(ShareSelectedEventsCommand)).ToolTip("share selected events"))
                    .BindVisible(nameof(HasSelection)),
                Lbl(" - tap an event to select it")
                    .StyleClass(Styles.Label.Demoted).TextCenterVertical().Height(44)
                    .BindVisible(nameof(HasSelection), converter: Converters.Not))
        };

        private static void ToggleSelected(EventView item, EventList model)
        {
            if (item == null) return;
            model.ToggleSelected(item);
        }
    }
}

public static class Selection
{
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.CreateAttached(
        "IsSelected", typeof(bool), typeof(Selection), false, propertyChanged: OnIsSelectedChanged);

    public static bool GetIsSelected(BindableObject view) =>
        (bool)view.GetValue(IsSelectedProperty);

    public static void SetIsSelected(BindableObject view, bool value) =>
        view.SetValue(IsSelectedProperty, value);

    private static void OnIsSelectedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement ve)
        {
            var isSelected = (bool)newValue;
            VisualStateManager.GoToState(ve, isSelected ? VisualStateManager.CommonStates.Selected : VisualStateManager.CommonStates.Normal);
        }
    }
}
