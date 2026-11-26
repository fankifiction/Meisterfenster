using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Presentation.Converters;

public class TaskColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TaskColor color)
            return new SolidColorBrush(Resolve(color, App.IsDarkMode));
        return new SolidColorBrush(App.IsDarkMode ? Color.FromRgb(0x2A, 0x30, 0x3A) : Colors.White);
    }

    public static Color Resolve(TaskColor color, bool dark) => dark
        ? color switch
        {
            TaskColor.Orange => Color.FromRgb(210, 155, 105),
            TaskColor.Green => Color.FromRgb(120, 190, 130),
            TaskColor.Blue => Color.FromRgb(120, 165, 210),
            TaskColor.Beige => Color.FromRgb(200, 190, 160),
            TaskColor.Yellow => Color.FromRgb(220, 200, 100),
            TaskColor.Red => Color.FromRgb(210, 125, 125),
            _ => Color.FromRgb(0x2A, 0x30, 0x3A)
        }
        : color switch
        {
            TaskColor.Orange => Color.FromRgb(255, 196, 140),
            TaskColor.Green => Color.FromRgb(155, 220, 160),
            TaskColor.Blue => Color.FromRgb(155, 195, 235),
            TaskColor.Beige => Color.FromRgb(235, 228, 200),
            TaskColor.Yellow => Color.FromRgb(250, 235, 130),
            TaskColor.Red => Color.FromRgb(240, 160, 155),
            _ => Colors.White
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
