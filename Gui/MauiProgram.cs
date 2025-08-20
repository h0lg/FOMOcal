using System.Reflection;
using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using Microsoft.Extensions.Logging;

namespace FomoCal.Gui;

public static class MauiProgram
{
    internal static string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name);
    internal static readonly string RepoUrl = $"https://github.com/h0lg/{AppInfo.Name}";

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
        Directory.CreateDirectory(StoragePath); // Ensure directory exists
        JsonFileStore jsonFileStore = new(StoragePath); // handles raw file read/write and de/serialization

        // register JSON file Repositories with file name for each type
        builder.Services.AddSingleton(_ => new SetJsonFileRepository<Venue>(jsonFileStore, "venues"));
        builder.Services.AddSingleton(_ => new EventRepository(jsonFileStore, "events"));
        builder.Services.AddSingleton(_ => new SingletonJsonFileRepository<VenueEditor.SelectorOptions>(jsonFileStore, "selectorOptions"));

        builder.Services.AddSingleton<Scraper>(); // just to have it disposed of properly by the service provider

        // register view models and views
        builder.Services.AddSingleton<VenueList>();
        builder.Services.AddSingleton<EventList>();
        builder.Services.AddSingleton<VenueList.View>();
        builder.Services.AddTransient<Settings>();
        builder.Services.AddTransient<Settings.Page>();
        builder.Services.AddSingleton<MainPage>();

#if WINDOWS
        /*  hide tick / check mark shown on Windows for CollectionView with SelectionMode Multiple,
            https://github.com/dotnet/maui/issues/16066#issuecomment-2058487452 */
        Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping(
            "DisableMultiselectCheckbox", (handler, _) => handler.PlatformView.IsMultiSelectCheckBoxEnabled = false);
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    internal static string GetAppVersion()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        // from AssemblyInformationalVersion in csproj, may contain git commit hash
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            // from AssemblyFileVersion in csproj
            ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            // from Version in csproj
            ?? assembly.GetName().Version?.ToString()
            ?? "Unknown";
    }
}
