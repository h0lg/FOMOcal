using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class Settings : ObservableObject
{
    [ObservableProperty] public partial AppTheme UserTheme { get; set; } = Theme.Get();
    partial void OnUserThemeChanged(AppTheme value) => Theme.Set(value);

    [ObservableProperty] public partial bool ExportTextAlignedWithHeaders { get; set; } = Export.TextAlignedWithHeaders;
    partial void OnExportTextAlignedWithHeadersChanged(bool value) => Export.TextAlignedWithHeaders = value;

    public EventPropertySelection ExportedTextEventFields { get; }
        = new(Export.EventFieldsForText, save: fields => Export.EventFieldsForText = fields);

    public EventPropertySelection ExportedHtmlEventFields { get; }
        = new(Export.EventFieldsForHtml, save: fields => Export.EventFieldsForHtml = fields);

    public partial class Page : ContentPage
    {
        public Page(Settings model)
        {
            BindingContext = model;
            Title = "Settings";

            var htmlExport = EventPropertySelection.Views(model.ExportedHtmlEventFields);
            var exportTextAlignedWithHeaders = Swtch(nameof(ExportTextAlignedWithHeaders)).Wrapper;
            var textExport = EventPropertySelection.Views(model.ExportedTextEventFields);

            const string includedFields = Glyphs.Select + "included fields",
                excludedFields = Glyphs.Deselect + "excluded fields";

            var layout = VStack(5,
                Headline("🎨 Theme"), ThemeSwitches().CenterHorizontal(),

                Expndr(Headline(Glyphs.Html + "HTML export"),
                    ExportSection(includedFields), htmlExport.included,
                    ContextLabel(EventPropertySelection.IncludedInfo),
                    ExportSection(excludedFields),
                    htmlExport.excluded, ContextLabel(EventPropertySelection.ExcludedInfo)),

                Expndr(Headline(Glyphs.Text + "Text export"),
                    HStack(5, Lbl("aligned with headers"), exportTextAlignedWithHeaders).CenterHorizontal(),
                    ContextLabel("whether to column-align the plain text export using spaces and include column headers"),
                    ExportSection(includedFields), textExport.included, ContextLabel(EventPropertySelection.IncludedInfo),
                    ExportSection(excludedFields), textExport.excluded, ContextLabel(EventPropertySelection.ExcludedInfo)),

                Headline("⏱ Browser timing"),
                ContextLabel(
                    "You can tweak the automation engine here if you experience problems, e.g. due to a slow internet connection."
                    + " Tread lightly - footguns ahead!"),
                Expndr(TimingSection("loading 💤 lazy or more"), LoadingLazyOrMore()),
                Expndr(TimingSection("📜 scroll paging"), ScrollPaging()),
                Expndr(TimingSection("↹ swap paging"), SwapPaging()));

            Content = new ScrollView { Content = layout.Center() }
                .Paddings(10, top: 0, 10, 10);
        }

        internal static Task GoHere(INavigation navigation) => navigation.PushAsync(new Page(new Settings()));
        private static Label Headline(string text) => Lbl(text).StyleClass(Styles.Label.Headline);
        private static Label ContextLabel(string text) => Lbl(text).StyleClass(Styles.Label.Demoted).TextCenterHorizontal();
        private static Label TimingSection(string text) => Lbl(text).StyleClass(Styles.Label.SubHeadline);
        private static Label ExportSection(string text) => TimingSection(text).Margins(top: 20);

        private static HorizontalStackLayout ThemeSwitches()
            => HStack(0, ThemeVariantToggle("🌑 dark", AppTheme.Dark, "always use dark theme"),
                ThemeVariantToggle("🌓 switch with OS", AppTheme.Unspecified,
                    "use light or dark depending on the theme variant selected on the operating system level"),
                ThemeVariantToggle("🌕 light", AppTheme.Light, "always use light theme"));

        private static RadioButton ThemeVariantToggle(string label, AppTheme theme, string tooltip)
            => new RadioButton { Content = label, StyleClass = [Styles.RadioButton.SingleSelectToggleButton] }.ToolTip(tooltip)
                .Bind(RadioButton.IsCheckedProperty, nameof(UserTheme),
                    convert: (AppTheme t) => t == theme, convertBack: isChecked => isChecked ? theme : Theme.Get());
    }
}
