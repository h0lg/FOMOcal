using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

public partial class VenueCollection(SetJsonFileRepository<Venue> repo, Scraper scraper) : ObservableObject
{
    internal event Action<string, string>? Renamed;
    internal event Action<string>? Deleted;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    public ObservableCollection<Venue> Observable { get; } = [];

    internal async Task EditAsync(Venue original, INavigation navigation)
    {
        TaskCompletionSource<VenueEditor.Actions?> editing = new();
        Venue edited = original.DeepCopy(); // so that original is not changed by the editor
        VenueEditor model = new(edited, scraper, editing);
        await navigation.PushAsync(new VenueEditor.Page(model));
        VenueEditor.Actions? result = await editing.Task; // wait for editor
        if (result == null) return; // canceled & navigation stack popped, do nothing

        switch (result)
        {
            case VenueEditor.Actions.Saved:
                Observable.Remove(original);
                Observable.Add(edited);
                await SaveVenues();

                if (original.Name != edited.Name)
                    Renamed?.Invoke(original.Name, edited.Name); // notify subscribers

                await LoadAsync(); // to refresh UI
                break;

            case VenueEditor.Actions.Deleted:
                await DeleteAsync(original);
                break;
        }

        await navigation.PopAsync(); // navigate back
    }

    internal async Task AddAsync(INavigation navigation)
    {
        TaskCompletionSource<VenueEditor.Actions?> adding = new();

        Venue added = new()
        {
            Name = "",
            ProgramUrl = "",
            Event = new() { Selector = "", Name = new(), Date = new() }
        };

        VenueEditor model = new(added, scraper, adding);
        await navigation.PushAsync(new VenueEditor.Page(model));
        VenueEditor.Actions? result = await adding.Task; // wait for editor
        if (result == null) return;  // canceled & navigation stack popped, do nothing

        switch (result)
        {
            case VenueEditor.Actions.Saved:
                Observable.Add(added);
                await SaveVenues();
                break;

            case VenueEditor.Actions.Deleted:
                break;
        }

        await navigation.PopAsync(); // navigate back
    }

    internal async Task DeleteAsync(Venue venue)
    {
        bool isConfirmed = await App.CurrentPage.DisplayAlertAsync("Confirm Deletion",
            $"Are you sure you want to delete the venue {venue.Name}?",
            "Yes", "No");

        if (!isConfirmed) return;
        Observable.Remove(venue);
        Deleted?.Invoke(venue.Name); // notify subscribers
        await SaveVenues();
    }

    internal async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var venues = await repo.LoadAllAsync();
            Refresh(venues);
        }
        catch (Exception ex)
        {
            await ErrorReport.WriteAsyncAndShare(ex.ToString(), "loading venues");
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal void Refresh(IEnumerable<Venue>? venues = null)
    {
        venues ??= [.. Observable];
        Observable.Clear();

        // order unscraped (new) venues on top, then by latest refresh
        foreach (var venue in venues.OrderByDescending(v => v.LastRefreshed ?? DateTime.Now))
            Observable.Add(venue);
    }

    internal Task SaveVenues() => repo.SaveCompleteAsync(Observable.Migrate().ToHashSet());
    internal void ShareFile() => repo.ShareFile("venues");
}
