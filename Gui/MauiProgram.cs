using System.Reflection;
using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using Microsoft.Extensions.Logging;

namespace FomoCal.Gui;

public static class MauiProgram
{
    internal static string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), nameof(FomoCal));

    private const string RepoOwner = "h0lg", RepoName = Name;
    internal const string Name = "FOMOcal", RepoUrl = $"https://github.com/{RepoOwner}/{RepoName}";

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
        Directory.CreateDirectory(StoragePath); // Ensure directory exists

        // Register JsonFileStore (handles raw file read/write)
        builder.Services.AddSingleton(_ => new JsonFileStore(StoragePath));
        builder.Services.AddSingleton<Scraper>();

        // Register JsonRepository with fileName for each type
        builder.Services.AddSingleton(sp => new JsonFileRepository<Venue>(sp.GetRequiredService<JsonFileStore>(), "venues"));
        builder.Services.AddSingleton(sp => new EventRepository(sp.GetRequiredService<JsonFileStore>(), "events"));

        // Register ViewModels
        builder.Services.AddSingleton<VenueList>();
        builder.Services.AddSingleton<EventList>();
        builder.Services.AddSingleton<VenueList.View>();
        builder.Services.AddSingleton<MainPage>();

#if WINDOWS
        /*  hide tick / check mark shown on Windows for CollectionView with SelectionMode Multiple,
            https://github.com/dotnet/maui/issues/16066#issuecomment-2058487452 */
        Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping(
            "DisableMultiselectCheckbox", (handler, view) => handler.PlatformView.IsMultiSelectCheckBoxEnabled = false);
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
