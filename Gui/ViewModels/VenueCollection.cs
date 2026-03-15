using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

public partial class VenueCollection(SetJsonFileRepository<Venue> repo, Scraper scraper) : ObservableObject
{
    internal event Action<string, string>? Renamed;
    internal event Action<string>? Deleted;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    public ObservableCollection<Venue> Observable { get; } = [];

    internal async Task EditAsync(string venueName, INavigation navigation)
    {
        var venue = Observable.SingleOrDefault(v => v.Name == venueName);
        if (venue == null) return;
        await EditAsync(venue, navigation);
    }

    internal async Task EditAsync(Venue original, INavigation navigation)
    {
        TaskCompletionSource<VenueEditor.Actions?> editing = new();
        Venue edited = original.DeepCopy(); // so that original is not changed by the editor
        VenueEditor model = new(edited, scraper, editing, navigation, Observable);
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

                RefreshList(); // to refresh UI
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

        VenueEditor model = new(added, scraper, adding, navigation, Observable);
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

    internal async Task Import(INavigation navigation, EventRepository eventRepo)
    {
        HashSet<Venue>? imported = await VenueImport.ChooseSourceAndLoadAsync();
        if (imported == null || imported.Count < 1) return;
        VenueImport model = new();
        var page = new VenueImport.Page(model);
        var events = await eventRepo.LoadAllAsync();
        await navigation.PushAsync(page);
        var renames = await model.ImportAsync(Observable, imported, events); // wait for import

        foreach (var rename in renames)
            Renamed?.Invoke(rename.Key, rename.Value);

        await navigation.PopAsync(); // navigate back
        RefreshList();
        await SaveVenues();
    }

    internal void ShareFile() => repo.ShareFile("venues");

    private void RefreshList(IEnumerable<Venue>? venues = null)
        // Ensure UI updates on the main thread
        => MainThread.BeginInvokeOnMainThread(() => Refresh(venues));

    private void Refresh(IEnumerable<Venue>? venues = null)
    {
        venues ??= [.. Observable];
        Observable.Clear();

        foreach (var venue in venues.OrderBy(v => v.Name))
            Observable.Add(venue);
    }

    private Task SaveVenues() => repo.SaveCompleteAsync(Observable.ToHashSet());
}
