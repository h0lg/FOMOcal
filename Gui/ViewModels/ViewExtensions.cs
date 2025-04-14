using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace FomoCal.Gui.ViewModels;

internal static class Widgets
{
    internal static Button Btn(string text, string? command = null, object? source = null, string parameterPath = ".")
    {
        Button btn = new() { Text = text };
        if (command != null) btn.BindCommand(command, source: source, parameterPath: parameterPath);
        return btn;
    }

    internal static Label Lbl(string text) => new() { Text = text };

    internal static HorizontalStackLayout LbldView(string label, View view, string? tooltip = null)
    {
        HorizontalStackLayout wrapper = HStack(5, Lbl(label), view);
        if (tooltip.IsSignificant()) wrapper.ToolTip(tooltip);
        return wrapper;
    }

    internal static Label BndLbl(string path = ".", string? stringFormat = null, object? source = null)
        => new Label().Bind(Label.TextProperty, path, stringFormat: stringFormat, source: source);

    internal static Entry Entr(string path, string? placeholder = null)
        => new Entry { Placeholder = placeholder }.Bind(Entry.TextProperty, path);

    internal static CheckBox Check(string isCheckedPropertyPath, object? source = null)
        => new CheckBox().Margins(right: -20).Bind(CheckBox.IsCheckedProperty, isCheckedPropertyPath, source: source);

    internal static (Switch Switch, Grid Wrapper) Swtch(string isToggledPropertyPath)
    {
        Switch swtch = new Switch().Bind(Switch.IsToggledProperty, isToggledPropertyPath);
        return (Switch: swtch, Wrapper: SwtchWrp(swtch));
    }

    internal static Grid SwtchWrp(Switch swtch) => Grd(cols: [42], rows: [Auto], children: swtch);

    internal static Grid Grd(GridLength[] cols, GridLength[] rows, double spacing = 0, params IView[] children)
    {
        Grid grid = new()
        {
            RowSpacing = spacing,
            ColumnSpacing = spacing,
            ColumnDefinitions = Columns.Define(cols),
            RowDefinitions = Rows.Define(rows)
        };

        foreach (var child in children)
            grid.Children.Add(child);

        return grid;
    }

    internal static VerticalStackLayout VStack(double? spacing = null, params IView[] children)
    {
        VerticalStackLayout layout = new();
        if (spacing.HasValue) layout.Spacing = spacing.Value;

        foreach (var child in children)
            layout.Children.Add(child);

        return layout;
    }

    internal static HorizontalStackLayout HStack(double? spacing = null, params IView[] children)
    {
        HorizontalStackLayout layout = new();
        if (spacing.HasValue) layout.Spacing = spacing.Value;

        foreach (View child in children)
            layout.Children.Add(child.CenterVertical());

        return layout;
    }

    internal static (FlexLayout View, Action<View> AddChild) HWrap(params View[] children) => HWrap(null, children);

    internal static (FlexLayout View, Action<View> AddChild) HWrap(Thickness? childMargin = null, params View[] children)
    {
        FlexLayout layout = new()
        {
            Wrap = FlexWrap.Wrap,
            //JustifyContent = FlexJustify.SpaceAround,
            AlignItems = FlexAlignItems.Center
        };

        foreach (var child in children) AddChild(child);
        return (layout, AddChild);

        void AddChild(View child)
        {
            layout.Children.Add(child);

            if (childMargin.HasValue && child.Margin == default)
                child.Margin = childMargin.Value;
        }
    }
}

internal static class Styles
{
    internal static class Label
    {
        internal static string Headline = GetName(), SubHeadline = GetName(),
            Clickable = GetName(), Error = GetName(), Success = GetName(),
            Demoted = GetName(), VenueRowDetail = GetName();
    }

    internal static class Span
    {
        internal static Style HelpHeaderSpan = Get(), HelpSpan = Get(),
            HelpLinkSpan = Get(), HelpFooterSpan = Get(), HelpFooterLinkSpan = Get();
    }

