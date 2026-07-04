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
            .ConfigureMauiHandlers(_ =>
            {
#if WINDOWS
                Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView",
                    (handler, _) => handler.PlatformView.SingleSelectionFollowsFocus = false);

                /*  hide tick / check mark shown on Windows for CollectionView with SelectionMode Multiple,
                    https://github.com/dotnet/maui/issues/16066#issuecomment-2058487452 */
                Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("DisableMultiselectCheckbox",
                    (handler, _) => handler.PlatformView.IsMultiSelectCheckBoxEnabled = false);
#endif

#if ANDROID
                Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("DisableKeyboardWhenReadOnly", (handler, view) =>
                {
                    if (view is Editor editor && editor.IsReadOnly) // used for read-only but selectable multi-line text
                    {
                        handler.PlatformView.SetTextIsSelectable(true);
                        handler.PlatformView.ShowSoftInputOnFocus = false; // hides keyboard
                    }
                });

                Microsoft.Maui.Handlers.CheckBoxHandler.Mapper.AppendToMapping("AndroidFocusFix", (handler, view) =>
                {
                    // allows taking and holding Focus, required for InlineTooltipOnFocus
                    handler.PlatformView.FocusableInTouchMode = true;

                    // setting FocusableInTouchMode = true causes first tap to be consumed by taking focus
                    handler.PlatformView.FocusChange += (o, e) =>
                    {
                        // toggle IsChecked when taking focus to compensate
                        if (e.HasFocus) view.IsChecked = !view.IsChecked;
                    };
                });

                Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping("AndroidFocusFix", (handler, view) =>
                {
                    // allows taking and holding Focus, required for InlineTooltipOnFocus
                    handler.PlatformView.FocusableInTouchMode = true;

                    // setting FocusableInTouchMode = true causes first tap to be consumed by taking focus
                    handler.PlatformView.FocusChange += (o, e) =>
                    {
                        // toggle IsOn when taking focus to compensate
                        if (e.HasFocus) view.IsOn = !view.IsOn;
                    };
                });
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
            });

        // set up storage
        Directory.CreateDirectory(StoragePath); // Ensure directory exists
        JsonFileStore jsonFileStore = new(StoragePath); // handles raw file read/write and de/serialization
        var fileHelper = new FileHelper();

        ErrorReport.Setup(fileHelper);

        Export.Setup(fileHelper, AppInfo.Name, GetAppVersion(), RepoUrl, FileSystem.CacheDirectory,
            displayActionSheet: (title, c, d, options) => App.CurrentPage.DisplayActionSheetAsync(title, c, d, options));

        ScrapeLogFile.Setup(StoragePath, fileHelper,
            reportError: async (ex, message) => await ErrorReport.WriteAsync(ex.ToString(), message));

        // register JSON file Repositories with file name for each type
        builder.Services.AddSingleton(_ => new SetJsonFileRepository<Venue>(jsonFileStore, "venues"));
        builder.Services.AddSingleton(_ => new EventRepository(jsonFileStore, "events"));
        builder.Services.AddSingleton(_ => new SingletonJsonFileRepository<VenueEditor.SelectorOptions>(jsonFileStore, "selectorOptions"));

        builder.Services.AddSingleton<IBrowser>(new Browser());
        builder.Services.AddSingleton<IBuildEventListingAutomators, MauiEventListingAutomatorFactory>();
        builder.Services.AddSingleton<ISaveScrapeLogFiles, DefaultScrapeLogFileSaver>();
        builder.Services.AddSingleton<Scraper>(); // just to have it disposed of properly by the service provider
        builder.Services.AddSingleton<VenueCollection>();

        // register view models and views
        builder.Services.AddTransient<Settings>();
        builder.Services.AddTransient<Settings.Page>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Services.AddLogging(configure => configure.AddDebug());
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
