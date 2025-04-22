namespace FomoCal.Gui.ViewModels;

internal static class MergedStyle
{
    /// <summary>Allows merging multiple <paramref name="styles"/> into a newly added one.
    /// Inspired by https://github.com/Epsil0neR/Epsiloner-libs/blob/develop/src/Epsiloner.Wpf.Core/Extensions/MultiStyleExtension.cs</summary>
    internal static Style? Combine(params Style[] styles)
    {
        Style? mergedStyle = null;

        foreach (var style in styles)
        {
            mergedStyle ??= new Style(style.TargetType);
            Merge(mergedStyle, style);
        }

        return mergedStyle;
    }

    /// <summary>Merges the two styles passed as parameters. <paramref name="style1"/> will be modified to include any
    /// information present in <paramref name="style2"/>. If there are collisions, <paramref name="style2"/> takes priority.</summary>
    private static void Merge(Style style1, Style style2)
    {
        if (style2.BasedOn != null) Merge(style1, style2.BasedOn);
        foreach (var setter in style2.Setters) style1.Setters.Add(setter);
        foreach (TriggerBase currentTrigger in style2.Triggers) style1.Triggers.Add(currentTrigger);
    }
}
