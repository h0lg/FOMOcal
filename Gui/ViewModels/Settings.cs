using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
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
            var textExport = EventPropertySelection.Views(model.ExportedTextEventFields);

            var exportTextAlignedWithHeaders = Swtch(nameof(ExportTextAlignedWithHeaders)).Wrapper
                .ToolTip("whether to column-align the plain text export using spaces and include column headers");

            var sectionEnd = 20;
            GridLength[] rows = [Auto, sectionEnd, Auto, Auto, Auto, sectionEnd, Auto, Auto, Auto, Auto, sectionEnd, Auto, Auto, Auto, Auto];

            var layout = Grd(cols: [Auto, Star], rows, spacing: 5,
                SubHeadline("🎨 Theme"), ThemeSwitches().Column(1),

                SubHeadline("🖺 HTML export").Row(2),
                Section("included fields").Row(3),
                htmlExport.included.CenterVertical().Row(3).Column(1),
                Section("excluded fields").StyleClass(Styles.Label.Demoted).Row(4),
                htmlExport.excluded.Row(4).Column(1),

                SubHeadline("🖹 Text export").Row(6),
                Section("aligned with headers").Row(7),
                exportTextAlignedWithHeaders.CenterVertical().Row(7).Column(1),
                Section("included fields").Row(8),
                textExport.included.CenterVertical().Row(8).Column(1),
                Section("excluded fields").StyleClass(Styles.Label.Demoted).Row(9),
                textExport.excluded.Row(9).Column(1),

                SubHeadline("⏱ Browser timing").Row(11),
                ContextLabel("You can tweak the automation engine here if you experience problems, e.g. due to a slow internet connection."
                    + " Tread lightly - footguns ahead!").Center().Row(11).Column(1),
                TimingSection("loading lazy or more").Row(12),
                LoadingLazyOrMore().Top().Row(12).Column(1),
                TimingSection("scroll paging").Row(13),
                ScrollPaging().Top().Row(13).Column(1),
                TimingSection("swap paging").Row(14),
                SwapPaging().Top().Row(14).Column(1));

            Content = new ScrollView { Content = layout.Center() };
        }

        private static Label SubHeadline(string text)
            => Lbl(text).StyleClass(Styles.Label.SubHeadline).CenterVertical().End();

        private static Label Section(string text, int topMargin = 10) => Lbl(text).Margins(top: topMargin).End();
        private static Label TimingSection(string text) => Section(text, topMargin: 24);

        private static HorizontalStackLayout ThemeSwitches()
            => HStack(0, ThemeVariantToggle("🌑 dark", AppTheme.Dark, "always use dark theme"),
                ThemeVariantToggle("🌓 switch with OS", AppTheme.Unspecified,
                    "use light or dark depending on the theme variant selected on the operating system level"),
                ThemeVariantToggle("🌕 light", AppTheme.Light, "always use light theme")).View;

        private static RadioButton ThemeVariantToggle(string label, AppTheme theme, string tooltip)
            => new RadioButton { Content = label, StyleClass = ["SingleSelectToggleButton"] }.ToolTip(tooltip)
                .Bind(RadioButton.IsCheckedProperty, nameof(UserTheme), mode: BindingMode.TwoWay,
                    convert: (AppTheme t) => t == theme, convertBack: isChecked => isChecked ? theme : Theme.Get());
    }
}

public partial class EventPropertySelection
{
    public ObservableCollection<PropertyInfo> ExportedFields { get; }
    public ObservableCollection<PropertyInfo> AvailableFields { get; }

    public EventPropertySelection(IEnumerable<PropertyInfo> exportedFields, Action<IEnumerable<PropertyInfo>> save)
    {
        ExportedFields = new(exportedFields);
        AvailableFields = new(Export.EventFields.Except(ExportedFields));

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
            ExportedFields.Remove(field);
            AvailableFields.Add(field);
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
        }
            .ToolTip("✊ Drag event fields to ⇆ re-order them, 👇 tap one to exclude it from the export.");

        var excluded = new CollectionView
        {
            ItemsSource = model.AvailableFields,
            ItemsLayout = itemsLayout,
            ItemTemplate = itemTemplate
        }
            .ToolTip("👆 Tap an event field to include it in the export.");

        return (included, excluded);
    }
}
