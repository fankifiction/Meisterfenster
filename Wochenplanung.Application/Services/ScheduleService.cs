using Microsoft.Extensions.Logging;
using Wochenplanung.Application.Exceptions;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;

namespace Wochenplanung.Application.Services;

public class ScheduleService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        IEmployeeRepository employeeRepository,
        IWorkTaskRepository workTaskRepository,
        ILogger<ScheduleService> logger)
    {
        _employeeRepository = employeeRepository;
        _workTaskRepository = workTaskRepository;
        _logger = logger;
    }

    public async Task<List<Employee>> GetAllEmployeesAsync()
    {
        return await _employeeRepository.GetAllAsync();
    }

    public async Task<List<WorkTask>> GetTasksForWeekAsync(WeekInfo week)
    {
        return await _workTaskRepository.GetTasksForWeekAsync(week);
    }

    public async Task<List<WorkTask>> GetTasksForDateRangeAsync(DateTime start, DateTime end)
    {
        return await _workTaskRepository.GetTasksForDateRangeAsync(start, end);
    }

    public async Task<WorkTask> CreateTaskAsync(WorkTask task)
    {
        await EnsureNoDuplicateAsync(task);
        return await _workTaskRepository.AddAsync(task);
    }

    public async Task UpdateTaskAsync(WorkTask task)
    {
        await EnsureNoDuplicateAsync(task, task.Id);
        await _workTaskRepository.UpdateAsync(task);
    }

    private async Task EnsureNoDuplicateAsync(WorkTask task, int? excludeTaskId = null)
    {
        var start = task.StartDate.Date;
        var end = (task.EndDate ?? task.StartDate).Date;
        // Nur Aufgaben desselben Mitarbeiters laden – ohne Employee-Include.
        var existingTasks = await _workTaskRepository.GetTasksForEmployeeDateRangeAsync(task.EmployeeId, start, end);

        foreach (var day in TaskScheduleRules.EnumerateCoveredDays(task))
        {
            var duplicate = TaskScheduleRules.FindDuplicateOnDay(existingTasks, task, day, excludeTaskId);
            if (duplicate != null)
                throw new DuplicateTaskException(TaskScheduleRules.NormalizeDescription(duplicate.Description), day);
        }
    }

    public async Task DeleteTaskAsync(int taskId)
    {
        await _workTaskRepository.DeleteAsync(taskId);
    }

    public Task<List<WorkTask>> SearchTasksAsync(string query, int take = 80) =>
        _workTaskRepository.SearchAsync(query, take);

    public Task<List<WorkTask>> GetSeriesAsync(Guid groupId) =>
        _workTaskRepository.GetByRecurrenceGroupAsync(groupId);

    public async Task UpdateSeriesAsync(WorkTask template, DateTime? fromDateInclusive)
    {
        if (!template.RecurrenceGroupId.HasValue)
        {
            await UpdateTaskAsync(template);
            return;
        }

        var series = await _workTaskRepository.GetByRecurrenceGroupAsync(template.RecurrenceGroupId.Value);
        foreach (var item in series)
        {
            if (fromDateInclusive.HasValue && item.StartDate.Date < fromDateInclusive.Value.Date)
                continue;

            item.Description = template.Description;
            item.StartTime = template.StartTime;
            item.EndTime = template.EndTime;
            item.Location = template.Location;
            item.Project = template.Project;
            item.Notes = template.Notes;
            item.Type = template.Type;
            item.Color = template.Color;
            item.EmployeeId = template.EmployeeId;
            if (item.Id == template.Id)
            {
                item.StartDate = template.StartDate;
                item.EndDate = template.EndDate;
            }

            await UpdateTaskAsync(item);
        }
    }

    public async Task DeleteSeriesAsync(Guid groupId, DateTime? fromDateInclusive)
    {
        var series = await _workTaskRepository.GetByRecurrenceGroupAsync(groupId);
        var ids = series
            .Where(t => !fromDateInclusive.HasValue || t.StartDate.Date >= fromDateInclusive.Value.Date)
            .Select(t => t.Id)
            .ToList();
        await _workTaskRepository.DeleteByIdsAsync(ids);
    }

    public async Task<Employee> CreateEmployeeAsync(Employee employee)
    {
        return await _employeeRepository.AddAsync(employee);
    }

    public async Task UpdateEmployeeAsync(Employee employee)
    {
        await _employeeRepository.UpdateAsync(employee);
    }

    public async Task ReorderEmployeesAsync(IReadOnlyList<int> employeeIdsInOrder)
    {
        await _employeeRepository.UpdateOrderAsync(employeeIdsInOrder);
    }

    public async Task DeleteEmployeeAsync(int employeeId)
    {
        await _employeeRepository.DeleteAsync(employeeId);
    }

    public Task NormalizeEmployeeOrderByNumberAsync() =>
        _employeeRepository.NormalizeOrderByEmployeeNumberAsync();

    public Task NormalizeEmployeeOrderByNameAsync() =>
        _employeeRepository.NormalizeOrderByNameAsync();
}
