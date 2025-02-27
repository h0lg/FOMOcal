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

        // Set up the storage path
        var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), nameof(FomoCal));
        Directory.CreateDirectory(storagePath); // Ensure directory exists

        // Register JsonFileStore (handles raw file read/write)
        builder.Services.AddSingleton(_ => new JsonFileStore(storagePath));
        builder.Services.AddSingleton<Scraper>();

        // Register JsonRepository with fileName for each type
        builder.Services.AddSingleton(sp => new JsonFileRepository<Venue>(sp.GetRequiredService<JsonFileStore>(), "venues"));
        builder.Services.AddSingleton(sp => new JsonFileRepository<Event>(sp.GetRequiredService<JsonFileStore>(), "events"));

        // Register ViewModels
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
