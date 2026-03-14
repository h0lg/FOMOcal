using CommunityToolkit.Maui.Markup;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class ShareEventsPage : PickerPage<ShareEventsPage.Format?>
{
    internal ShareEventsPage()
    {
        Title = Glyphs.Export + " Share selected events";
        if (Shell.Current != null) Shell.SetNavBarIsVisible(this, true); // to show Title and Back Button for canceling

        const string configurableInSettings = "\nConfigure included event properties in the 🛠 Settings.";

        ExportOption[] options = [
            new(Format.iCalendar, Glyphs.Date + "ics", "...for calendar apps in iCalendar format."),
            new(Format.HTML, Glyphs.Html + "html",
                "...as a rich HTML document to open and filter in a browser."
                + "\nProbably the most end-user friendly option." + configurableInSettings),
            new(Format.text, Glyphs.Text + "text",
                "...as a plain text with configurable alignment."
                + "\nAn easily digestable format without frills or noise, e.g. for text messages."
                + " Also your best choice if you want to re-format the events in a text editor before sharing."
                + configurableInSettings),
            new(Format.CSV, "▦ csv", "...as a table for spreadsheet apps in comma-separated value CSV format.")];

        DataTemplate optionTemplate = new(() => RoundedSection(VStack(5,
            BndLbl(nameof(ExportOption.Title)).StyleClass(Styles.Label.Headline),
            BndLbl(nameof(ExportOption.Description)).StyleClass(Styles.Label.Demoted).TextCenterHorizontal())));

        BindingContext = options;

        CollectionView list = new()
        {
            ItemsSource = options,
            ItemTemplate = optionTemplate,
            SelectionMode = SelectionMode.Single
        };

        list.SelectionChanged += (o, e) =>
        {
            var option = (ExportOption)list.SelectedItem;
            SetResult(option.Format);
        };

        Content = new Border
        {
            Padding = 5,
            MaximumWidthRequest = 360,
            StrokeThickness = 0,
            Content = list
        }.Center();
    }

    private class ExportOption(Format format, string title, string description)
    {
        public Format Format { get; } = format;
        public string Title { get; } = title;
        public string Description { get; } = description;
    }

    public enum Format { iCalendar, HTML, text, CSV }
}
