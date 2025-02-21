using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

internal static class Widgets
{
    internal static Button Button(string text, string command) => new Button { Text = text }.BindCommand(command);
}

internal static class ViewExtensions
{
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