    private static string GetName([CallerMemberName] string key = "") => key;
    private static Style Get([CallerMemberName] string key = "") => (Style)Application.Current!.Resources[key];
}

internal static class ViewExtensions
{
    internal static T OnFocusChanged<T>(this T vis, Action<VisualElement, bool> setFocused) where T : VisualElement
    {
        vis.Focused += (_, _) => setFocused(vis, true);
        vis.Unfocused += (_, _) => setFocused(vis, false);
        return vis;
    }

    internal static T InlineTooltipOnFocus<T>(this T vis, string tooltip, Label label,
        Action<VisualElement, bool>? onFocusChanged = null,
        Func<VisualElement, bool, bool>? cancelFocusChanged = null) where T : VisualElement
        => vis.ToolTip(tooltip).OnFocusChanged((vis, focused) =>
        {
            if (cancelFocusChanged?.Invoke(vis, focused) == true) return;
            label.FormattedText = focused ? tooltip.ParseMarkdown() : null;
            vis.ToolTip(focused ? null : tooltip); // prevent tooltip from overlaying help on focus
            onFocusChanged?.Invoke(vis, focused);
        });

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

    internal static T OnTextChanged<T>(this T input, Action<TextChangedEventArgs> handle) where T : InputView
    {
        input.TextChanged += (object? sender, TextChangedEventArgs e) => handle(e);
        return input;
    }

    internal static Label Wrap(this Label label)
    {
        label.LineBreakMode = LineBreakMode.WordWrap;
        return label;
    }

    private static readonly Regex linkRegex = new(@"\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)", RegexOptions.Compiled),
        headerRegex = new(@"^(#+)\s+(.*)", RegexOptions.Compiled); // e.g. # Heading

    private const string footerPrefix = "^^";

    internal static FormattedString ParseMarkdown(this string text)
    {
        FormattedString formatted = new();

        foreach (var line in text.Split('\n'))
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.IsNullOrWhiteSpace())
            {
                formatted.Spans.Add(new Span { Text = Environment.NewLine });
                continue;
            }

            // Detect footer
            if (trimmedLine.StartsWith(footerPrefix))
            {
                var footerText = trimmedLine[footerPrefix.Length..].TrimStart();
                AppendWithLinks(formatted, footerText, Styles.Span.HelpFooterLinkSpan, Styles.Span.HelpFooterSpan);
                formatted.Spans.Add(new Span { Text = Environment.NewLine });
                continue;
            }

            // Detect headers
            var headerMatch = headerRegex.Match(trimmedLine);

            if (headerMatch.Success)
            {
                int level = headerMatch.Groups[1].Value.Length;
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
            formatted.Spans.Add(new Span { Text = Environment.NewLine });
        }

        return formatted;

        static void AppendWithLinks(FormattedString target, string text, Style linkStyle, Style normalStyle)
        {
            int lastIndex = 0;

            foreach (Match match in linkRegex.Matches(text))
            {
                // Text before link
                if (match.Index > lastIndex) target.Spans.Add(new Span
                {
                    Text = text[lastIndex..match.Index],
                    Style = normalStyle
                });

                string displayText = match.Groups[1].Value;
                string url = match.Groups[2].Value;
                Span link = new() { Text = displayText, Style = linkStyle };
                link.TapGesture(() => Launcher.OpenAsync(new Uri(url)));
                target.Spans.Add(link);
                lastIndex = match.Index + match.Length;
            }

            // Remainder
            if (lastIndex < text.Length) target.Spans.Add(new Span
            {
                Text = text[lastIndex..],
                Style = normalStyle
            });
        }
    }

    internal static Task AnimateHeightRequest(this VisualElement view, double endValue, uint duration = 300)
    {
        var tcs = new TaskCompletionSource<bool>(); // Create a task to await
        var animation = new Animation(v => view.HeightRequest = v, view.HeightRequest, endValue);

        animation.Commit(view, name: nameof(AnimateHeightRequest), length: duration, easing: Easing.CubicOut,
            finished: (v, c) => tcs.SetResult(true));

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
