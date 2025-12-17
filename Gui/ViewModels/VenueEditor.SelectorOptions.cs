using System.ComponentModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using FomoCal.Gui.Resources;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

using SelectorOptionsRepo = SingletonJsonFileRepository<VenueEditor.SelectorOptions>;

partial class VenueEditor
{
    private readonly Lazy<SelectorOptionsRepo> selectorOptionsRepo
        = new(() => IPlatformApplication.Current!.Services.GetService<SelectorOptionsRepo>()!);

    private readonly SelectorOptions selectorOptions = new() { SemanticClasses = true, LayoutClasses = true }; // initialize with defaults

    [ObservableProperty] public partial bool ShowSelectorDetail { get; set; }

    private void ToggleSelectorDetail() => ShowSelectorDetail = !ShowSelectorDetail;

    private Task<bool>? LazyLoadSelectorOptionsOnce()
        => selectorOptionsRepo.IsValueCreated ? null : LazyLoadSelectorOptionsAsync();

    private async Task<bool> LazyLoadSelectorOptionsAsync()
    {
        // load and restore remembered selectorOptions once from JSON file
        var saved = await selectorOptionsRepo.Value.LoadAsync();

        bool madeChanges = false;

        if (saved != null)
        {
            madeChanges = selectorOptions.RestoreFrom(saved);

            // notify potential subscribers
            if (madeChanges) OnPropertyChanged(nameof(DisplayedSelector));
        }

        // hook up PropertyChanged handler saving changes only after options restore
        selectorOptions.PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(SelectorOptions.IncludeAncestorPath)
                || e.PropertyName == nameof(SelectorOptions.XPathSyntax))
                OnPropertyChanged(nameof(DisplayedSelector));

            selectorOptionsRepo.Value.SaveAsync(selectorOptions);
        };

        return madeChanges;
    }

    // implements and extends the PickedSelectorOptions with others that need persistence
    internal partial class SelectorOptions : ObservableObject, AutomatedEventPageView.PickedSelectorOptions
    {
        [ObservableProperty] public partial bool IncludeAncestorPath { get; set; }
        [ObservableProperty] public partial bool XPathSyntax { get; set; }
        [ObservableProperty] public partial bool TagName { get; set; }
        [ObservableProperty] public partial bool Ids { get; set; }
        [ObservableProperty] public partial bool SemanticClasses { get; set; }
        [ObservableProperty] public partial bool LayoutClasses { get; set; }
        [ObservableProperty] public partial bool OtherAttributes { get; set; }
        [ObservableProperty] public partial bool OtherAttributeValues { get; set; }
        [ObservableProperty] public partial bool Position { get; set; }

        internal bool RestoreFrom(SelectorOptions saved)
        {
            bool madeChanges = false;
            PropertyChanged += TrackChanges;

            foreach (var prop in typeof(SelectorOptions).GetProperties())
                prop.SetValue(this, prop.GetValue(saved, null));

            PropertyChanged -= TrackChanges;
            return madeChanges;

            void TrackChanges(object? o, PropertyChangedEventArgs e) => madeChanges = true;
        }
    }

    partial class Page
    {
        View[] GetSelectorOptions((Label label, Border layout) help)
        {
            var xPathSyntax = new Switch() // enables switching between CSS and XPath syntax to save space
                .Bind(Switch.IsToggledProperty, nameof(SelectorOptions.XPathSyntax), source: model.selectorOptions)
                .InlineTooltipOnFocus(string.Format(HelpTexts.SelectorSyntaxFormat, FomoCal.ScrapeJob.XPathSelectorPrefix), help);

            var syntax = HStack(5, Lbl("Syntax").Bold(), Lbl("CSS"), SwtchWrp(xPathSyntax), Lbl("XPath"));

            return [syntax.View,
                Lbl("detail").Bold(),
                LbldView("ancestor path", Check(nameof(SelectorOptions.IncludeAncestorPath), source: model.selectorOptions)
                    .InlineTooltipOnFocus(HelpTexts.IncludePickedSelectorPath, help)),
                SelectorOption("tag name", nameof(SelectorOptions.TagName), HelpTexts.TagName),
                SelectorOption("id", nameof(SelectorOptions.Ids), HelpTexts.ElementId),
                Lbl("classes").Bold(),
                SelectorOption("with style", nameof(SelectorOptions.LayoutClasses), string.Format(HelpTexts.ClassesWith_Style, "")),
                SelectorOption("without", nameof(SelectorOptions.SemanticClasses), string.Format(HelpTexts.ClassesWith_Style, " no")),
                Lbl("other attributes").Bold(),
                SelectorOption("names", nameof(SelectorOptions.OtherAttributes), HelpTexts.OtherAttributes),
                SelectorOption("values", nameof(SelectorOptions.OtherAttributeValues), HelpTexts.OtherAttributeValues),
                SelectorOption("position", nameof(SelectorOptions.Position), HelpTexts.ElementPosition)];

            HorizontalStackLayout SelectorOption(string label, string isCheckedPropertyPath, string helpText)
                => LbldView(label, Check(isCheckedPropertyPath, source: model.selectorOptions).InlineTooltipOnFocus(helpText, help));
        }
    }
}
