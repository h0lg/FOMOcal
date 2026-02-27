using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanReload))] public partial bool IsEventPageLoading { get; set; }

    /// <summary>Bound to the editor and eventually committed to <see cref="ProgramUrl"/>.</summary>
    [ObservableProperty,
        NotifyPropertyChangedFor(nameof(IsEditingProgramUrlValid)),
        NotifyPropertyChangedFor(nameof(CanLoadValidUrl)),
        NotifyPropertyChangedFor(nameof(CanReload))]
    public partial string EditingProgramUrl { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanLoadValidUrl))]
    public partial bool HasInternet { private get; set; } = App.HasInternet;

    public bool IsEditingProgramUrlValid => EditingProgramUrl.IsSignificantValidUrl();
    public bool CanReload => IsEditingProgramUrlValid && !IsEventPageLoading;
    public bool CanLoadValidUrl => HasInternet && IsEditingProgramUrlValid;

    public string ProgramUrl
    {
        get => venue.ProgramUrl;
        private set
        {
            if (value == venue.ProgramUrl) return;
            venue.ProgramUrl = value;
            SetDocument(null);
            OnPropertyChanged(); // triggers web view to navigate
            RevealMore();
        }
    }

    private async ValueTask CommitEditingProgramUrlAsync()
    {
        // only skip if both are the same and valid URLs - to enable searching previously saved draft
        if (EditingProgramUrl == ProgramUrl && ProgramUrl.IsSignificantValidUrl()) return;

        if (EditingProgramUrl.IsNullOrWhiteSpace())
        {
            ProgramUrl = EditingProgramUrl;
            return;
        }

        if (EditingProgramUrl.IsDomainLike(out var validUrl))
        {
            EditingProgramUrl = ProgramUrl = validUrl!;
            return;
        }

        if (App.HasInternet)
        {
            string? luckyUrl = null;
            LuckyUrlSearch.Engine? luckyEngine = null;
            LuckyUrlSearch.Engine[] engines = [.. LuckyUrlSearchSettings.Engines];
            string query = EditingProgramUrl + " " + LuckyUrlSearchSettings.Suffix;

            foreach (var engine in engines)
            {
                luckyUrl = await LuckyUrlSearch.TryAsync(query, engine);

                if (luckyUrl != null)
                {
                    luckyEngine = engine;
                    break;
                }
            }

            var chosenUrl = await PickUrlAsync(EditingProgramUrl, luckyUrl, luckyEngine, engines);
            if (chosenUrl != null) EditingProgramUrl = ProgramUrl = chosenUrl!;
            else ProgramUrl = EditingProgramUrl; // commit even if nothing was found or chosen to enable saving drafts
        }
        else ProgramUrl = EditingProgramUrl;
    }

    private static string LabelSearch(LuckyUrlSearch.Engine engine, string originalQuery)
        => $"🔎 Search \"{originalQuery}\" with {engine.GetLabel()}";

    private async Task<string?> PickUrlAsync(string originalQuery,
        string? suggestedUrl, LuckyUrlSearch.Engine? luckyEngine,
        LuckyUrlSearch.Engine[] triedLuckyEngines)
    {
        const string previewSuggested = "👁 Preview suggested URL",
            cancel = "🚫 Neither, let me rety",
            openSettings = $"{Glyphs.Settings} Configure {Glyphs.Lucky}lucky search";

        string useSuggested = $"{Glyphs.Target}Use suggested URL",
            google = LabelSearch(LuckyUrlSearch.Engine.Google, originalQuery),
            duckDuckGo = LabelSearch(LuckyUrlSearch.Engine.DuckDuckGo, originalQuery);

        string title;
        var options = new List<string>();

        if (suggestedUrl.IsSignificant())
        {
            title = $"{luckyEngine!.Value.GetLabel()} suggests {suggestedUrl}";
            options.Add(useSuggested);
            options.Add(previewSuggested);
        }
        else
        {
            title = triedLuckyEngines.Length == 0
                ? $"That's not a web address and you have no {Glyphs.Lucky}lucky search engines selected."
                : $"That's not a web address and your selected {Glyphs.Lucky}lucky search engines didn't suggest anything.";

            options.Add(openSettings);
        }

        options.Add(duckDuckGo);
        options.Add(google);
        var choice = await App.CurrentPage.DisplayActionSheetAsync(title, cancel, null, [.. options]);
        if (choice == cancel || choice == null) return null;
        if (choice == useSuggested) return suggestedUrl;

        if (choice == openSettings)
        {
            await Settings.Page.GoHere(navigation);
            return null;
        }

        string previewUrl = choice == previewSuggested ? suggestedUrl!
            : choice == google ? "https://www.google.com/search?q=" + Uri.EscapeDataString(originalQuery)
            : choice == duckDuckGo ? "https://duckduckgo.com/?q=" + Uri.EscapeDataString(originalQuery)
            : throw new NotImplementedException(nameof(choice));

        return await PickUrlFromBrowser(previewUrl);
    }

    private async Task RepickUrlAsync()
    {
        var chosenUrl = await PickUrlFromBrowser(ProgramUrl);
        if (chosenUrl != null) EditingProgramUrl = ProgramUrl = chosenUrl!;
    }

    private Task<string?> PickUrlFromBrowser(string url)
        => new PickUrlPage(url).GetResult(navigation);

    partial class Page
    {
        private void ProgramUrlControls(out Editor urlEditor, out Label invalidIndicator,
            out ActivityIndicator loadingIndicator, out Button reload, out Button openUrl,
            out Label noInternetIndicator)
        {
            // bind to a draft model property without property change handler
            urlEditor = Edtr(nameof(EditingProgramUrl),
                placeholder: $"enter event listing web address - or {Glyphs.Lucky} lucky search it by venue name and city", Keyboard.Url)
                // commit changes on loss of focus to one that has - to avoid premature URL loading errors
                .OnFocusChanged(async (_, focused) =>
                {
                    if (!focused)
                    {
                        await Task.Delay(50); // for OnDisappearing to SetActionTaken via SignalCancelation if that's the reason 

                        // prevent committing after navigating back
                        if (!model.HasTakenAction())
                            await model.CommitEditingProgramUrlAsync();
                    }
                });

            const string isValidUrl = nameof(IsEditingProgramUrlValid);

            invalidIndicator = ErrorLbl("This is not a valid HTTP/S URL.")
                .BindVisible(isValidUrl, converter: Converters.Not);

            loadingIndicator = new ActivityIndicator { IsRunning = true }
                .BindVisible(new Binding(isValidUrl), Converters.And, new Binding(nameof(IsEventPageLoading)));

            reload = Btn("⟳").TapGesture(async () => await ReloadAsync()).BindVisible(nameof(CanReload));
            const string canLoad = nameof(CanLoadValidUrl);

            openUrl = Btn(Glyphs.Link).BindVisible(canLoad)
                .TapGesture(async () => await model.RepickUrlAsync());

            noInternetIndicator = ErrorLbl("No internet access.")
                .BindVisible(new Binding(isValidUrl), Converters.And, new Binding(canLoad, converter: Converters.Not));
        }
    }
}
