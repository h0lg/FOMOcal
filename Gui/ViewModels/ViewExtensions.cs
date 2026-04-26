using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.Resources;

namespace FomoCal.Gui.ViewModels;

internal static class Glyphs
{
    internal const string Add = "➕",
        Comment = "💬",
        Date = "📆 ",
        Delete = "🗑",
        Deselect = "☐ ",
        DoorPrice = "💵 ",
        Doors = "🚪 ",
        Edit = "✏",
        Error = "⚠",
        EventPage = "📰 ",
        Export = "🎁",
        Genres = "🎶 ",
        Html = "🌐 ",
        Link = "🔗",
        Lucky = "🍀 ",
        PresalePrice = "💳 ",
        Refresh = "⟳",
        Scrape = "⛏",
        Select = "☑ ",
        Settings = "🛠",
        Stage = "🏛 ",
        Start = "🎼 ",
        Target = "🎯 ",
        Test = "🧪",
        Text = "𝕋 ",
        Tickets = "🎫 ",
        Venue = "🏟 ";
}

internal static class Styles
{
    internal static class Label
    {
        internal static string TitleView = GetName(), Headline = GetName(), SubHeadline = GetName(),
            Demoted = GetName(), EndingEntryButton = GetName(), VenueRowDetail = GetName();
    }

    internal static class Editor
    {
        internal static Style Error = Get(nameof(Editor)),
            Success = Get(nameof(Editor));
    }

    internal static class Span
    {
        internal static Style Link = Get(), Highlit = Get(), Normal = Get(),
            HelpHeader = Get(), Help = Get(),
            HelpLink = Get(), HelpFooter = Get(),
            HelpFooterLink = MergedStyle.Combine(HelpFooter, HelpLink)!;

        private static Style Get([CallerMemberName] string key = "") => Styles.Get(nameof(Span), key);
    }

    internal static class Border
    {
        internal static string RoundedSection = GetName(), Error = GetName(),
            EndingEntryButton = GetName(), ScrapedValue = GetName(), ListItem = GetName();
    }

    internal static class Button
    {
        internal static string Secondary = GetName(), Tertiary = GetName(),
            Transparent = GetName(), SwipeItem = GetName();
    }

    internal static class RadioButton
    {
        internal static string SingleSelectToggleButton = GetName();
    }

    internal static class VisualElement
    {
        internal static string ChromeBg = GetName(), NormalBg = GetName(),
            ListItemBg = GetName(), SelectableListItem = GetName();
    }

    private static string GetName([CallerMemberName] string key = "") => key;

    private static Style Get(string typeName, [CallerMemberName] string key = "")
        => (Style)Application.Current!.Resources[typeName + "." + key];
}

internal static partial class ViewExtensions
{
    internal static T OnFocusChanged<T>(this T vis, Action<VisualElement, bool> setFocused) where T : VisualElement
    {
        vis.Focused += (_, _) => setFocused(vis, true);
        vis.Unfocused += (_, _) => setFocused(vis, false);
        return vis;
    }

    internal static T InlineTooltipOnFocus<T>(this T host, string tooltip, (Label label, Border layout) help,
        Action<VisualElement, bool>? onFocusChanged = null,
        Func<VisualElement, bool, bool>? cancelFocusChanged = null) where T : VisualElement
        => host.ToolTip(tooltip).OnFocusChanged(async (vis, focused) =>
        {
            if (cancelFocusChanged?.Invoke(vis, focused) == true) return;
            onFocusChanged?.Invoke(vis, focused);
            vis.ToolTip(focused ? null : tooltip); // prevent tooltip from overlaying help on focus
            await help.InlineHelpTextAsync(tooltip, vis, focused);
        });

