using System.Windows;

namespace Wochenplanung.Presentation.Services;

public static class ScheduleColumnLayout
{
    public const double MinDayColumnWidth = 88;

    public static bool UseFixedDayWidths(int dayCount) => dayCount >= 14;

    public static GridLength DayColumnWidth(double storedWidth, int dayCount)
    {
        var width = Math.Max(MinDayColumnWidth, storedWidth);
        if (UseFixedDayWidths(dayCount))
            return new GridLength(width);
        return new GridLength(Math.Max(1, storedWidth), GridUnitType.Star);
    }

    public static double DayColumnMinWidth(int dayCount) =>
        UseFixedDayWidths(dayCount) ? MinDayColumnWidth : 32;
}
