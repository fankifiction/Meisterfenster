using System.Globalization;
using System.Windows.Data;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Converters;

public class SeriesEditScopeToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SeriesEditScope scope)
            return UiText.SeriesEditScopeName(scope);
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