    internal static async Task InlineHelpTextAsync(this (Label label, Border layout) help, string tooltip, VisualElement host, bool focused)
    {
        if (focused)
        {
            help.label.BindingContext = host; // abusing unused BindingContext to remember host
            help.label.FormattedText = tooltip.ParseMarkdown(); // set its help text
            help.layout.IsVisible = true;
            await Task.WhenAll(help.layout.FadeToAsync(1, 300), help.layout.ScaleToAsync(1, 300, Easing.CubicOut)); // show label
        }
        else if (help.label.BindingContext == host) // only react to unfocused if remembered host matches
        {
            help.label.BindingContext = null; // reset remembered host
            await Task.Delay(100); // defer unfocus reaction to allow another control to take focus
            if (help.label.BindingContext != null) return; // another host took focus, do not hide

            if (help.layout.IsLoaded) // hide label, guarding against layout being already unloaded when closing app while help is showing
                await Task.WhenAll(help.layout.FadeToAsync(0, 300), help.layout.ScaleToAsync(0, 300, Easing.CubicIn));

            if (help.layout.IsLoaded) help.layout.IsVisible = false;
            if (help.label.IsLoaded && help.label.BindingContext == null) help.label.FormattedText = null; // only reset content if no other host took focus
        }
    }

    internal static T StyleClass<T>(this T styleable, string styleClass) where T : StyleableElement
    {
        if (styleClass != null) styleable.StyleClass = [styleClass];
        return styleable;
    }

    internal static T ToolTip<T>(this T bindable, string? text) where T : BindableObject
    {
        ToolTipProperties.SetText(bindable, text!);
        return bindable;
    }

