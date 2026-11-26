using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wochenplanung.Presentation.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
            return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}
