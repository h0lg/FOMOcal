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

    internal static Label BndLbl(string path = ".", string? stringFormat = null)
        => new Label().Bind(Label.TextProperty, path, stringFormat: stringFormat);

    internal static Entry Entr(string path, string? placeholder = null)
        => new Entry { Placeholder = placeholder }.Bind(Entry.TextProperty, path);

    internal static CheckBox Check(string isCheckedPropertyPath)
        => new CheckBox().Margins(right: -20).Bind(CheckBox.IsCheckedProperty, isCheckedPropertyPath);

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

        foreach (var child in children)
            layout.Children.Add(child);

        return layout;
    }

    internal static FlexLayout HWrap(params View[] children) => HWrap(null, children);

    internal static FlexLayout HWrap(Thickness? childMargin = null, params View[] children)
    {
        FlexLayout layout = new()
        {
            Wrap = FlexWrap.Wrap,
            //JustifyContent = FlexJustify.SpaceAround,
            AlignItems = FlexAlignItems.Center
        };

        foreach (var child in children)
        {
            layout.Children.Add(child);

            if (childMargin.HasValue && child.Margin == default)
                child.Margin = childMargin.Value;
        }

        return layout;
    }
}

internal static class ViewExtensions
{
    internal static T OnFocusChanged<T>(this T vis, Action<VisualElement, bool> setFocused) where T : VisualElement
    {
        vis.Focused += (_, _) => setFocused(vis, true);
        vis.Unfocused += (_, _) => setFocused(vis, false);
        return vis;
    }

    internal static T ToolTip<T>(this T bindable, string text) where T : BindableObject
    {
        ToolTipProperties.SetText(bindable, text);
        return bindable;
    }

    internal static T BindVisible<T>(this T vis, string property, object? source = null) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, property, source: source);

    internal static T BindIsVisibleToValueOf<T>(this T vis, string textProperty) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, textProperty, convert: static (string? value) => value.IsSignificant());

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

    internal static VisualElement? FindTopLayout(this Element element)
    {
        if (element is Layout layout) return layout;
        if (element is ContentPage page) return FindTopLayout(page.Content);
        if (element is ContentView contentView) return FindTopLayout(contentView.Content);
        if (element is ScrollView scrollView) return FindTopLayout(scrollView.Content);
        return null;
    }
}