    internal static T BindVisible<T>(this T vis, string property, object? source = null, IValueConverter? converter = null) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, property, converter: converter, source: source);

    internal static T BindVisible<T>(this T vis, BindingBase binding1,
        Func<ValueTuple<bool, bool>, bool> predicate, BindingBase binding2) where T : VisualElement
        => vis.BindVisible<T, bool, bool>(binding1, binding2, predicate);

    internal static Tvis BindVisible<Tvis, Tbinding1, Tbinding2>(this Tvis vis, BindingBase binding1,
        BindingBase binding2, Func<ValueTuple<Tbinding1?, Tbinding2?>, bool> predicate) where Tvis : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, binding1, binding2, convert: predicate);

    /// <summary>Binds the visibility of <paramref name="vis"/> to whether
    /// the <paramref name="textProperty"/> is <see cref="Converters.IsSignificant"/>,
    /// i.e. not null or white space.</summary>
    internal static T BindVisibleToSignificanceOf<T>(this T vis, string textProperty) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, textProperty, converter: Converters.IsSignificant);

    /// <summary>Binds the visibility of <paramref name="vis"/> to whether
    /// the nullable <paramref name="property"/> of the current binding context has a value.</summary>
    internal static T BindIsVisibleToHasValueOf<T, TProp>(this T vis, string property) where T : VisualElement where TProp : struct
        => vis.Bind(VisualElement.IsVisibleProperty, property, converter: Converters<TProp>.HasValue);

    /// <summary>Binds the visibility of <paramref name="vis"/> to whether
    /// the reference typed <paramref name="property"/> of the current binding context is not null.</summary>
    internal static T BindVisibleToNotNullOf<T>(this T vis, string property) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, property, converter: Converters.NotNull);

    /// <summary>Binds the <see cref="RadioButtonGroup.SelectedValueProperty"/>
    /// to the given <paramref name="layout"/> and <paramref name="pathToSelectedValue"/>.</summary>
    /// <param name="pathToGroupName">The unique group name to bind to - for use inside <see cref="DataTemplate"/>s.</param>
    internal static T BindRadioButtonGroupSelectedValue<T>(this T layout, string pathToSelectedValue,
        string? pathToGroupName = null) where T : BindableObject
    {
        layout.Bind(RadioButtonGroup.SelectedValueProperty, pathToSelectedValue);

        // setting or binding group name seems to be required for RadioButtonGroup.SelectedValueProperty binding to work
        if (pathToGroupName == null) RadioButtonGroup.SetGroupName(layout, pathToSelectedValue);
        else layout.Bind(RadioButtonGroup.GroupNameProperty, pathToGroupName);

        return layout;
    }

    internal static Label Wrap(this Label label)
    {
        label.LineBreakMode = LineBreakMode.WordWrap;
        return label;
    }

    [GeneratedRegex(@"^(#+)\s+(.*)")] private static partial Regex HeaderRegex(); // e.g. # Heading
    private const string footerPrefix = "^^";

    internal static FormattedString ParseMarkdown(this string text)
    {
        FormattedString formatted = new();

        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string? line = lines[i];
            var trimmedLine = line.Trim();

            if (trimmedLine.IsNullOrWhiteSpace())
            {
                AppendEmptyLine(formatted, lines, i);
                continue;
            }

            // Detect footer
            if (trimmedLine.StartsWith(footerPrefix))
            {
                var footerText = trimmedLine[footerPrefix.Length..].TrimStart();
                AppendWithLinks(formatted, footerText, Styles.Span.HelpFooterLink, Styles.Span.HelpFooter);
                AppendEmptyLine(formatted, lines, i);
                continue;
            }

            // Detect headers
            var headerMatch = HeaderRegex().Match(trimmedLine);

            if (headerMatch.Success)
            {
                // also available if useful: int level = headerMatch.Groups[1].Value.Length;
                string headerText = headerMatch.Groups[2].Value;

                formatted.Spans.Add(new Span
                {
                    Text = headerText + Environment.NewLine,
                    Style = Styles.Span.HelpHeader
                });

                continue;
            }

            // Default: normal paragraph with possible links
            AppendWithLinks(formatted, trimmedLine, Styles.Span.HelpLink, Styles.Span.Help);
            AppendEmptyLine(formatted, lines, i);
        }

        return formatted;

        static void AppendEmptyLine(FormattedString formatted, string[] lines, int lineIndex)
        {
            // only append empty line if there are more lines, don't end on one
            if (lineIndex < lines.Length - 1) formatted.Spans.Add(new Span { Text = Environment.NewLine });
        }

        static void AppendWithLinks(FormattedString target, string text, Style linkStyle, Style? normalStyle)
        {
            foreach ((string display, string? url) in text.ChunkByLinksAndUrls())
            {
                Span chunk = new() { Text = display };

                if (url == null) chunk.Style = normalStyle;
                else
                {
                    chunk.Style = linkStyle;
                    chunk.TapGesture(() => WebViewPage.OpenUrlAsync(url));
                }

                target.Spans.Add(chunk);
            }
        }
    }

    internal static Task AnimateHeightRequest(this VisualElement view, double endValue, uint duration = 300)
    {
        var tcs = new TaskCompletionSource<bool>(); // Create a task to await
        var animation = new Animation(v => view.HeightRequest = v, view.HeightRequest, endValue);

        animation.Commit(view, name: nameof(AnimateHeightRequest), length: duration, easing: Easing.CubicOut,
            finished: (_, _) => tcs.SetResult(true));

        return tcs.Task; // Await the completion of the animation
    }

    /// <summary>Makes the multi-line <paramref name="input"/> shrink to display its contents
    /// while fit in the same <see cref="FlexLayout"/> row if possible
    /// - but grow if necessary to use the remaining space.</summary>
    internal static T FlexFitContents<T>(this T input) where T : BindableObject
    {
        FlexLayout.SetGrow(input, 1);
        FlexLayout.SetShrink(input, 1);
        return input; // to support chaining
    }

    internal static VisualElement? FindTopLayout(this Element element)
    {
        if (element is Layout layout) return layout;
        if (element is ContentPage page) return FindTopLayout(page.Content);
        if (element is ContentView contentView) return FindTopLayout(contentView.Content);
        if (element is ScrollView scrollView) return FindTopLayout(scrollView.Content);
        return null;
    }

    internal static string? GetHelp(this Venue.PagingStrategy strategy)
        => HelpTexts.ResourceManager.GetString(nameof(Venue.PagingStrategy) + strategy.ToString());

    internal static IEnumerable<Event> GetEvents(this IEnumerable<EventList.EventView> views) => views.Select(v => v.Model);
}

internal static class Converters
{
    internal static FuncConverter<bool, bool> Not = new(value => !value, value => !value);
    internal static Func<ValueTuple<bool, bool>, bool> Or = ((bool a, bool b) values) => values.a || values.b;
    internal static Func<ValueTuple<bool, bool>, bool> And = ((bool a, bool b) values) => values.a && values.b;
    internal static FuncConverter<string, bool> IsSignificant = new(value => value.IsSignificant());
    internal static FuncConverter<object, bool> NotNull = new(value => value != null);
    internal static FuncConverter<T, bool> Predicate<T>(Func<T?, bool> predicate) => new(predicate);
    internal static FuncConverter<Tin?, Tout?> Func<Tin, Tout>(Func<Tin?, Tout?> convert) => new(convert);
}

internal static class Converters<T> where T : struct
{
    internal static FuncConverter<T?, bool> HasValue = new(static value => value.HasValue);
}
