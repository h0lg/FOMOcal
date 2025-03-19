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
        private readonly System.Timers.Timer closeTimer = new(500); // Close delay in ms
        private readonly Grid visualSelector;
        private bool isHelperVisible;
        private AutomatedEventPageView? pageView;

        private Grid CreateVisualSelector(VenueEditor model)
        {
            closeTimer.Elapsed += (s, e) => HideHelper();

            System.ComponentModel.PropertyChangedEventHandler editorPropertyChanged = (o, e) =>
            {
                if (e.PropertyName == nameof(ScrapeJobEditor.HasFocus))
                {
                    var editor = (ScrapeJobEditor)o!;
                    ToggleVisualSelector(editor.HasFocus, editor.EventProperty);
                }
            };

            foreach (var editor in model.scrapeJobEditors)
                editor.PropertyChanged += editorPropertyChanged;

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

            pageView.PickedSelector += selector =>
            {
                model.PickedSelector = selector;
                closeTimer.Stop();
            };

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
                Btn("🥢 Toggle selectors").TapGesture(TogglePickedSelector));

            var selectors = Grd(cols: [Auto, Star], rows: [Auto, Auto], spacing: 5,
                Lbl("Select parts of either selector and copy them to your venue scraping config to try them out.").ColumnSpan(2),
                SelectorDisplay(pickedSelector).Row(1).ColumnSpan(2))
                .BindVisible(nameof(ShowPickedSelector));

            var visualSelector = Grd(cols: [Star], rows: [Auto, Auto, Star], spacing: 5,
                header, selectors.Row(1), pageView.Row(2))
                .IsVisible(false).BackgroundColor(Colors.DarkSlateGray);

            SizeChanged += (o, e) => SyncHeightWithPage();
            SyncHeightWithPage(); // to init HeightRequest to init TranslationY for smooth first opening
            visualSelector.TranslationY = visualSelector.HeightRequest;

            // Keep the helper open if interacted with, seems to cause closing
            return visualSelector.TapGesture(() => ToggleVisualSelector(true, nameof(visualSelector)));

            void SyncHeightWithPage() => visualSelector.HeightRequest = Height - 100;
        }

        private Editor SelectorDisplay(string propertyPath) =>
            new Editor { IsReadOnly = true }.Bind(Editor.TextProperty, propertyPath)
                .OnFocusChanged((_, focused) => ToggleVisualSelector(focused, propertyPath));

        private void TogglePicking() => model.EnablePicking = !model.EnablePicking;
        private async void PickParent() => await pageView!.PickParent();
        private void TogglePickedSelector() => model.ShowPickedSelector = !model.ShowPickedSelector;

        private void ToggleVisualSelector(bool focused, string what)
        {
            if (focused)
            {
                closeTimer.Stop();
                if (isHelperVisible) return;
                isHelperVisible = true;
                visualSelector.IsVisible = true;
                visualSelector.TranslateTo(0, 0, 300, Easing.CubicInOut);
            }
            else closeTimer.Start();
        }

        private void HideHelper()
        {
            if (!isHelperVisible) return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await visualSelector.TranslateTo(0, visualSelector.HeightRequest, 300, Easing.CubicInOut);
                visualSelector.IsVisible = false;
                isHelperVisible = false;
            });

            closeTimer.Stop();
        }
    }
}
