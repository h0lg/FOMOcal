using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using Microsoft.Extensions.Logging;

namespace FomoCal.Gui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMarkup()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // set up storage
        var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), nameof(FomoCal));
        Directory.CreateDirectory(storagePath); // Ensure directory exists
        JsonFileStore jsonFileStore = new(storagePath); // handles raw file read/write and de/serialization

        // register JSON file Repositories with file name for each type
        builder.Services.AddSingleton(_ => new JsonFileRepository<Venue>(jsonFileStore, "venues"));
        builder.Services.AddSingleton(_ => new EventRepository(jsonFileStore, "events"));

        builder.Services.AddSingleton<Scraper>(); // just to have it disposed of properly by the service provider

        // register view models and views
        builder.Services.AddSingleton<VenueList>();
        builder.Services.AddSingleton<EventList>();
        builder.Services.AddSingleton<VenueList.View>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
