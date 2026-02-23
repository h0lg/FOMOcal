using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanReload))] public partial bool IsEventPageLoading { get; set; }

    /// <summary>Bound to the editor and eventually committed to <see cref="ProgramUrl"/>.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsEditingProgramUrlValid)), NotifyPropertyChangedFor(nameof(CanReload))]
    public partial string EditingProgramUrl { get; set; }

    public bool IsEditingProgramUrlValid => EditingProgramUrl.IsSignificant() && EditingProgramUrl.IsValidHttpUrl();
    public bool CanReload => IsEditingProgramUrlValid && !IsEventPageLoading;

    public string ProgramUrl
    {
        get => venue.ProgramUrl;
        set
        {
            if (value == venue.ProgramUrl || !value.IsValidHttpUrl()) return;
            venue.ProgramUrl = value; // triggers web view to navigate
            SetDocument(null);
            OnPropertyChanged();
            RevealMore();
        }
    }

    [RelayCommand]
    private static Task OpenUrl(string url) => WebViewPage.OpenUrlAsync(url);

    partial class Page
    {
        private void ProgramUrlControls(out Entry urlEntry, out Label invalidIndicator,
            out ActivityIndicator loadingIndicator, out Button reload, out Button openUrl)
        {
            // bind to a draft model property without property change handler
            urlEntry = Entr(nameof(EditingProgramUrl), placeholder: "Program page URL", Keyboard.Url)
                // commit changes on loss of focus to one that has - to avoid premature URL loading errors
                .OnFocusChanged((_, focused) => { if (!focused) model.ProgramUrl = model.EditingProgramUrl; });

            const string isValidUrl = nameof(IsEditingProgramUrlValid);

            invalidIndicator = Lbl("⚠").ToolTip("This is not a valid HTTP URL.").CenterVertical()
                .BindVisible(isValidUrl, converter: Converters.Not);

            loadingIndicator = new ActivityIndicator { IsRunning = true }
                .BindVisible(new Binding(isValidUrl), Converters.And, new Binding(nameof(IsEventPageLoading)));

            reload = Btn("⟳").TapGesture(Reload).BindVisible(nameof(CanReload));

            openUrl = Btn(Glyphs.Link, nameof(OpenUrlCommand), source: model, parameterPath: nameof(ProgramUrl))
                .BindVisible(isValidUrl);

        }
    }
}
