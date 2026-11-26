using System.Globalization;
using System.Windows.Data;
using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Presentation.Converters;

/// <summary>Gibt "(Alle)" für null zurück, sonst FullName des Mitarbeiters.</summary>
public class NullToEmployeeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return "(Alle)";
        if (value is Employee emp) return emp.FullName;
        return value.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
