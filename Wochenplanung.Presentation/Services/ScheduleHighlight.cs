using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Services;

public static class ScheduleHighlight
{
    public static bool IsCellHighlighted(
        ScheduleHighlightMode mode,
        int? cellEmployeeId,
        DateTime? cellDay,
        DateTime? columnDay,
        int? rowEmployeeId,
        int employeeId,
        DateTime day) =>
        mode switch
        {
            ScheduleHighlightMode.Cell => cellEmployeeId == employeeId && cellDay == day.Date,
            ScheduleHighlightMode.Column => columnDay == day.Date,
            ScheduleHighlightMode.Row => rowEmployeeId == employeeId,
            _ => false
        };

    public static bool IsColumnHeaderHighlighted(ScheduleHighlightMode mode, DateTime? columnDay, DateTime day) =>
        mode == ScheduleHighlightMode.Column && columnDay == day.Date;

    public static bool IsEmployeeNameHighlighted(ScheduleHighlightMode mode, int? rowEmployeeId, int employeeId) =>
        mode == ScheduleHighlightMode.Row && rowEmployeeId == employeeId;
}
