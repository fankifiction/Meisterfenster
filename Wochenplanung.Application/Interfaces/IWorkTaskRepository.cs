using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;

namespace Wochenplanung.Application.Interfaces;

public interface IWorkTaskRepository
{
    Task<List<WorkTask>> GetTasksForWeekAsync(WeekInfo week);
    Task<List<WorkTask>> GetTasksForDateRangeAsync(DateTime start, DateTime end);
    Task<List<WorkTask>> GetTasksForEmployeeDateRangeAsync(int employeeId, DateTime start, DateTime end);
    Task<List<WorkTask>> GetTasksForEmployeeAsync(int employeeId, WeekInfo week);
    Task<WorkTask?> GetByIdAsync(int id);
    Task<List<WorkTask>> SearchAsync(string query, int take = 80);
    Task<List<WorkTask>> GetByRecurrenceGroupAsync(Guid recurrenceGroupId);
    Task<WorkTask> AddAsync(WorkTask task);
    Task UpdateAsync(WorkTask task);
    Task DeleteAsync(int id);
    Task DeleteByIdsAsync(IReadOnlyList<int> ids);
}
