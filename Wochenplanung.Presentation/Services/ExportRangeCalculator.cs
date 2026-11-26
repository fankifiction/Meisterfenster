using Wochenplanung.Application.Services;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Services;

public static class ExportRangeCalculator
{
    public static (DateTime Start, DateTime End) GetDefaultRange(
        ScheduleViewMode mode,
        DateTime selectedDate,
        DateTime monthStart,
        DateTime weekStart,
        DateTime weekEnd) =>
        mode switch
        {
            ScheduleViewMode.Day => (selectedDate.Date, selectedDate.Date),
            ScheduleViewMode.Month => (monthStart.Date, monthStart.AddMonths(1).AddDays(-1).Date),
            _ => (weekStart.Date, weekEnd.Date)
        };
}

public static class TaskAuditText
{
    public static string FormatTooltip(WorkTask task)
    {
        var stamp = ChangeStamp.Format(task.LastModifiedBy, task.LastModifiedAt);
        if (string.IsNullOrWhiteSpace(stamp))
            return task.Description;
        var label = UiText.Tr("Zuletzt geändert", "Last changed", "Dernière modification", "Ultima modifica");
        return $"{task.Description}\n{label}: {stamp}";
    }

    public static string FormatDialogLine(string? actor, DateTime? utcTimestamp)
    {
        var stamp = ChangeStamp.Format(actor, utcTimestamp);
        if (string.IsNullOrWhiteSpace(stamp))
            return string.Empty;
        var label = UiText.Tr("Zuletzt geändert", "Last changed", "Dernière modification", "Ultima modifica");
        return $"{label}: {stamp}";
    }
}
