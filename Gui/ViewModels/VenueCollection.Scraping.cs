using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FomoCal.Gui.ViewModels;

partial class VenueCollection
{
    private readonly HashSet<Venue> refreshingVenues = [];

    internal event Action<Venue, HashSet<Event>>? EventsScraped;
    [ObservableProperty] public partial double RefreshMultipleVenuesProgress { get; set; } = 1; // none is refreshing

    internal async Task RefreshByNameAsync(string venueName)
    {
        var venue = Observable.SingleOrDefault(v => v.Name == venueName);
        if (venue == null || !CanRefreshVenue(venue)) return;
        await RefreshVenueAsync(venue);
    }

    [RelayCommand(AllowConcurrentExecutions = true, CanExecute = nameof(CanRefreshVenue))]
    private async Task RefreshVenueAsync(Venue venue)
    {
        if (!await HasInternet()) return;

        (List<Exception> errors, string? warning) = await RefreshEvents(venue);
        await SaveVenues();
        RefreshList(); // after SaveVenues to have venue visually refreshed
        if (errors.Count > 0) await WriteErrorReportAsync(ReportErrors(errors, venue));
        if (warning != null) await App.CurrentPage.DisplayAlertAsync("You may want to look into:", warning, "OK");
    }

    internal async Task RefreshMultipleVenuesAsync(Collection<Venue> venues)
    {
        if (!await HasInternet()) return;

        var refreshs = venues.Select(venue => (venue, task: RefreshEvents(venue, totalRefreshing: venues.Count))).ToArray();
        await Task.WhenAll(refreshs.Select(r => r.task));
        RefreshList();
        await SaveVenues();

        var scrapesWithErrors = refreshs.Where(r => r.task.Result.errors.Count > 0).ToArray();

        if (scrapesWithErrors.Length > 0)
        {
            string errorReport = scrapesWithErrors.Select(r => ReportErrors(r.task.Result.errors, r.venue)).Join(ErrorReport.OutputSpacing);
            await WriteErrorReportAsync(errorReport);
        }

        var scrapesWithWarnings = refreshs.Where(r => r.task.Result.warning != null).ToArray();

        if (scrapesWithWarnings.Length > 0)
        {
            string warnings = scrapesWithWarnings.Select(r => r.task.Result.warning).LineJoin();
            await App.CurrentPage.DisplayAlertAsync("You may want to look into:", warnings, "OK");
        }
    }

    private static async ValueTask<bool> HasInternet()
    {
        if (App.HasInternet) return true;

        await App.CurrentPage.DisplayAlertAsync("Connect to the internet and retry.",
            "Loading event listings requires an internet connection.", "OK");

        return false;
    }

    internal bool CanRefreshVenue(Venue? venue) => venue is not null && !IsRefreshing(venue);
    private bool IsRefreshing(Venue venue) => refreshingVenues.Contains(venue);

    private void SetVenueRefreshing(Venue venue, int totalRefreshing, bool isRefreshing)
    {
        if (isRefreshing) refreshingVenues.Add(venue);
        else refreshingVenues.Remove(venue);

        /* refreshing venues count against the progress, i.e. all refreshing => 0, none => 1
         * so that the bar progresses as venues finish refreshing */
        RefreshMultipleVenuesProgress = (totalRefreshing - refreshingVenues.Count) / (double)totalRefreshing;

        RefreshVenueCommand.NotifyCanExecuteChanged();
    }

    private async Task<(List<Exception> errors, string? warning)> RefreshEvents(Venue venue, int totalRefreshing = 1)
    {
        SetVenueRefreshing(venue, totalRefreshing, true);

        try
        {
            (HashSet<Event> events, List<Exception> errors) = await scraper.ScrapeVenueAsync(venue);
            venue.LastRefreshed = DateTime.Now;
            venue.LastEventCount = events.Count;
            var warning = events.Count > 0 || errors.Count > 0 ? null : $"Found no relevant events for {venue.Name}.";
            EventsScraped?.Invoke(venue, events); // notify subscribers
            return (errors, warning);
        }
        finally
        {
            SetVenueRefreshing(venue, totalRefreshing, false);
        }
    }

    private static string ReportErrors(IEnumerable<Exception> errors, Venue venue)
        => errors.Select(ex => ex.ToString())
            .Prepend("Scraping " + venue.Name + " " + venue.ProgramUrl)
            .Join(ErrorReport.OutputSpacing);

    private static async Task WriteErrorReportAsync(string errorReport)
        => await ErrorReport.WriteAsyncAndShare(errorReport, "refreshing venues");
}