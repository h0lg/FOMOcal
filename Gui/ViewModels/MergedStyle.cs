namespace FomoCal.Gui.ViewModels;

/// <summary>Extension that allows to merge multiple styles into one.
/// See https://learn.microsoft.com/en-us/dotnet/maui/xaml/markup-extensions/create?view=net-maui-9.0.
/// Inspired by https://github.com/Epsil0neR/Epsiloner-libs/blob/develop/src/Epsiloner.Wpf.Core/Extensions/MultiStyleExtension.cs</summary>
[ContentProperty(nameof(Styles))]
[RequireService([typeof(IProvideValueTarget)])]
public class MergedStyleExtension : IMarkupExtension<Style?>
{
    public IList<Style> Styles { get; set; } = new List<Style>();

    public MergedStyleExtension() { }

    public MergedStyleExtension(params Style[] styles)
    {
        Styles = styles?.ToList() ?? new List<Style>();
    }

    public Style? ProvideValue(IServiceProvider serviceProvider)
    {
        if (Styles == null || Styles.Count == 0) return null;
        var service = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        if (service?.TargetObject is not Style) return null;

        Style? mergedStyle = null;

        foreach (var style in Styles)
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

    object? IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
