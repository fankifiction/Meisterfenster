using System.Globalization;
using System.Windows.Data;
using Wochenplanung.Presentation.Services;

namespace Wochenplanung.Presentation.Converters;

public class TimeSpanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
            return TimeText.Format(timeSpan);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string str)
            return null;
        if (!TimeText.TryParse(str, out var time))
            return System.Windows.Data.Binding.DoNothing;
        return time;
    }
}
