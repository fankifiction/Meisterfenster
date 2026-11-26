using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Converters;

public sealed class ViewModeAndBoolToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || parameter is not string modeString)
            return Visibility.Collapsed;

        if (values[0] is not ScheduleViewMode viewMode || values[1] is not bool isEnabled || !isEnabled)
            return Visibility.Collapsed;

        return modeString switch
        {
            "Week" when viewMode == ScheduleViewMode.Week => Visibility.Visible,
            "Day" when viewMode == ScheduleViewMode.Day => Visibility.Visible,
            "Month" when viewMode == ScheduleViewMode.Month => Visibility.Visible,
            _ => Visibility.Collapsed
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => new object[] { Binding.DoNothing, Binding.DoNothing };
}
