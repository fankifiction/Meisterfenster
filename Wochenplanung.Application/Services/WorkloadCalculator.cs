using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Services;

public static class WorkloadCalculator
{
    public static (int Days, double Hours) ForTasks(IEnumerable<WorkTask> tasks, DateTime start, DateTime end)
    {
        start = start.Date;
        end = end.Date;
        if (end < start)
            (start, end) = (end, start);

        var days = new HashSet<DateTime>();
        double hours = 0;
        foreach (var t in tasks)
        {
            if (t.Type is TaskType.Vacation or TaskType.Absence or TaskType.School)
                continue;

            var taskDays = 0;
            for (var d = t.StartDate.Date; d <= (t.EndDate ?? t.StartDate).Date; d = d.AddDays(1))
            {
                if (d < start || d > end)
                    continue;
                days.Add(d);
                taskDays++;
            }

            if (t.StartTime.HasValue && t.EndTime.HasValue)
                hours += (t.EndTime.Value - t.StartTime.Value).TotalHours * Math.Max(1, taskDays);
            else
                hours += Math.Max(0, taskDays) * 8;
        }

        return (days.Count, hours);
    }
}
