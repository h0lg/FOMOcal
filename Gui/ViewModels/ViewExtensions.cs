using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

internal static class Styles
{
    internal static class Label
    {
        internal static string Headline = GetName(), SubHeadline = GetName(),
            Demoted = GetName(), VenueRowDetail = GetName();
    }

    internal static class Editor
    {
        internal static string Error = GetName(), Success = GetName();
    }

    internal static class Span
    {
        internal static Style LinkSpan = Get(), HelpHeaderSpan = Get(), HelpSpan = Get(),
            HelpLinkSpan = Get(), HelpFooterSpan = Get(),
            HelpFooterLinkSpan = MergedStyle.Combine(HelpFooterSpan, HelpLinkSpan)!;
    }

    private static string GetName([CallerMemberName] string key = "") => key;
    private static Style Get([CallerMemberName] string key = "") => (Style)Application.Current!.Resources[key];
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
            await Task.WhenAll(help.layout.FadeTo(1, 300), help.layout.ScaleTo(1, 300, Easing.CubicOut)); // show label
        }
        else if (help.label.BindingContext == host) // only react to unfocused if remembered host matches
        {
            help.label.BindingContext = null; // reset remembered host
            await Task.Delay(100); // defer unfocus reaction to allow another control to take focus
            if (help.label.BindingContext != null) return; // another host took focus, do not hide
            await Task.WhenAll(help.layout.FadeTo(0, 300), help.layout.ScaleTo(0, 300, Easing.CubicIn)); // hide label
            if (help.label.BindingContext == null) help.label.FormattedText = null; // only reset content if no other host took focus
        }
    }

    internal static FormattedString? FormatTooltip(this VisualElement vis)
        => ToolTipProperties.GetText(vis)?.ToString()?.ParseMarkdown();

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
        Func<ValueTuple<bool, bool>, bool> convert, BindingBase binding2) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, binding1, binding2, convert: convert);

    internal static T BindIsVisibleToValueOf<T>(this T vis, string textProperty) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, textProperty, converter: Converters.IsSignificant);

    internal static T BindIsVisibleToHasValueOf<T, TProp>(this T vis, string textProperty) where T : VisualElement where TProp : struct
        => vis.Bind(VisualElement.IsVisibleProperty, textProperty, converter: Converters<TProp>.HasValue);

    internal static T OnTextChanged<T>(this T input, Action<TextChangedEventArgs> handle) where T : InputView
    {
        input.TextChanged += (sender, e) => handle(e);
        return input;
    }

    internal static Label Wrap(this Label label)
    {
        label.LineBreakMode = LineBreakMode.WordWrap;
        return label;
    }

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\((?<url>https?:\/\/[^\s)]+)\)|(?<urlonly>https?:\/\/[^\s\[\]()]+)")]
    private static partial Regex LinkRegex();

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
                AppendWithLinks(formatted, footerText, Styles.Span.HelpFooterLinkSpan, Styles.Span.HelpFooterSpan);
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
                    Style = Styles.Span.HelpHeaderSpan
                });

                continue;
            }

            // Default: normal paragraph with possible links
            AppendWithLinks(formatted, trimmedLine, Styles.Span.HelpLinkSpan, Styles.Span.HelpSpan);
            AppendEmptyLine(formatted, lines, i);
        }

        return formatted;

        static void AppendEmptyLine(FormattedString formatted, string[] lines, int lineIndex)
        {
            // only append empty line if there are more lines, don't end on one
            if (lineIndex < lines.Length - 1) formatted.Spans.Add(new Span { Text = Environment.NewLine });
        }
    }

    internal static FormattedString LinkifyUrls(this string text, Style linkStyle, Style? normalStyle = null)
    {
        FormattedString formatted = new();
        AppendWithLinks(formatted, text, linkStyle, normalStyle);
        return formatted;
    }

    private static void AppendWithLinks(FormattedString target, string text, Style linkStyle, Style? normalStyle)
    {
        int lastIndex = 0;

        foreach (Match match in LinkRegex().Matches(text))
        {
            // Add normal text before this match
            if (match.Index > lastIndex) target.Spans.Add(new Span
            {
                Text = text[lastIndex..match.Index],
                Style = normalStyle
            });

            Span link;
            string url;

            Group label = match.Groups["label"],
                urlMatch = match.Groups["url"];

            if (label.Success && urlMatch.Success) // Markdown-style link
            {
                string displayText = label.Value;
                url = urlMatch.Value;
                link = new() { Text = displayText, Style = linkStyle };
            }
            else
            {
                Group urlOnly = match.Groups["urlonly"];

                if (urlOnly.Success) // Plain URL
                {
                    url = urlOnly.Value;
                    link = new() { Text = url, Style = linkStyle };
                }
                else throw new ArgumentException(nameof(LinkRegex) + " matched something unexpected " + match);
            }

            link.TapGesture(() => Launcher.OpenAsync(new Uri(url)));
            target.Spans.Add(link);
            lastIndex = match.Index + match.Length;
        }

        // Remaining normal text
        if (lastIndex < text.Length) target.Spans.Add(new Span
        {
            Text = text[lastIndex..],
            Style = normalStyle
        });
    }

    internal static Task AnimateHeightRequest(this VisualElement view, double endValue, uint duration = 300)
    {
        var tcs = new TaskCompletionSource<bool>(); // Create a task to await
        var animation = new Animation(v => view.HeightRequest = v, view.HeightRequest, endValue);

        animation.Commit(view, name: nameof(AnimateHeightRequest), length: duration, easing: Easing.CubicOut,
            finished: (_, _) => tcs.SetResult(true));

        return tcs.Task; // Await the completion of the animation
    }

    internal static VisualElement? FindTopLayout(this Element element)
    {
        if (element is Layout layout) return layout;
        if (element is ContentPage page) return FindTopLayout(page.Content);
        if (element is ContentView contentView) return FindTopLayout(contentView.Content);
        if (element is ScrollView scrollView) return FindTopLayout(scrollView.Content);
        return null;
    }
}

internal static class Converters
{
    internal static FuncConverter<bool, bool> Not = new(value => !value, value => !value);
    internal static Func<ValueTuple<bool, bool>, bool> Or = ((bool a, bool b) values) => values.a || values.b;
    internal static Func<ValueTuple<bool, bool>, bool> And = ((bool a, bool b) values) => values.a && values.b;
    internal static FuncConverter<string, bool> IsSignificant = new(value => value.IsSignificant());
    internal static FuncConverter<T, bool> Func<T>(Func<T?, bool> predicate) => new(predicate);
}

internal static class Converters<T> where T : struct
{
    internal static FuncConverter<T?, bool> HasValue = new(static value => value.HasValue);
}
