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
        Border layout = new() { StyleClass = ["help"], Content = label, IsVisible = false };
        return (label, layout);
    }

    internal static Grid LbldView(string label, params View[] views) => LbldView(Lbl(label), views);

    internal static Grid LbldView(Label label, params View[] views)
    {
        label.TextCenterVertical().Margins(right: 5);
        GridLength[] cols = [Auto, Star, .. Enumerable.Repeat(Auto, views.Length - 1)];
        var grid = Grd(cols: cols, rows: [Auto], spacing: 0, label);
        var column = 1;

        foreach (View view in views)
            grid.Add(view, column: column++);

        return grid;
    }

    internal static Border Expndr(Label header, params View[] toggledViews)
    {
        /* used to animate the expanding/collapsing of toggledViews
         * while allowing to determine how much Height they'd need on screen when expanding them
         * and allowing access to the toggledViews if the container is resized */
        var scroller = new ScrollView
        {
            Content = VStack(5, toggledViews),
            HeightRequest = 0 // hide toggledViews initially
        };

        bool isOpen = false;

        // adjust open expander to the size of its contents
        scroller.Content.SizeChanged += async (o, e) =>
        {
            if (isOpen) await scroller.AnimateHeightRequest(scroller.Content.DesiredSize.Height);
        };

        // toggle visibility of toggledViews on header tap
        header.TapGesture(async () =>
        {
            isOpen = !isOpen; // toggle state before animation to prevent size adjustment from interfering with closing animation
            await scroller.AnimateHeightRequest(isOpen ? scroller.Content.Height : 0); // isOpen was already toggled
        }).FillHorizontal(); // so tapping anywhere in the header row works

        return new()
        {
            StyleClass = [Styles.Border.RoundedSection],
            Content = VStack(5, header, scroller)
        };
    }

    internal static Label BndLbl(string path = ".", string? stringFormat = null, object? source = null, IValueConverter? converter = null)
        => new Label().Bind(Label.TextProperty, path, converter: converter, stringFormat: stringFormat, source: source);

    internal static Label BndFmtLbl(string path = ".", IValueConverter? converter = null)
        => new Label().Bind(Label.FormattedTextProperty, path, converter: converter);

    internal static Entry Entr(string path, string? placeholder = null, Keyboard? keybord = null)
        => new Entry { Placeholder = placeholder, Keyboard = keybord }.Bind(Entry.TextProperty, path);

    internal static Editor Edtr(string path, string? placeholder = null, Keyboard? keybord = null)
        => new Editor { Placeholder = placeholder, AutoSize = EditorAutoSizeOption.TextChanges, Keyboard = keybord }.Bind(Editor.TextProperty, path);

    internal static Editor SelectableMultiLineLabel(string textPropertyPath = ".")
        => new Editor { IsReadOnly = true, AutoSize = EditorAutoSizeOption.TextChanges }
            .Bind(Editor.TextProperty, textPropertyPath, BindingMode.OneWay);

    private static CheckBox Check(string isCheckedPropertyPath, object? source = null)
        => new CheckBox().Bind(CheckBox.IsCheckedProperty, isCheckedPropertyPath, source: source);

    internal static (Grid Wrapper, Label Label, CheckBox CheckBox) LbldChck(string label, string isCheckedPropertyPath, object? source = null)
    {
        Label lbl = Lbl(label);
        (Grid wrapper, CheckBox checkBox) = LbldChck(lbl, isCheckedPropertyPath, source);
        return (wrapper, lbl, checkBox);
    }

    internal static (Grid Wrapper, CheckBox CheckBox) LbldChck(Label label, string isCheckedPropertyPath, object? source = null)
    {
        CheckBox checkBox = Check(isCheckedPropertyPath, source);

        Grid wrapper = Grd(cols: [Auto, Auto], rows: [Auto], spacing: 0, checkBox,
            label.TextCenterVertical().Column(1));

        // "forward" tap on label to checkbox, which is hard to hit on a touch screen
        label.TapGesture(() =>
        {
            checkBox.IsChecked = !checkBox.IsChecked; // toggle
            checkBox.Focus(); // to inline tooltip
        });

        return (wrapper, checkBox);
    }

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
        => HStackable(spacing, children).View;

    internal static (HorizontalStackLayout View, Action<View> AddChild) HStackable(double? spacing = null, params IView[] children)
    {
        HorizontalStackLayout layout = [];
        if (spacing.HasValue) layout.Spacing = spacing.Value;

        foreach (var child in children.Cast<View>())
            AddChild(child);

        return (layout, AddChild);

        void AddChild(View child) => layout.Children.Add(child.CenterVertical());
    }

    internal static (FlexLayout View, Action<View> AddChild) Flx(Thickness? childMargin = null, params View[] children)
    {
        FlexLayout layout = new() { AlignItems = FlexAlignItems.Center };

        foreach (var child in children) AddChild(child);
        return (layout, AddChild);

        void AddChild(View child)
        {
            layout.Children.Add(child);

            if (childMargin.HasValue && child.Margin == default)
                child.Margin = childMargin.Value;
        }
    }

    internal static (FlexLayout View, Action<View> AddChild) HWrap(params View[] children) => HWrap(null, children);

    internal static (FlexLayout View, Action<View> AddChild) HWrap(Thickness? childMargin = null, params View[] children)
    {
        var (view, addChild) = Flx(childMargin, children);
        view.Wrap = FlexWrap.Wrap;
        return (view, addChild);
    }

    internal static Label MenuTrigger(Action onTap)
        => Lbl("︙").StyleClass(Styles.Label.Headline).CenterVertical().Paddings(left: 5, right: 5).Bold().TapGesture(onTap);
}
