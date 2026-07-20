using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using FomoCal.Gui.Resources;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayedSelector))] public partial string? PickedSelector { get; set; }
    [ObservableProperty] public partial bool EnablePicking { get; set; } = false;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayedSelector))] public partial bool ShowSelectorOptions { get; set; }

    public string? DisplayedSelector
        => PickedSelector.IsNullOrWhiteSpace() ? null
            : selectorOptions.IncludeAncestorPath
                ? ShowSelectorOptions ? PickedSelector
                    : PickedSelector.NormalizeWhitespace().GetLast(50)
                : AutomatedEventPageView.GetLeafSelector(PickedSelector!, selectorOptions.XPathSyntax);

    private void TogglePicking() => EnablePicking = !EnablePicking;
    private void TogglePickedSelector() => ShowSelectorOptions = !ShowSelectorOptions;

    private async Task OnHtmlLoadedAsync(string? html, string? url)
    {
        if (html.IsSignificant()) SetDocument(await scraper.CreateDocumentAsync(html!, venue, url));
        else await App.CurrentPage.DisplayAlertAsync("Event loading timed out.", venue.FormatEventLoadingTimedOut(), "OK");

        IsEventPageLoading = false;
        RevealMore();
    }

    private async Task OnErrorLoadingEventsAsync(WebNavigationError error)
    {
        SetDocument(null);
        IsEventPageLoading = false;
        string suffix = error == WebNavigationError.Cancel ? "ed" : "";
        var message = $"Navigation {error}{suffix}.";

        // using ErrorLoading to give user feedback about an invalid URL instead of validating before
        if (error == WebNavigationError.Failure)
        {
            if (!ProgramUrl.IsSignificantValidUrl()) message += $" '{ProgramUrl}' is not a valid web address.";
            else if (!App.HasInternet) message += " Loading the event listing requires an internet connection.";
        }

        await App.CurrentPage.DisplayAlertAsync("Error loading event page.", message, "OK");
        RevealMore();
    }

    partial class Page
    {
        private readonly Grid visualSelector;
        private AutomatedEventPageView? pageView;

        private Grid CreateVisualSelector()
        {
            pageView = new(model.venue, log: (message, level) => model.BrowserLog.Add(VenueScrapeContext.FormatLog(message, level)));
            pageView.HtmlLoaded += async html => await model.OnHtmlLoadedAsync(html, pageView.Url);
            pageView.ErrorLoading += async error => await model.OnErrorLoadingEventsAsync(error);
            pageView.PickedSelector += selector => model.PickedSelector = selector;

            void GoTo(string programUrl)
            {
                model.HasInternet = App.HasInternet;
                if (!model.HasInternet || !programUrl.IsSignificantValidUrl()) return; // avoid navigation error
                pageView.Url = programUrl;
                model.IsEventPageLoading = true;
            }

            GoTo(model.venue.ProgramUrl);

            model.PropertyChanged += async (o, e) =>
            {
                if (e.PropertyName == nameof(ProgramUrl)) GoTo(model.ProgramUrl);
                else if (e.PropertyName == nameof(EnablePicking))
                    await pageView.EnablePicking(model.EnablePicking);
                else if (e.PropertyName == nameof(ShowSelectorOptions))
                    UpdateHeight();
                else if (e.PropertyName == nameof(LazyLoaded)
                    || e.PropertyName == nameof(Encoding)
                    || (e.PropertyName == nameof(EventSelector) && model.LazyLoaded))
                    await ReloadAsync();
            };

            const string displayedSelector = nameof(DisplayedSelector),
                showSelectorOptions = nameof(ShowSelectorOptions);

            var help = HelpLabel();
            var enablePicking = Swtch(nameof(EnablePicking));
            enablePicking.Switch.InlineTooltipOnFocus(HelpTexts.EnablePicking, help);

            Label enablePickingLabel = Lbl("Tap to pick.")
                .BindVisible(showSelectorOptions, converter: Converters.Not)
                .TapGesture(() =>
                {
                    model.TogglePicking();
                    enablePicking.Switch.Focus(); // to in-line tooltip
                });

            var controlsAndInstructions = HWrap(5,
                enablePicking.Wrapper.BindVisible(showSelectorOptions, converter: Converters.Not),
                enablePickingLabel,
                Btn("⿴ Pick parent").TapGesture(PickParent)
                    .BindVisible(new Binding(displayedSelector, converter: Converters.IsSignificant),
                        Converters.And, new Binding(showSelectorOptions, converter: Converters.Not)),
                new Button().BindVisibleToSignificanceOf(displayedSelector).TapGesture(model.TogglePickedSelector)
                    .Bind(Button.TextProperty, showSelectorOptions,
                        convert: static (bool showSelector) => showSelector ? "⏮ Re-pick ⛶ the element" : "🍒 Choose selector ⏭"));

            Editor selectorDisplay = SelectableMultiLineLabel(displayedSelector).FontSize(16);

            var ancestorPathInfo = Lbl("The part after the last > or / represents the picked element and is most important.")
                .StyleClass(Styles.Label.Demoted).TextCenter().IsVisible(false);

            Label appendLbl = Lbl("Appends the ⇥selection⇤ to the existing selector if it matches the syntax - and otherwise replaces it.")
                .StyleClass(Styles.Label.Demoted).TextCenterVertical().End().IsVisible(false);

            var append = Btn(Glyphs.Add + " append").IsVisible(false)
                .TapGesture(() => AppendSelectedQuery(selectorDisplay));

            selectorDisplay.InlineTooltipOnFocus(HelpTexts.PickedSelectorDisplay, help,
                onFocusChanged: async (_, hasFocus) =>
                {
                    await Task.Delay(300); // so that tap gesture can fire
                    appendLbl.IsVisible = append.IsVisible = hasFocus;
                    ancestorPathInfo.IsVisible = hasFocus && model.selectorOptions.IncludeAncestorPath;
                });

            Label selectorDetailHeadline = Lbl("🍜 selector syntax & detail").StyleClass(Styles.Label.SubHeadline);
            var selectorDetailInfo = Lbl(HelpTexts.SelectorDetailInfo).StyleClass(Styles.Label.Demoted);
            View[] selectorDetail = [selectorDetailInfo, .. GetSelectorOptions(help)];
            var selectorOptions = Expndr(selectorDetailHeadline, selectorDetail).BindVisible(showSelectorOptions);

            pickedSelectorScroller = new()
            {
                Content = Grd(cols: [Star, Auto], rows: [Auto, Auto, Auto, Auto, Auto, Auto], spacing: 5,
                    controlsAndInstructions.View.ColumnSpan(2),
                    selectorOptions.Row(1).ColumnSpan(2),
                    help.layout.Row(2).ColumnSpan(2),
                    ancestorPathInfo.Row(3).ColumnSpan(2),
                    selectorDisplay.Row(4).ColumnSpan(2),
                    appendLbl.Row(5), append.Row(5).Column(1))
                    .Paddings(bottom: 10)
            };

            SetupAutoSizing();

            return Grd(cols: [Star], rows: [5, Star], spacing: 0,
                Grd(cols: [Star], rows: [Auto, Star], spacing: 0,
                    pickedSelectorScroller.Paddings(5),
                    pageView
                        .ToolTip("You may find it useful to zoom  the page using [Ctrl] + MouseWheel or try the 'Inspect' tool from the right-click context menu.")
                        .BindVisible(showSelectorOptions, converter: Converters.Not)
                        .Row(1)).StyleClass("VisualSelectorContent").Row(1),
                Btn("⬇️").TapGesture(HideVisualSelector).End().Height(45).TranslationY(20))
                    .StyleClass("VisualSelector");
        }

        private async void PickParent() => await pageView!.PickParent();

        private void AppendSelectedQuery(Editor display)
        {
            var selectedQuery = display.Text?.Substring(display.CursorPosition, display.SelectionLength);
            string normalized = selectedQuery.NormalizeWhitespace();
            InputView host = model.visualSelectorHost!;
            var existing = host.Text ?? "";
            var hasXpath = FomoCal.ScrapeJob.TryGetXPathSelector(existing, out var existingXpath);

            if (model.selectorOptions.XPathSyntax)
            {
                var selector = hasXpath ? existingXpath + normalized // append to existing XPath
                    : normalized; // discard CSS query

                host.Text = FomoCal.ScrapeJob.FormatXpathSelector(selector);
            }
            else host.Text = hasXpath ? normalized // discard XPath query
                : (existing + " " + normalized).Trim(); // append to existing CSS
        }

        private async Task ReloadAsync()
        {
            model.HasInternet = App.HasInternet;

            if (!model.HasInternet)
            {
                await App.CurrentPage.DisplayAlertAsync("Connect to the internet and retry",
                    "Loading the event listing requires internet access.", "OK");

                return;
            }

            model.IsEventPageLoading = true;
            pageView!.Reload();
        }

        private async Task ShowVisualSelectorForAsync(InputView input, string selector, bool descendant)
        {
            // reset UI state to allow picking an element
            model.ShowSelectorOptions = false;
            model.EnablePicking = true;

            var loadingOnce = model.LazyLoadSelectorOptionsOnce();

            if (loadingOnce != null)
            {
                var madeChanges = await loadingOnce;
                if (madeChanges) await pageView!.SetPickedSelectorDetail(model.selectorOptions); // apply restored options

                // hook up PropertyChanged handler only after options restore triggered it, potentially more than once
                model.selectorOptions.PropertyChanged += (o, e) => pageView!.SetPickedSelectorDetail(model.selectorOptions);
            }

            model.visualSelectorHost = input;

            if (DeviceInfo.Idiom == DeviceIdiom.Desktop || DeviceInfo.Idiom == DeviceIdiom.Tablet)
                input.Focus(); // to keep its help open on devices with big screens
            else input.Unfocus(); // on-screen keyboard sliding in from below is useless in visual picker and takes up space

            visualSelector.IsVisible = true;
            await pageView!.PickRelativeTo(selector, descendant);
            UpdateHeight();
        }

        private void HideVisualSelector()
        {
            model.EnablePicking = false; // to enable running a PagingStrategy that ClicksElementToLoad
            if (model.visualSelectorHost == null) return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await visualSelector.AnimateHeightRequest(0);
                visualSelector.IsVisible = false;
                InputView input = model.visualSelectorHost; // keep a reference to it before resetting
                model.visualSelectorHost = null; // reset before re-focusing entry because its handler may check model.visualSelectorHost
                form.HeightRequest = -1; // reset form height
                input.Focus(); // re-focus the entry to keep its help and preview or errors open
            });
        }

        #region Sizing
        private ScrollView? pickedSelectorScroller;
        private Debouncer? debouncedUpdateHeight;

        private void SetupAutoSizing()
        {
            debouncedUpdateHeight = new(TimeSpan.FromMilliseconds(100), UndebouncedUpdateHeightAsync,
                async ex => await ErrorReport.WriteAsyncAndShare(ex.ToString(), "updating visual selector height"));

            /* eagerly subscribe to the SizeChanged of visuals influencing the required container height
             * for when ShowPickedSelector is true and it has dynamic height, see UpdateHeight */
            pickedSelectorScroller!.Content.SizeChanged += (sender, e) =>
            {
                // exit handler early if height update is unnecessary
                if (!visualSelector.IsVisible) return;
                UpdateHeight();
            };
        }

        private void UpdateHeight() => debouncedUpdateHeight!.Run();

        private async Task UndebouncedUpdateHeightAsync()
        {
            var maxHeight = Height - 100;
            double height;

            if (model.ShowSelectorOptions) // calculate dynamic height based on measured selectorControls sizes
            {
                height = pickedSelectorScroller!.Content.Height;

                if (maxHeight < height)
                {
                    pickedSelectorScroller.HeightRequest = maxHeight;
                    height = maxHeight;
                }
                else pickedSelectorScroller.HeightRequest = -1; // allow scroller to re-shrink
            }
            else
            {
                height = maxHeight;
                pickedSelectorScroller!.HeightRequest = -1; // allow scroller to re-shrink
            }

            await visualSelector.AnimateHeightRequest(height);

            if (model.visualSelectorHost != null)
            {
                form.HeightRequest = Height - height; // shrink form so End is visible to scroll there

                // scroll entry to end so that Help above is visible
                await form.ScrollToAsync(model.visualSelectorHost, ScrollToPosition.End, animated: true);
            }
            else form.HeightRequest = -1; // reset form height
        }
        #endregion
    }
}
