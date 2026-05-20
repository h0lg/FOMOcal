using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.Resources;
using Microsoft.Maui.Layouts;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class ScrapeJobEditor
{
    public partial class View : Border
    {
        private readonly (Label label, Border layout) help;
        private readonly ScrapeJobEditor model;
        private readonly Func<InputView, Func<string?>?, Grid> createVisualSelectorEntry;
        private readonly Func<InputView?> getVisualSelectorHost;

        public View(ScrapeJobEditor model,
            Func<InputView, Func<string?>?, Grid> createVisualSelectorEntry,
            Func<InputView?> getVisualSelectorHost)
        {
            this.model = model;
            this.createVisualSelectorEntry = createVisualSelectorEntry;
            this.getVisualSelectorHost = getVisualSelectorHost;
            help = HelpLabel();
            StyleClass = [Styles.Border.RoundedSection];
            BindingContext = model;

            (Switch Switch, Grid Wrapper) displayInputs = Swtch(nameof(DisplayInputs),
                BindingMode.OneWayToSource); // avoids triggering set by reaction to PropertyChanged, falsifying field value

            (Switch Switch, Grid Wrapper) ignoreNestedText = Swtch(nameof(IgnoreNestedText));
            HintedInput(ignoreNestedText.Switch, HelpTexts.ScrapeJobIgnoreNestedText);

            List<IView> children = [
                HStack(5, Lbl(model.label).Bold(), displayInputs.Wrapper.BindVisible(nameof(IsEmpty))),

                SelectorInput("closest", nameof(Closest),
                    null, // for picking common ancestor of clicked element and event container
                    HelpTexts.ScrapeJobClosest),

                SelectorInput("selector", nameof(Selector),
                    () => model.Closest, // for picking descendant, preferably from Closest if set
                    HelpTexts.ScrapeJobSelector),

                LbldView("ignore nested text", ignoreNestedText.Wrapper).DisplayWithChecked(nameof(IgnoreNestedText)),
                TextEntry("attribute", nameof(Attribute), HelpTexts.ScrapeJobAttribute),

                TextEntry("replace", nameof(Replace), HelpTexts.ScrapeJobReplace, multiLine: true,
                    placeholder: "a }} b", regex101DeepLink: ScrapeJob.Step.Replacements),

                TextEntry("match", nameof(Match), HelpTexts.ScrapeJobMatch, multiLine: true,
                    regex101DeepLink: ScrapeJob.Step.Match)
            ];

            if (model.DateScrapeJob is not null) children.AddRange(
                TextEntry(Glyphs.Date + "format", nameof(Format), HelpTexts.DateScrapeJobFormat),
                TextEntry("culture", nameof(Culture), HelpTexts.DateScrapeJobCulture));

            children.Add(TextEntry(Glyphs.Comment, nameof(Comment), HelpTexts.Comment, multiLine: true));

            FlexLayout form = new() { Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center };

            foreach (var child in children.Cast<Microsoft.Maui.Controls.View>())
            {
                child.Margins(left: 10);
                form.Children.Add(child);
            }

            var previewSummary = BndLbl(nameof(PreviewSummary))
                // display if PreviewSummary has value. hide if editor has focus because ValuePreview.List is then shown
                .BindVisible(new Binding(nameof(PreviewSummary), converter: Converters.NotNull),
                    Converters.And, new Binding(nameof(HasFocus), converter: Converters.Not));

            form.Children.Add(previewSummary.End().Grow(1));

            Content = VStack(5, help.layout, form,
                ValuePreview.List(itemsSource: nameof(PreviewResults),
                    hasFocus: nameof(HasFocus), source: model, editor: model));

            model.UpdatePreview(); // once initially
        }

        private Grid SelectorInput(string label, string property, Func<string?>? maybeGetDescendantOfClosest, string tooltip)
        {
            var input = createVisualSelectorEntry(HintedInput(Edtr(property), tooltip,
                cancelFocusChanged: (vis, focused) => !focused && getVisualSelectorHost() == vis),
                maybeGetDescendantOfClosest);

            return LbldView(label, input).FlexFitContents().DisplayWithSignificant(property);
        }

        private Grid TextEntry(string label, string property, string tooltip,
            bool multiLine = false, string? placeholder = null, ScrapeJob.Step? regex101DeepLink = null)
        {
            InputView input = multiLine ? Edtr(property) : Entr(property);
            HintedInput(input, tooltip).Placeholder(placeholder);

            Microsoft.Maui.Controls.View[] views = regex101DeepLink == null ? [input]
                : [input, Regex101.DeepLink(regex101DeepLink.Value, input, model.GetPreviewValues)];

            Grid wrapper = LbldView(label, views);
            if (multiLine) wrapper.FlexFitContents();
            return wrapper.DisplayWithSignificant(property);
        }

        private T HintedInput<T>(T vis, string tooltip,
            Func<VisualElement, bool, bool>? cancelFocusChanged = null) where T : VisualElement
            => vis.InlineTooltipOnFocus(tooltip, help, async (vis, focused) => await model.SetFocusAsync(vis, focused), cancelFocusChanged);
    }
}

internal static class ScopeJobEditorExtensions
{
    internal static T ForwardFocusTo<T>(this T vis, ScrapeJobEditor model) where T : VisualElement
        => vis.OnFocusChanged(async (vis, focused) => await model.SetFocusAsync(vis, focused));

    internal static T DisplayWithSignificant<T>(this T vis, string textPropertyName) where T : VisualElement
        => vis.BindVisible(new Binding(nameof(ScrapeJobEditor.DisplayInputs)), Converters.Or,
            new Binding(textPropertyName, converter: Converters.IsSignificant));

    internal static T DisplayWithChecked<T>(this T vis, string boolPropertyName) where T : VisualElement
        => vis.BindVisible(new Binding(nameof(ScrapeJobEditor.DisplayInputs)),
            Converters.Or, new Binding(boolPropertyName));
}
