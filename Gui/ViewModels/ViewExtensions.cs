namespace FomoCal.Gui.ViewModels;

internal static class ViewExtensions
{
    internal static Label Wrap(this Label label)
    {
        label.LineBreakMode = LineBreakMode.WordWrap;
        return label;
    }
}
