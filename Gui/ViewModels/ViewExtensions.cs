using CommunityToolkit.Maui.Markup;
using static Microsoft.Maui.Controls.VisualStateManager;

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

    // inspired by https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/collectionview/selection?view=net-maui-9.0#change-selected-item-color
    internal static T StyleSelected<T>(this T vis, Type targetType, BindableProperty property, object value) where T : VisualElement
    {
        Setter setter = new() { Property = property, Value = value };
        VisualState stateSelected = new() { Name = CommonStates.Selected, Setters = { setter } };
        VisualState stateNormal = new() { Name = CommonStates.Normal };
        VisualStateGroup visualStateGroup = new() { Name = nameof(CommonStates), States = { stateSelected, stateNormal } };
        VisualStateGroupList visualStateGroupList = [visualStateGroup];
        Setter vsgSetter = new() { Property = VisualStateGroupsProperty, Value = visualStateGroupList };
        Style style = new(targetType) { Setters = { vsgSetter } };
        vis.Resources.Add(style);
        return vis;
    }
}
