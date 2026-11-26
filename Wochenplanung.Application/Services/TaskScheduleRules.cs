using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Services;

public static class TaskScheduleRules
{
    public static string NormalizeDescription(string description) => description.Trim();

    public static bool HasSameDescription(WorkTask left, WorkTask right) =>
        string.Equals(NormalizeDescription(left.Description), NormalizeDescription(right.Description), StringComparison.OrdinalIgnoreCase);

    public static bool CoversDay(WorkTask task, DateTime day)
    {
        var start = task.StartDate.Date;
        var end = (task.EndDate ?? task.StartDate).Date;
        day = day.Date;
        return day >= start && day <= end;
    }

    public static IEnumerable<DateTime> EnumerateCoveredDays(WorkTask task)
    {
        var start = task.StartDate.Date;
        var end = (task.EndDate ?? task.StartDate).Date;
        for (var day = start; day <= end; day = day.AddDays(1))
            yield return day;
    }

    public static WorkTask? FindDuplicateOnDay(
        IEnumerable<WorkTask> existingTasks,
        WorkTask candidate,
        DateTime day,
        int? excludeTaskId = null)
    {
        return existingTasks.FirstOrDefault(task =>
            task.EmployeeId == candidate.EmployeeId &&
            task.Id != excludeTaskId &&
            HasSameDescription(task, candidate) &&
            CoversDay(task, day));
    }
}
