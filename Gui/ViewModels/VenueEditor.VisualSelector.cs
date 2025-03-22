using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;
using SelectorOptions = FomoCal.Gui.ViewModels.AutomatedEventPageView.PickedSelectorOptions;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    [ObservableProperty] private string? pickedSelector;
    [ObservableProperty] private bool enablePicking = true;
    [ObservableProperty] private bool showPickedSelector;
    [ObservableProperty] private bool showSelectorDetail;

    partial class Page
    {
        private readonly AbsoluteLayout visualSelector;
        private readonly SelectorOptions selectorOptions = new() { SemanticClasses = true, LayoutClasses = true };
        private AutomatedEventPageView? pageView;
        private string? selectedQuery;

        private AbsoluteLayout CreateVisualSelector()
        {
            pageView = new(model.venue);
            selectorOptions.PropertyChanged += (o, e) => pageView.SetPickedSelectorDetail(selectorOptions);

            pageView.HtmlWithEventsLoaded += async html =>
            {
                if (html.IsSignificant())
                    model.programDocument = await model.scraper.CreateDocumentAsync(html!);
                else
                {
                    model.programDocument = null;
                    await App.CurrentPage.DisplayAlert("Event loading timed out.", pageView.EventLoadingTimedOut, "Ok");
                }

                model.previewedEvents = null;
                model.RevealMore();
            };

            pageView.PickedSelector += selector => model.PickedSelector = selector;

            model.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == nameof(ProgramUrl))
                    pageView.Source = model.ProgramUrl;
                else if (e.PropertyName == nameof(EnablePicking))
                    pageView.EnablePicking(model.EnablePicking);
            };

            const string pickedSelector = nameof(PickedSelector);

            var controlsAndInstructions = HWrap(5,
                Swtch(nameof(EnablePicking)).Wrapper
                    .ToolTip("Toggle picking mode. You may want to disable this to interact with the page" +
                        " as you would in a normal browser, e.g. to close popups and overlays" +
                        " - or play with those eye-opening 🍪 cookie reminders sponsored by" +
                        " the EU if you're lucky enough to be browsing from there."),
                Lbl("Tap a page element to pick it.").TapGesture(TogglePicking),
                Btn("⿴ Pick its container").BindIsVisibleToValueOf(pickedSelector).TapGesture(PickParent),
                Lbl("if you need.").BindIsVisibleToValueOf(pickedSelector),
                Btn("🥢 Choose a selector").BindIsVisibleToValueOf(pickedSelector).TapGesture(TogglePickedSelector),
                Btn("🍜 selector options").BindVisible(nameof(ShowPickedSelector)).TapGesture(ToggleSelectorDetail));

            var xPathSyntax = new Switch() // enables switching between CSS and XPath syntax to save space
                .Bind(Switch.IsToggledProperty, nameof(SelectorOptions.XPathSyntax), source: selectorOptions);

            var syntax = HStack(5, Lbl("Syntax").Bold(), Lbl("CSS"), SwtchWrp(xPathSyntax), Lbl("XPath"));

            View[] selectorDetails = [syntax,
                Lbl("detail").Bold(),
                SelectorOption("tag name", nameof(SelectorOptions.TagName)),
                SelectorOption("id", nameof(SelectorOptions.Ids)),
                Lbl("classes").Bold(),
                SelectorOption("with style", nameof(SelectorOptions.LayoutClasses)),
                SelectorOption("without", nameof(SelectorOptions.SemanticClasses)),
                Lbl("other attibutes").Bold(),
                SelectorOption("names", nameof(SelectorOptions.OtherAttributes)),
                SelectorOption("values", nameof(SelectorOptions.OtherAttributeValues)),
                SelectorOption("position", nameof(SelectorOptions.Position))];

            foreach (var view in selectorDetails)
                controlsAndInstructions.AddChild(view.BindVisible(nameof(ShowSelectorDetail)));

            View[] appendSelection = [
                Lbl("Select parts of the selector text and"),
                Btn("➕ append").TapGesture(AppendSelectedQuery),
                Lbl("them to your query to try them out.")];

            foreach (var view in appendSelection)
                controlsAndInstructions.AddChild(view.BindVisible(nameof(ShowPickedSelector)));

            return new()
            {
                IsVisible = false,
                BackgroundColor = Colors.DarkSlateGray,
                Children = {
                    Grd(cols: [Star], rows: [Auto, Auto, Star], spacing: 0,
                        controlsAndInstructions.View,
                        SelectorDisplay(pickedSelector).Row(1).ColumnSpan(2).BindVisible(nameof(ShowPickedSelector)),
                        pageView.Row(2))
                        .LayoutBounds(0, 0, 1, 1).LayoutFlags(AbsoluteLayoutFlags.SizeProportional), // full size
                    Btn("🗙").TapGesture(HideVisualSelector).Size(30, 30).TranslationY(-35) // float half above upper boundary
                        .LayoutBounds(0.99, 0, -1, -1).LayoutFlags(AbsoluteLayoutFlags.PositionProportional) // position on the right, autosized
                }
            };
        }

        private HorizontalStackLayout SelectorOption(string label, string isCheckedPropertyPath)
            => HStack(5, Check(isCheckedPropertyPath, source: selectorOptions), Lbl(label).CenterVertical());

        private Editor SelectorDisplay(string propertyPath)
        {
            var editor = new Editor { IsReadOnly = true, AutoSize = EditorAutoSizeOption.TextChanges }.Bind(Editor.TextProperty, propertyPath);
            // save selected part of selector query for AppendSelectedQuery
            editor.Unfocused += (o, e) => selectedQuery = editor.Text.Substring(editor.CursorPosition, editor.SelectionLength);
            return editor;
        }

        private void TogglePicking() => model.EnablePicking = !model.EnablePicking;
        private async void PickParent() => await pageView!.PickParent();
        private void TogglePickedSelector() => model.ShowPickedSelector = !model.ShowPickedSelector;
        private void ToggleSelectorDetail() => model.ShowSelectorDetail = !model.ShowSelectorDetail;
        private void AppendSelectedQuery() => model.visualSelectorHost!.Text += " " + selectedQuery;

        private async Task ShowVisualSelectorForAsync(Entry entry, string selector, bool descendant)
        {
            model.visualSelectorHost = entry;
            entry.Focus(); // to keep its help open
            visualSelector.IsVisible = true;
            await pageView!.PickRelativeTo(selector, descendant);
            await visualSelector.AnimateHeightRequest(Height - 100);
            await form.ScrollToAsync(entry, ScrollToPosition.End, true); // scroll entry to end so that Help above is visible
        }

        private void HideVisualSelector()
        {
            if (model.visualSelectorHost == null) return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await visualSelector.AnimateHeightRequest(0);
                visualSelector.IsVisible = false;
                Entry entry = model.visualSelectorHost; // keep a reference to it before resetting
                model.visualSelectorHost = null; // reset before re-focusing entry because its handler may check model.visualSelectorHost
                entry.Focus(); // re-focus the entry to keep its help and preview or errors open
            });
        }
    }
}
