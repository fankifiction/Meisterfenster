using System.Globalization;
using System.Windows.Data;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Converters;

public class RecurrencePatternToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RecurrencePattern pattern)
            return UiText.RecurrencePatternName(pattern);
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
