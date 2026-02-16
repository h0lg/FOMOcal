using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FomoCal.Gui.Resources;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    public bool SaveScrapeLogs
    {
        get => venue.SaveScrapeLogs;
        set
        {
            if (value == venue.SaveScrapeLogs) return;
            venue.SaveScrapeLogs = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ScrapeLogFile.ForVenue> ScrapeLogs { get; }
    [ObservableProperty] public partial bool ShowBrowserLog { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> BrowserLog { get; set; } = [];

    [RelayCommand]
    private static Task OpenScrapeLog(ScrapeLogFile.ForVenue log) => ScrapeLogFile.Open(log);

    [RelayCommand]
    private void DeleteScrapeLog(ScrapeLogFile.ForVenue log)
    {
        ScrapeLogFile.Remove(log);
        ScrapeLogs!.Remove(log);
    }

    partial class Page
    {
        private static FlexLayout ScrapeLogs(VenueEditor model)
        {
            var save = Swtch(nameof(SaveScrapeLogs));
            save.Switch.ToolTip(HelpTexts.SaveScrapLogs);

            DataTemplate itemTemplate = new(() =>
            {
                var deleteBtn = Lbl(Glyphs.Delete).BindTapGesture(nameof(DeleteScrapeLogCommand),
                    commandSource: model, parameterPath: ".");

                var label = BndLbl(nameof(ScrapeLogFile.ForVenue.TimeStamp)).Padding(10)
                    .BindTapGesture(nameof(OpenScrapeLogCommand), commandSource: model,
                        parameterPath: ".");

                return HStack(5, deleteBtn, label);
            });

            var logs = new CollectionView
            {
                ItemsSource = model.ScrapeLogs,
                ItemsLayout = LinearItemsLayout.Horizontal,
                ItemTemplate = itemTemplate
            }
                .ToolTip("Tap any log to open it.");

            return HWrap(5, Lbl("📜 Scrape logs").Bold(), Lbl("save"), save.Wrapper, logs).View;
        }

        private static FlexLayout ScriptLog(VenueEditor model)
        {
            var toggle = Swtch(nameof(ShowBrowserLog));
            toggle.Switch.ToolTip("View the browser log during the configuration process, e.g. to debug it.");

            var log = new CollectionView
            {
                ItemsSource = model.BrowserLog,
                ItemTemplate = new DataTemplate(() => BndLbl())
            }
                .BindVisible(nameof(ShowBrowserLog));

            return HWrap(5, Lbl("📨 Browser log").Bold(), toggle.Wrapper, log).View;
        }
    }
}
