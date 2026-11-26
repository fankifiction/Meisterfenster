using System.Globalization;
using System.Windows.Data;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.Converters;

public sealed class FavoriteGroupHeaderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isFavorite = value is true ||
                         string.Equals(value?.ToString(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        return isFavorite
            ? UiText.Tr("Favoriten", "Favorites", "Favoris", "Preferiti")
            : UiText.Tr("Weitere Standard-Aufgaben", "Other default tasks", "Autres tâches standard", "Altre attività standard");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
