using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

internal static class Widgets
{
    internal static Button Button(string text, string command) => new Button { Text = text }.BindCommand(command);
}

internal static class ViewExtensions
{
    internal static T OnFocusChanged<T>(this T vis, Action<bool> setFocused) where T : VisualElement
    {
        vis.Focused += (_, _) => setFocused(true);
        vis.Unfocused += (_, _) => setFocused(false);
        return vis;
    }

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
