namespace Wochenplanung.Presentation.Services;

public static class ScheduleGridLayout
{
    public const double DefaultEmployeeColumnWidth = 180;
    public const double DefaultDayColumnWidth = 150;
    public const double TaskSlotMinHeight = 40;
    public const double EmployeeRowGap = 6;
    public const double GripColumnWidth = 28;
    public const double SplitterColumnWidth = 8;

    public static int DayContentColumn(int dayIndex) => 3 + dayIndex * 2;
    public static int TrailingSplitterColumn(int dayCount) => 2 + dayCount * 2;
    public static int TrailingFillerColumn(int dayCount) => TrailingSplitterColumn(dayCount) + 1;
    public static int TotalColumnCount(int dayCount) => TrailingFillerColumn(dayCount) + 1;
    public static bool IsSplitterColumn(int index) => index >= 2 && index % 2 == 0;
    public static bool IsFillerColumn(int index, int dayCount) => index == TrailingFillerColumn(dayCount);
    public static bool IsDayContentColumn(int index, int dayCount) =>
        index >= 3 && index < TrailingSplitterColumn(dayCount) && !IsSplitterColumn(index);

    public static double GetDefaultColumnWidth(int index, int dayCount = 7) => index switch
    {
        0 => GripColumnWidth,
        1 => DefaultEmployeeColumnWidth,
        _ when IsSplitterColumn(index) => SplitterColumnWidth,
        _ when IsFillerColumn(index, dayCount) => 0,
        _ => DefaultDayColumnWidth
    };
}
