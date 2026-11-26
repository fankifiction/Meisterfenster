using System.Globalization;
using System.Windows.Data;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.Converters;

/// <summary>Konvertiert TaskType für die Anzeige.</summary>
public class TaskTypeToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return UiText.FilterAll;
        if (value is TaskType type)
            return UiText.TaskTypeName(type);
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && targetType == typeof(TaskType) && UiText.TryParseTaskType(str, out var type))
            return type;
        return System.Windows.Data.Binding.DoNothing;
    }
}
