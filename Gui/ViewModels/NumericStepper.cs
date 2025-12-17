using System.Globalization;
using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls.Shapes;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

internal static class NumericStepper
{
    internal static (View Wrapper, Entry Entry) Create(string property,
        string? startLabel = null, string? endLabel = null,
        int min = 0, int max = int.MaxValue, int stepSize = 1)
    {
        var entry = new Entry { Keyboard = Keyboard.Numeric }.TextCenterHorizontal();

        entry.SetBinding(Entry.TextProperty, property, BindingMode.TwoWay,
            converter: new ClampedIntConverter(min, max), stringFormat: "{0}");

        var layout = HStack();
        if (startLabel != null) layout.AddChild(Lbl(startLabel).Margins(right: 5));

        layout.AddChild(new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(topLeft: 10, 0, bottomLeft: 10, 0) },
            Content = new Button { Text = "-", CornerRadius = 0 }.RepeatOnHold(ran => DoStep(-ran))
        });

        layout.AddChild(entry);

        layout.AddChild(new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(0, topRight: 10, 0, bottomRight: 10) },
            Content = new Button { Text = "+", CornerRadius = 0 }.RepeatOnHold(DoStep)
        });

        if (endLabel != null) layout.AddChild(Lbl(endLabel).Margins(left: 5));
        return (layout.View, entry);

        void DoStep(int ran)
        {
            Step(entry, stepSize * ran, min, max);

            if (Math.Abs(ran) == 1)
            {
                if (entry.IsFocused) entry.Unfocused += FocusOnUnfocused;
                else entry.Focus();
            }
        }

        static void FocusOnUnfocused(object? sender, FocusEventArgs e)
        {
            var entry = (Entry)sender!;
            entry.Unfocused -= FocusOnUnfocused;
            entry.Focus();
        }
    }

    private static Button RepeatOnHold(this Button button, Action<int> action)
    {
        bool isPressed = false;

        button.Pressed += async (s, e) =>
        {
            isPressed = true;
            int ran = 1;

            while (isPressed)
            {
                action(ran++); // pass and count up iteration
                await Task.Delay(200); // Repeat rate
            }
        };

        button.Released += (s, e) => isPressed = false;
        return button;
    }

    private static void Step(Entry entry, int size, int min, int max)
    {
        if (int.TryParse(entry.Text, out int value))
        {
            value = Math.Clamp(value + size, min, max);
            entry.Text = value.ToString();
        }
    }

    private class ClampedIntConverter(int min, int max) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value?.ToString() ?? "0";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            int.TryParse(value as string, out var result) ? Math.Clamp(result, min, max) : min;
    }
}
