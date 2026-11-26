using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Services;

public sealed record HolidayDay(DateTime Date, string Name);

public static class HolidayPlanner
{
    public static IReadOnlyList<HolidayDay> FromRange(DateTime start, DateTime end, string name)
    {
        var label = name.Trim();
        if (string.IsNullOrWhiteSpace(label))
            return Array.Empty<HolidayDay>();

        if (end.Date < start.Date)
            (start, end) = (end, start);

        var days = new List<HolidayDay>();
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            days.Add(new HolidayDay(d, label));
        return days;
    }

    public static IReadOnlyList<WorkTask> CreateTasks(
        IEnumerable<Employee> employees,
        IEnumerable<HolidayDay> days,
        TaskType type = TaskType.Absence,
        TaskColor color = TaskColor.Yellow)
    {
        var employeeList = employees.ToList();
        var result = new List<WorkTask>();
        foreach (var day in days)
        {
            foreach (var employee in employeeList)
            {
                result.Add(new WorkTask
                {
                    EmployeeId = employee.Id,
                    Description = day.Name,
                    StartDate = day.Date,
                    EndDate = null,
                    Type = type,
                    Color = color
                });
            }
        }

        return result;
    }

    public static IReadOnlyList<WorkTask> CreateRangeTasks(
        IEnumerable<Employee> employees,
        DateTime start,
        DateTime end,
        string name,
        TaskType type = TaskType.Vacation,
        TaskColor color = TaskColor.Beige)
    {
        var label = name.Trim();
        if (string.IsNullOrWhiteSpace(label))
            return Array.Empty<WorkTask>();

        if (end.Date < start.Date)
            (start, end) = (end, start);

        return employees.Select(employee => new WorkTask
        {
            EmployeeId = employee.Id,
            Description = label,
            StartDate = start.Date,
            EndDate = end.Date,
            Type = type,
            Color = color
        }).ToList();
    }
}
