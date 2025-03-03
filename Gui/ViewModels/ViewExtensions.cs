using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

internal static class Widgets
{
    internal static Button Btn(string text, string command, object? source = null)
        => new Button { Text = text }.BindCommand(command, source: source);

    internal static Label BndLbl(string path = ".", string? stringFormat = null)
        => new Label().Bind(Label.TextProperty, path, stringFormat: stringFormat);

    internal static CheckBox Check(string isCheckedPropertyPath)
        => new CheckBox().Margins(right: -20).Bind(CheckBox.IsCheckedProperty, isCheckedPropertyPath);
}

internal static class ViewExtensions
{
    internal static T OnFocusChanged<T>(this T vis, Action<Guid, bool> setFocused) where T : VisualElement
    {
        vis.Focused += (_, _) => setFocused(vis.Id, true);
        vis.Unfocused += (_, _) => setFocused(vis.Id, false);
        return vis;
    }

    internal static T BindVisible<T>(this T vis, string property) where T : VisualElement
        => vis.Bind(VisualElement.IsVisibleProperty, property);

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
}
