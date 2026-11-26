using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Converters;

public class ViewModeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScheduleViewMode mode && parameter is string paramStr)
        {
            if (paramStr == "Day" && mode == ScheduleViewMode.Day) return Visibility.Visible;
            if (paramStr == "Month" && mode == ScheduleViewMode.Month) return Visibility.Visible;
            if (paramStr == "Week" && mode == ScheduleViewMode.Week) return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
