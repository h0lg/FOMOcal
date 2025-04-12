using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using FomoCal.Gui.Resources;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;
using SelectorOptions = FomoCal.Gui.ViewModels.AutomatedEventPageView.PickedSelectorOptions;

namespace FomoCal.Gui.ViewModels;

partial class VenueEditor
{
    private readonly SelectorOptions selectorOptions = new() { SemanticClasses = true, LayoutClasses = true };
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayedSelector))] private string? pickedSelector;
    [ObservableProperty] private bool enablePicking = true;
    [ObservableProperty] private bool showPickedSelector;
    [ObservableProperty] private bool showSelectorDetail;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayedSelector))] private bool includePickedSelectorPath;

    public string? DisplayedSelector
    {
        get
        {
            if (IncludePickedSelectorPath || PickedSelector.IsNullOrWhiteSpace()) return PickedSelector;
            return AutomatedEventPageView.GetLeafSelector(PickedSelector!, selectorOptions.XPathSyntax);
        }
    }

    partial class Page
    {
        private readonly AbsoluteLayout visualSelector;
        private AutomatedEventPageView? pageView;
        private string? selectedQuery;

        private AbsoluteLayout CreateVisualSelector()
        {
            pageView = new(model.venue);
            model.selectorOptions.PropertyChanged += (o, e) => pageView.SetPickedSelectorDetail(model.selectorOptions);

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

            pageView.PickedSelector += selector => model.PickedSelector = selector;

            model.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == nameof(ProgramUrl))
                    pageView.Source = model.ProgramUrl;
                else if (e.PropertyName == nameof(EnablePicking))
                    pageView.EnablePicking(model.EnablePicking);
                else if (e.PropertyName == nameof(WaitForJsRendering)
                    || (e.PropertyName == nameof(EventSelector) && model.WaitForJsRendering))
                    Reload();
            };

            const string displayedSelector = nameof(DisplayedSelector),
                showPickedSelector = nameof(ShowPickedSelector);

            var controlsAndInstructions = HWrap(5,
                Swtch(nameof(EnablePicking)).Wrapper
                    .BindVisible(showPickedSelector, converter: Converters.Not)
                    .ToolTip(HelpTexts.EnablePicking),
                Lbl("Tap a page element to pick it.").TapGesture(TogglePicking)
                    .BindVisible(showPickedSelector, converter: Converters.Not),
                Btn("⿴ Pick its container").TapGesture(PickParent)
                    .BindVisible(new Binding(displayedSelector, converter: Converters.IsSignificant),
                        Converters.And, new Binding(showPickedSelector, converter: Converters.Not)),
                Lbl("if you need.")
                    .BindVisible(new Binding(displayedSelector, converter: Converters.IsSignificant),
                        Converters.And, new Binding(showPickedSelector, converter: Converters.Not)),
                new Button().BindIsVisibleToValueOf(displayedSelector).TapGesture(TogglePickedSelector)
                    .Bind(Button.TextProperty, showPickedSelector,
                        convert: static (bool showSelector) => showSelector ? "⏮ Back to ⛶ picking an element" : "🥢 Choose a selector next ⏭"));

            var xPathSyntax = new Switch() // enables switching between CSS and XPath syntax to save space
                .Bind(Switch.IsToggledProperty, nameof(SelectorOptions.XPathSyntax), source: model.selectorOptions)
                .ToolTip(string.Format(HelpTexts.SelectorSyntaxFormat, string.Format(FomoCal.ScrapeJob.XPathSelectorFormat, "selector")));

            var syntax = HStack(5, Lbl("Syntax").Bold(), Lbl("CSS"), SwtchWrp(xPathSyntax), Lbl("XPath"));

            View[] selectorDetails = [syntax,
                Lbl("detail").Bold(),
                LbldView("ancestor path", Check(nameof(IncludePickedSelectorPath))),
                SelectorOption("tag name", nameof(SelectorOptions.TagName)),
                SelectorOption("id", nameof(SelectorOptions.Ids)),
                Lbl("classes").Bold(),
                SelectorOption("with style", nameof(SelectorOptions.LayoutClasses)),
                SelectorOption("without", nameof(SelectorOptions.SemanticClasses)),
                Lbl("other attibutes").Bold(),
                SelectorOption("names", nameof(SelectorOptions.OtherAttributes)),
                SelectorOption("values", nameof(SelectorOptions.OtherAttributeValues)),
                SelectorOption("position", nameof(SelectorOptions.Position))];

            View[] appendSelection = [
                Lbl("Select parts of the selector text and"),
                Btn("➕ append").TapGesture(AppendSelectedQuery).ToolTip(HelpTexts.AppendSelectedQuery),
                Lbl("them to your query to try them out."),
                Btn("🍜 selector options").BindVisible(showPickedSelector).TapGesture(ToggleSelectorDetail)];

            foreach (var view in appendSelection)
                controlsAndInstructions.AddChild(view.BindVisible(showPickedSelector));

            foreach (var view in selectorDetails)
                controlsAndInstructions.AddChild(
                    view.BindVisible(new Binding(showPickedSelector),
                        Converters.And, new Binding(nameof(ShowSelectorDetail))));

            Editor selectorDisplay = SelectorDisplay(displayedSelector).BindVisible(showPickedSelector);
            SetupAutoSizing(controlsAndInstructions.View, selectorDisplay);

            return new()
            {
                IsVisible = false,
                StyleClass = ["VisualSelector"],
                HeightRequest = 0, // to initialize it collapsed and fix first opening animation
                Children = {
                    Grd(cols: [Star], rows: [Auto, Auto, Star], spacing: 0,
                        controlsAndInstructions.View,
                        selectorDisplay.Row(1).ColumnSpan(2),
                        pageView.BindVisible(showPickedSelector, converter: Converters.Not).Row(2)).LayoutBounds(0, 0, 1, 1).LayoutFlags(AbsoluteLayoutFlags.SizeProportional), // full size
                    Btn("🗙").TapGesture(HideVisualSelector).Size(30, 30).TranslationY(-35) // float half above upper boundary
                        .LayoutBounds(0.99, 0, -1, -1).LayoutFlags(AbsoluteLayoutFlags.PositionProportional) // position on the right, autosized
                }
            };
        }

        private HorizontalStackLayout SelectorOption(string label, string isCheckedPropertyPath)
            => LbldView(label, Check(isCheckedPropertyPath, source: model.selectorOptions));

        private Editor SelectorDisplay(string propertyPath)
        {
            var editor = new Editor { IsReadOnly = true, AutoSize = EditorAutoSizeOption.TextChanges }
                .ToolTip(HelpTexts.PickedSelectorDisplay)
                .Bind(Editor.TextProperty, propertyPath, BindingMode.OneWay);

            // save selected part of selector query for AppendSelectedQuery
            editor.Unfocused += (o, e) => selectedQuery = editor.Text.Substring(editor.CursorPosition, editor.SelectionLength);
            return editor;
        }

        private void TogglePicking() => model.EnablePicking = !model.EnablePicking;
        private async void PickParent() => await pageView!.PickParent();
        private void TogglePickedSelector() => model.ShowPickedSelector = !model.ShowPickedSelector;
        private void ToggleSelectorDetail() => model.ShowSelectorDetail = !model.ShowSelectorDetail;

        private void AppendSelectedQuery()
        {
            var xpathMatch = FomoCal.ScrapeJob.XpathSelectorPattern.Match(model.visualSelectorHost!.Text);
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
            model.ShowPickedSelector = false;
            model.ShowSelectorDetail = false;
            model.EnablePicking = true;

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
        private readonly SemaphoreSlim heightUpdate = new(1, 1);
        private VisualElement[]? selectorControls; // used to measure required size of container when ShowPickedSelector

        private void SetupAutoSizing(params VisualElement[] selectorControls)
        {
            this.selectorControls = selectorControls;

            /* eagerly subscribe to the SizeChanged of visuals influencing the required container height
             * for when ShowPickedSelector is true and it has dynamic height, see UpdateHeightAsync */
            foreach (var vis in (VisualElement[])[.. selectorControls, this])
                vis.SizeChanged += async (object? sender, EventArgs e) =>
                {
                    // exit handler early if height update is unnecessary
                    if (!visualSelector.IsVisible) return;
                    await UpdateHeightAsync();
                };
        }

        private async Task UpdateHeightAsync()
        {
            if (heightUpdate.CurrentCount == 0) return; // drop parallel update requests
            heightUpdate.Wait();
            double height = 0;

            if (model.ShowPickedSelector) // calculate dynamic height based on measured selectorControls sizes
                foreach (var child in selectorControls!)
                {
                    var measuredSize = child.Measure(Width, double.PositiveInfinity);
                    height += measuredSize.Height;
                }
            else height = Height - 100;

            await visualSelector.AnimateHeightRequest(height);

            if (model.visualSelectorHost != null)
            {
                form.HeightRequest = Height - height; // shrink form so End is visible to scroll there

                // scroll entry to end so that Help above is visible
                await form.ScrollToAsync(model.visualSelectorHost, ScrollToPosition.End, animated: true);
            }
            else form.HeightRequest = -1; // reset form height

            heightUpdate.Release();
        }
        #endregion
    }
}
