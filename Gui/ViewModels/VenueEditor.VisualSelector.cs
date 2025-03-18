using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    [ObservableProperty] private string? pickedSelector;
    [ObservableProperty] private bool enablePicking = true;
    [ObservableProperty] private bool showPickedSelector;

    partial class Page
    {
        private readonly Grid visualSelector;
        private AutomatedEventPageView? pageView;
        private string? selectedQuery;

        private Grid CreateVisualSelector()
        {
            pageView = new(model.venue);

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

            var header = HWrap(5,
                Check(nameof(EnablePicking))
                    .ToolTip("Toggle picking mode. You may want to disable this to interact with the page" +
                        " as you would in a normal browser, e.g. to close popups and overlays" +
                        " - or play with those eye-opening 🍪 cookie reminders sponsored by" +
                        " the EU if you're lucky enough to be browsing from there.")
                    .Margins(top: 5, right: -13),
                Lbl("Tap an element on the page to pick it.").TapGesture(TogglePicking),
                Btn("⿴ Pick the outer element").BindIsVisibleToValueOf(pickedSelector).TapGesture(PickParent),
                Lbl("until you're happy with your pick.").BindIsVisibleToValueOf(pickedSelector),
                Btn("🥢 Toggle selectors").BindIsVisibleToValueOf(pickedSelector).TapGesture(TogglePickedSelector),
                Btn("🗙").TapGesture(HideVisualSelector));

            var selectors = Grd(cols: [Auto, Star], rows: [Auto, Auto, Auto], spacing: 5,
                Lbl("Select parts of either selector you'd like to use.").ColumnSpan(2),
                SelectorDisplay(pickedSelector).Row(1).ColumnSpan(2),
                Btn("➕").TapGesture(AppendSelectedQuery).Row(2),
                Lbl("Append the selected text to your query to try it out.").Row(2).Column(2))
                .BindVisible(nameof(ShowPickedSelector));

            var visualSelector = Grd(cols: [Star], rows: [Auto, Auto, Star], spacing: 5,
                header, selectors.Row(1), pageView.Row(2))
                .IsVisible(false).BackgroundColor(Colors.DarkSlateGray);

            SizeChanged += (o, e) => SyncHeightWithPage();
            SyncHeightWithPage(); // to init HeightRequest to init TranslationY for smooth first opening
            visualSelector.TranslationY = visualSelector.HeightRequest;
            return visualSelector;

            void SyncHeightWithPage() => visualSelector.HeightRequest = Height - 100;
        }

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
        private void AppendSelectedQuery() => model.visualSelectorHost!.Text += " " + selectedQuery;

        private async Task ShowVisualSelectorForAsync(Entry entry, string selector)
        {
            model.visualSelectorHost = entry;
            entry.Focus(); // to keep its help open
            visualSelector.IsVisible = true;
            await pageView!.PickRelativeTo(selector);
            await visualSelector.TranslateTo(0, 0, 300, Easing.CubicInOut);
            await form.ScrollToAsync(entry, ScrollToPosition.End, true); // scroll entry to end so that Help above is visible
        }

        private void HideVisualSelector()
        {
            if (model.visualSelectorHost == null) return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await visualSelector.TranslateTo(0, visualSelector.HeightRequest, 300, Easing.CubicInOut);
                visualSelector.IsVisible = false;
                Entry entry = model.visualSelectorHost; // keep a reference to it before resetting
                model.visualSelectorHost = null; // reset before re-focusing entry because its handler may check model.visualSelectorHost
                entry.Focus(); // re-focus the entry to keep its help and preview or errors open
            });
        }
    }
}
