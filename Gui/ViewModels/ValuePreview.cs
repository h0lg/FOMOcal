using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public sealed class ValuePreview(string? result, ValuePreview.States state)
{
    public string? Result { get; } = result;
    public States State { get; } = state;

    public Style? Style { get; }
        = state == States.Success ? Styles.Editor.Success
        : state == States.Error ? Styles.Editor.Error
        : null;

    internal static ValuePreview Error(Exception ex) => new(ex.Message, States.Error);

    internal static ValuePreview Create(string? result)
        => new(result, result.IsSignificant() ? States.Success : States.Empty);

    internal static VerticalStackLayout List(string itemsSource, string hasFocus, string hasError, object source, ScrapeJobEditor? editor = null)
    {
        var observable = source as ObservableObject;

        var list = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, verticalSize: 10) }
            .IsVisible(false) // closed initially, toggled via debouncedUpdateVisibility
            .Bind(BindableLayout.ItemsSourceProperty, itemsSource)
            .ItemTemplate(() =>
            {
                Editor display = SelectableMultiLineLabel(nameof(Result));
                if (editor is not null) display.ForwardFocusTo(editor); // to avoid collapsing editor when selecting text from the display
                return display.Bind(VisualElement.StyleProperty, nameof(Style)); // bind item with correct class on construction
            });

        Debouncer debouncedUpdateVisibility = new(TimeSpan.FromMilliseconds(100), UpdateVisibilityUndebouncedAsync,
            async ex => await ErrorReport.WriteAsyncAndShare(ex.ToString(), "updating visibility of " + nameof(ValuePreview)));

        // attaching event handler to set StyleClass on Label children because that property is not bindable
        observable!.PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == hasError || e.PropertyName == hasFocus) debouncedUpdateVisibility.Run();
        };

        return list;

        // Animate show/hide when hasError or hasFocus changes
        async void UpdateVisibilityUndebouncedAsync()
        {
            Type type = observable!.GetType();

            bool shouldBeVisible = (bool)type.GetProperty(hasError)!.GetValue(source)!
                || (bool)type.GetProperty(hasFocus)!.GetValue(source)!;

            if (shouldBeVisible && !list.IsVisible)
            {
                list.IsVisible = true;

                await Task.WhenAll(list.FadeToAsync(1, 300),
                    list.ScaleToAsync(1, 300, Easing.CubicOut));
            }
            else if (!shouldBeVisible && list.IsVisible)
            {
                await Task.WhenAll(list.FadeToAsync(0, 300),
                    list.ScaleToAsync(0, 300, Easing.CubicIn));

                list.IsVisible = false;
            }
        }
    }

    public enum States { Empty, Success, Error }
}
