using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
                    HStack(5, Lbl("aligned with headers"), exportTextAlignedWithHeaders).View.CenterHorizontal(),
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
                ThemeVariantToggle("🌕 light", AppTheme.Light, "always use light theme")).View;

        private static RadioButton ThemeVariantToggle(string label, AppTheme theme, string tooltip)
            => new RadioButton { Content = label, StyleClass = [Styles.RadioButton.SingleSelectToggleButton] }.ToolTip(tooltip)
                .Bind(RadioButton.IsCheckedProperty, nameof(UserTheme),
                    convert: (AppTheme t) => t == theme, convertBack: isChecked => isChecked ? theme : Theme.Get());
    }
}

public partial class EventPropertySelection
{
    internal const string IncludedInfo = "✊ Drag event fields to ⇆ re-order them, 👇 tap one to exclude it from the export.",
        ExcludedInfo = "👆 Tap an event field to include it in the export.";

    public ObservableCollection<PropertyInfo> ExportedFields { get; }
    public ObservableCollection<PropertyInfo> AvailableFields { get; }

    public EventPropertySelection(IEnumerable<PropertyInfo> exportedFields, Action<IEnumerable<PropertyInfo>> save)
    {
        ExportedFields = new(exportedFields);
        AvailableFields = new(Event.Fields.Except(ExportedFields));

        // used for saving after the last CollectionChanged, which fires more than once when re-ordering
        Debouncer saveExportedEventFields = new(TimeSpan.FromMilliseconds(100), () => save(ExportedFields),
            async error => await ErrorReport.WriteAsyncAndShare(error.ToString(), "saving exported event fields in the Settings"));

        // When selection changes, save export format
        ExportedFields.CollectionChanged += (_, __) => saveExportedEventFields.Run();
    }

    [RelayCommand]
    public void ToggleField(PropertyInfo field)
    {
        if (ExportedFields.Contains(field))
        {
            AvailableFields.Add(field);
            ExportedFields.Remove(field);
        }
        else
        {
            ExportedFields.Add(field);
            AvailableFields.Remove(field);
        }
    }

    internal static (CollectionView included, CollectionView excluded) Views(EventPropertySelection model)
    {
        DataTemplate itemTemplate = new(() => BndLbl(nameof(PropertyInfo.Name)).Padding(10)
            .BindTapGesture(nameof(ToggleFieldCommand), commandSource: model, parameterPath: "."));

        var itemsLayout = LinearItemsLayout.Horizontal;

        var included = new CollectionView
        {
            ItemsSource = model.ExportedFields,
            ItemsLayout = itemsLayout,
            ItemTemplate = itemTemplate,
            CanReorderItems = true
        };

        var excluded = new CollectionView
        {
            ItemsSource = model.AvailableFields,
            ItemsLayout = itemsLayout,
            ItemTemplate = itemTemplate
        };

        return (included, excluded);
    }
}
