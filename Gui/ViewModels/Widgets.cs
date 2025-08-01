using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace FomoCal.Gui.ViewModels;

internal static class Widgets
{
    internal static Button Btn(string text, string? command = null, object? source = null,
        string parameterPath = ".", object? parameterSource = null)
    {
        Button btn = new() { Text = text };

        if (command != null) btn.BindCommand(command, source: source,
            parameterPath: parameterPath, parameterSource: parameterSource);

        return btn;
    }

    internal static Label Lbl(string text) => new() { Text = text };

    internal static (Label label, Border layout) HelpLabel()
    {
        Label label = new();
        Border layout = new() { StyleClass = ["help"], Content = label };
        return (label, layout);
    }

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

    internal static Editor SelectableMultiLineLabel(string textPropertyPath = ".")
        => new Editor { IsReadOnly = true, AutoSize = EditorAutoSizeOption.TextChanges }
            .Bind(Editor.TextProperty, textPropertyPath, BindingMode.OneWay);

    internal static CheckBox Check(string isCheckedPropertyPath, object? source = null)
        => new CheckBox().Margins(right: -20).Bind(CheckBox.IsCheckedProperty, isCheckedPropertyPath, source: source);

    internal static (Switch Switch, Grid Wrapper) Swtch(string isToggledPropertyPath, BindingMode mode = BindingMode.Default)
    {
        Switch swtch = new Switch().Bind(Switch.IsToggledProperty, isToggledPropertyPath, mode);
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
        VerticalStackLayout layout = [];
        if (spacing.HasValue) layout.Spacing = spacing.Value;

        foreach (var child in children)
            layout.Children.Add(child);

        return layout;
    }

    internal static HorizontalStackLayout HStack(double? spacing = null, params IView[] children)
    {
        HorizontalStackLayout layout = [];
        if (spacing.HasValue) layout.Spacing = spacing.Value;

        foreach (var child in children.Cast<View>())
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
