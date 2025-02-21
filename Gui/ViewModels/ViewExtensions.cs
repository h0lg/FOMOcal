using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

internal static class Widgets
{
    internal static Label BndLbl(string path = ".", string? stringFormat = null)
        => new Label().Bind(Label.TextProperty, path, stringFormat: stringFormat);
}

internal static class ViewExtensions
{
    internal static Label Wrap(this Label label)
    {
        label.LineBreakMode = LineBreakMode.WordWrap;
        return label;
    }
}
