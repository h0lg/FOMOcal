using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using FomoCal.Gui.Resources;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;
using SelectorOptionsRepo = SingletonJsonFileRepository<VenueEditor.SelectorOptions>;

partial class VenueEditor
{
    private readonly SelectorOptions selectorOptions = new() { SemanticClasses = true, LayoutClasses = true };
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayedSelector))] public partial string? PickedSelector { get; set; }
    [ObservableProperty] public partial bool EnablePicking { get; set; } = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayedSelector))] public partial bool ShowSelectorOptions { get; set; }
    [ObservableProperty] public partial bool ShowSelectorDetail { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayedSelector))] public partial bool IncludePickedSelectorPath { get; set; }

    public string? DisplayedSelector
    {
        get
        {
            if (PickedSelector.IsNullOrWhiteSpace()) return null;
            if (IncludePickedSelectorPath) return ShowSelectorOptions ? PickedSelector : PickedSelector.NormalizeWhitespace();
            return AutomatedEventPageView.GetLeafSelector(PickedSelector!, selectorOptions.XPathSyntax);
        }
    }

    internal partial class SelectorOptions : AutomatedEventPageView.PickedSelectorOptions
    {
        public bool IncludePickedSelectorPath { get; set; }
    }

    partial class Page
    {
        private readonly AbsoluteLayout visualSelector;
        private AutomatedEventPageView? pageView;
        private string? selectedQuery;
        private SelectorOptionsRepo? selectorOptionsRepo;

        private AbsoluteLayout CreateVisualSelector()
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
                model.IsEventPageLoading = false;
                model.RevealMore();
            };

            pageView.ErrorLoading += async navigationResult =>
            {
                model.previewedEvents = null;
                model.programDocument = null;
                model.IsEventPageLoading = false;
                string suffix = navigationResult == WebNavigationResult.Cancel ? "ed" : "";
                var message = $"Navigation {navigationResult}{suffix}.";

                // using ErrorLoading to give user feedback about an invalid URL instead of validating before
                if (navigationResult == WebNavigationResult.Failure && !model.ProgramUrl.IsValidHttpUrl())
                    message += $" '{model.ProgramUrl}' is not a valid HTTP URL.";

                await App.CurrentPage.DisplayAlert("Error loading event page.", message, "Ok");
                model.RevealMore();
            };

            pageView.PickedSelector += selector => model.PickedSelector = selector;

            model.PropertyChanged += async (o, e) =>
            {
                if (e.PropertyName == nameof(ProgramUrl))
                    pageView.Source = model.ProgramUrl;
                else if (e.PropertyName == nameof(EnablePicking))
                    await pageView.EnablePicking(model.EnablePicking);
                else if (e.PropertyName == nameof(WaitForJsRendering)
                    || (e.PropertyName == nameof(EventSelector) && model.WaitForJsRendering))
                    Reload();
            };

            const string displayedSelector = nameof(DisplayedSelector),
                showSelectorOptions = nameof(ShowSelectorOptions);

            Label help = new();

            var controlsAndInstructions = HWrap(5,
                Swtch(nameof(EnablePicking)).Wrapper
                    .BindVisible(showSelectorOptions, converter: Converters.Not)
                    .InlineTooltipOnFocus(HelpTexts.EnablePicking, help),
                Lbl("Tap a page element to pick it.").TapGesture(TogglePicking)
                    .BindVisible(showSelectorOptions, converter: Converters.Not),
                Btn("⿴ Pick its container").TapGesture(PickParent)
                    .BindVisible(new Binding(displayedSelector, converter: Converters.IsSignificant),
                        Converters.And, new Binding(showSelectorOptions, converter: Converters.Not)),
                Lbl("if you need.")
                    .BindVisible(new Binding(displayedSelector, converter: Converters.IsSignificant),
                        Converters.And, new Binding(showSelectorOptions, converter: Converters.Not)),
                new Button().BindIsVisibleToValueOf(displayedSelector).TapGesture(TogglePickedSelector)
                    .Bind(Button.TextProperty, showSelectorOptions,
                        convert: static (bool showSelector) => showSelector ? "⏮ Back to ⛶ picking an element" : "🥢 Choose a selector next ⏭"));

            var xPathSyntax = new Switch() // enables switching between CSS and XPath syntax to save space
                .Bind(Switch.IsToggledProperty, nameof(SelectorOptions.XPathSyntax), source: model.selectorOptions)
                .InlineTooltipOnFocus(string.Format(HelpTexts.SelectorSyntaxFormat, string.Format(FomoCal.ScrapeJob.XPathSelectorFormat, "selector")), help);

            var syntax = HStack(5, Lbl("Syntax").Bold(), Lbl("CSS"), SwtchWrp(xPathSyntax), Lbl("XPath"));

            View[] selectorDetails = [syntax,
                Lbl("detail").Bold(),
                LbldView("ancestor path", Check(nameof(IncludePickedSelectorPath))
                    .InlineTooltipOnFocus(HelpTexts.IncludePickedSelectorPath, help)),
                SelectorOption("tag name", nameof(SelectorOptions.TagName), HelpTexts.TagName),
                SelectorOption("id", nameof(SelectorOptions.Ids), HelpTexts.ElementId),
                Lbl("classes").Bold(),
                SelectorOption("with style", nameof(SelectorOptions.LayoutClasses), string.Format(HelpTexts.ClassesWith_Style, "")),
                SelectorOption("without", nameof(SelectorOptions.SemanticClasses), string.Format(HelpTexts.ClassesWith_Style, " no")),
                Lbl("other attibutes").Bold(),
                SelectorOption("names", nameof(SelectorOptions.OtherAttributes), HelpTexts.OtherAttributes),
                SelectorOption("values", nameof(SelectorOptions.OtherAttributeValues), HelpTexts.OtherAttributeValues),
                SelectorOption("position", nameof(SelectorOptions.Position), HelpTexts.ElementPosition)];

            View[] appendSelection = [
                Lbl("Select parts of the selector text and"),
                Btn("➕ append").TapGesture(AppendSelectedQuery)
                    .InlineTooltipOnFocus(HelpTexts.AppendSelectedQuery, help),
                Lbl("them to your query to try them out."),
                Btn("🍜 selector options").BindVisible(showSelectorOptions).TapGesture(ToggleSelectorDetail)
                    .InlineTooltipOnFocus(HelpTexts.ToggleSelectorDetail, help)];

            foreach (var view in appendSelection)
                controlsAndInstructions.AddChild(view.BindVisible(showSelectorOptions));

            foreach (var view in selectorDetails)
                controlsAndInstructions.AddChild(
                    view.BindVisible(new Binding(showSelectorOptions),
                        Converters.And, new Binding(nameof(ShowSelectorDetail))));

            Editor selectorDisplay = SelectorDisplay(displayedSelector)
                .InlineTooltipOnFocus(HelpTexts.PickedSelectorDisplay, help);

            pickedSelectorScroller = new()
            {
                Content = Grd(cols: [Star], rows: [Auto, Auto, Auto], spacing: 0,
                controlsAndInstructions.View,
                help.BindVisible(showSelectorOptions).Row(1), selectorDisplay.Row(2))
            };

            SetupAutoSizing();

            return new()
            {
                IsVisible = false,
                StyleClass = ["VisualSelector"],
                HeightRequest = 0, // to initialize it collapsed and fix first opening animation
                Children = {
                    Grd(cols: [Star], rows: [Auto, Star], spacing: 0,
                        pickedSelectorScroller,
                        pageView
                            .ToolTip("You may find it useful to zoom  the page using [Ctrl] + MouseWheel or try the 'Inspect' tool from the right-click context menu.")
                            .BindVisible(showSelectorOptions, converter: Converters.Not).Row(3))
                        .LayoutBounds(0, 0, 1, 1).LayoutFlags(AbsoluteLayoutFlags.SizeProportional), // full size
                    Btn("🗙").TapGesture(HideVisualSelector).Size(30, 30).TranslationY(-35) // float half above upper boundary
                        .LayoutBounds(0.99, 0, -1, -1).LayoutFlags(AbsoluteLayoutFlags.PositionProportional) // position on the right, autosized
                }
            };

            HorizontalStackLayout SelectorOption(string label, string isCheckedPropertyPath, string helpText)
                => LbldView(label, Check(isCheckedPropertyPath, source: model.selectorOptions).InlineTooltipOnFocus(helpText, help));
        }

        private Editor SelectorDisplay(string propertyPath)
        {
            var editor = new Editor { IsReadOnly = true, AutoSize = EditorAutoSizeOption.TextChanges }
                .Bind(Editor.TextProperty, propertyPath, BindingMode.OneWay);

            // save selected part of selector query for AppendSelectedQuery
            editor.Unfocused += (o, e) => selectedQuery = editor.Text?.Substring(editor.CursorPosition, editor.SelectionLength);
            return editor;
        }

        private void TogglePicking() => model.EnablePicking = !model.EnablePicking;
        private async void PickParent() => await pageView!.PickParent();
        private void TogglePickedSelector() => model.ShowSelectorOptions = !model.ShowSelectorOptions;
        private void ToggleSelectorDetail() => model.ShowSelectorDetail = !model.ShowSelectorDetail;

        private void AppendSelectedQuery()
        {
            var xpathMatch = FomoCal.ScrapeJob.XpathSelectorPattern().Match(model.visualSelectorHost!.Text ?? "");
            string normalized = selectedQuery.NormalizeWhitespace();

            if (model.selectorOptions.XPathSyntax)
            {
                var selector = xpathMatch.Success ? xpathMatch.Value + normalized : normalized; // discard CSS query
                model.visualSelectorHost!.Text = FomoCal.ScrapeJob.FormatXpathSelector(selector);
            }
            else model.visualSelectorHost!.Text = xpathMatch.Success ? normalized // discard XPath query
                    : model.visualSelectorHost!.Text + " " + normalized;
        }

        private void Reload()
        {
            model.IsEventPageLoading = true;
            pageView!.Reload();
        }

        private async Task ShowVisualSelectorForAsync(Entry entry, string selector, bool descendant)
        {
            // reset UI state to allow picking an element
            model.ShowSelectorOptions = false;
            model.ShowSelectorDetail = false;
            model.EnablePicking = true;

            if (selectorOptionsRepo == null) // load and restore remembered selectorOptions once from JSON file
            {
                selectorOptionsRepo = IPlatformApplication.Current!.Services.GetService<SelectorOptionsRepo>();
                var saved = await selectorOptionsRepo!.LoadAsync();

                if (saved != null)
                {
                    model.IncludePickedSelectorPath = saved.IncludePickedSelectorPath;

                    foreach (var prop in typeof(SelectorOptions).GetProperties())
                        prop.SetValue(model.selectorOptions, prop.GetValue(saved, null));

                    await pageView!.SetPickedSelectorDetail(model.selectorOptions);
                }

                // hook up change handlers saving changes after restoration
                model.PropertyChanged += async (o, e) =>
                {
                    if (e.PropertyName == nameof(IncludePickedSelectorPath))
                    {
                        model.selectorOptions.IncludePickedSelectorPath = model.IncludePickedSelectorPath;
                        await selectorOptionsRepo!.SaveAsync(model.selectorOptions);
                    }
                };

                model.selectorOptions.PropertyChanged += async (o, e) =>
                {
                    await pageView!.SetPickedSelectorDetail(model.selectorOptions);
                    await selectorOptionsRepo!.SaveAsync(model.selectorOptions);
                };
            }

            model.visualSelectorHost = entry;
            entry.Focus(); // to keep its help open
            visualSelector.IsVisible = true;
            await pageView!.PickRelativeTo(selector, descendant);
            await UpdateHeightAsync();
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
                form.HeightRequest = -1; // reset form height
                entry.Focus(); // re-focus the entry to keep its help and preview or errors open
            });
        }

        #region Sizing
        private CancellationTokenSource? sizeChangedCts;
        private ScrollView? pickedSelectorScroller;

        private void SetupAutoSizing()
        {
            /* eagerly subscribe to the SizeChanged of visuals influencing the required container height
             * for when ShowPickedSelector is true and it has dynamic height, see UpdateHeightAsync */
            pickedSelectorScroller!.Content.SizeChanged += async (sender, e) =>
            {
                // exit handler early if height update is unnecessary
                if (!visualSelector.IsVisible) return;
                await UpdateHeightAsync();
            };
        }

        private Task UpdateHeightAsync()
        {
            sizeChangedCts?.Cancel(); // cancel any previous waiting task
            sizeChangedCts = new CancellationTokenSource();
            var token = sizeChangedCts.Token;

            return Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(100, token); // debounce delay

                    if (!token.IsCancellationRequested)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            var maxHeight = Height - 100;
                            double height = 0;

                            if (model.ShowSelectorOptions) // calculate dynamic height based on measured selectorControls sizes
                            {
                                height = pickedSelectorScroller!.Content.Height;

                                if (maxHeight < height)
                                {
                                    pickedSelectorScroller.HeightRequest = maxHeight;
                                    height = maxHeight;
                                }
                                else pickedSelectorScroller.HeightRequest = -1;
                            }
                            else height = maxHeight;

                            await visualSelector.AnimateHeightRequest(height);

                            if (model.visualSelectorHost != null)
                            {
                                form.HeightRequest = Height - height; // shrink form so End is visible to scroll there

                                // scroll entry to end so that Help above is visible
                                await form.ScrollToAsync(model.visualSelectorHost, ScrollToPosition.End, animated: true);
                            }
                            else form.HeightRequest = -1; // reset form height
                        });
                    }
                }
                catch (TaskCanceledException) { } // ignore cancellation
            });
        }
        #endregion
    }
}
