using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Services;

public static class TaskFilter
{
    public static IReadOnlyList<WorkTask> Apply(
        IEnumerable<WorkTask> tasks,
        Employee? employee,
        TaskType? taskType,
        string? text)
    {
        IEnumerable<WorkTask> list = tasks;
        if (employee != null)
            list = list.Where(t => t.EmployeeId == employee.Id);
        if (taskType.HasValue)
            list = list.Where(t => t.Type == taskType.Value);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var query = text.Trim();
            list = list.Where(t =>
                ContainsIgnoreCase(t.Description, query) ||
                ContainsIgnoreCase(t.Location, query) ||
                ContainsIgnoreCase(t.Project, query) ||
                ContainsIgnoreCase(t.Notes, query));
        }

        return list.ToList();
    }

    private static bool ContainsIgnoreCase(string? value, string query) =>
        !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
