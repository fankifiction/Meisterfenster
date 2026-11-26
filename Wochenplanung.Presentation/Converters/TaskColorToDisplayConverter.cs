using System.Globalization;
using System.Windows.Data;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.Converters;

/// <summary>Konvertiert TaskColor für die Anzeige in der aktuellen UI-Sprache.</summary>
public class TaskColorToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TaskColor color)
            return UiText.TaskColorName(color);
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && targetType == typeof(TaskColor))
        {
            var key = str.Trim();
            if (key is "Orange" or "Arancione") return TaskColor.Orange;
            if (key is "Grün" or "Green" or "Vert" or "Verde") return TaskColor.Green;
            if (key is "Blau" or "Blue" or "Bleu" or "Blu") return TaskColor.Blue;
            if (key is "Beige") return TaskColor.Beige;
            if (key is "Gelb" or "Yellow" or "Jaune" or "Giallo") return TaskColor.Yellow;
            if (key is "Rot" or "Red" or "Rouge" or "Rosso") return TaskColor.Red;
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}
