using Microsoft.EntityFrameworkCore;
using Wochenplanung.Application.Exceptions;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Application.Services;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;
using Wochenplanung.Infrastructure.Data;

namespace Wochenplanung.Infrastructure.Repositories;

public class WorkTaskRepository : IWorkTaskRepository
{
    private readonly ScheduleDbContext _context;

    public WorkTaskRepository(ScheduleDbContext context)
    {
        _context = context;
    }

    public async Task<List<WorkTask>> GetTasksForWeekAsync(WeekInfo week)
    {
        var start = week.StartDate.Date;
        var end = week.EndDate.Date;
        // Kein Employee-Include: UI gruppiert über EmployeeId + Employees-Liste.
        return await QueryOverlapping(start, end)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<WorkTask>> GetTasksForDateRangeAsync(DateTime start, DateTime end)
    {
        start = start.Date;
        end = end.Date;
        return await QueryOverlapping(start, end)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<WorkTask>> GetTasksForEmployeeDateRangeAsync(int employeeId, DateTime start, DateTime end)
    {
        start = start.Date;
        end = end.Date;
        return await QueryOverlapping(start, end)
            .AsNoTracking()
            .Where(t => t.EmployeeId == employeeId)
            .ToListAsync();
    }

    public async Task<List<WorkTask>> GetTasksForEmployeeAsync(int employeeId, WeekInfo week)
    {
        var start = week.StartDate.Date;
        var end = week.EndDate.Date;
        return await QueryOverlapping(start, end)
            .Include(t => t.Employee)
            .AsNoTracking()
            .Where(t => t.EmployeeId == employeeId)
            .OrderBy(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<WorkTask?> GetByIdAsync(int id)
    {
        return await _context.Tasks
            .Include(t => t.Employee)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<WorkTask> AddAsync(WorkTask task)
    {
        ChangeStamp.Touch(task);
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        _context.Entry(task).State = EntityState.Detached;
        return task;
    }

    public async Task UpdateAsync(WorkTask task)
    {
        var existingTask = await _context.Tasks.FindAsync(task.Id);
        if (existingTask == null)
            return;

        _context.Entry(existingTask).Property(t => t.Version).OriginalValue = task.Version;
        existingTask.EmployeeId = task.EmployeeId;
        existingTask.Description = task.Description;
        existingTask.StartDate = task.StartDate;
        existingTask.EndDate = task.EndDate;
        existingTask.StartTime = task.StartTime;
        existingTask.EndTime = task.EndTime;
        existingTask.Location = task.Location;
        existingTask.Project = task.Project;
        existingTask.Notes = task.Notes;
        existingTask.Type = task.Type;
        existingTask.Color = task.Color;
        existingTask.RecurrenceGroupId = task.RecurrenceGroupId;
        existingTask.Version = task.Version + 1;
        ChangeStamp.Touch(existingTask);
        task.LastModifiedBy = existingTask.LastModifiedBy;
        task.LastModifiedAt = existingTask.LastModifiedAt;
        try
        {
            await _context.SaveChangesAsync();
            task.Version = existingTask.Version;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        _context.Entry(existingTask).State = EntityState.Detached;
    }

    public async Task DeleteAsync(int id)
    {
        var task = await _context.Tasks.FindAsync(id);
        if (task != null)
        {
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteByIdsAsync(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
            return;
        var tasks = await _context.Tasks.Where(t => ids.Contains(t.Id)).ToListAsync();
        _context.Tasks.RemoveRange(tasks);
        await _context.SaveChangesAsync();
    }

    public async Task<List<WorkTask>> SearchAsync(string query, int take = 80)
    {
        var term = query.Trim();
        if (term.Length == 0)
            return [];

        take = Math.Clamp(take, 1, 200);
        return await _context.Tasks
            .AsNoTracking()
            .Include(t => t.Employee)
            .Where(t =>
                t.Description.Contains(term) ||
                (t.Location != null && t.Location.Contains(term)) ||
                (t.Project != null && t.Project.Contains(term)) ||
                (t.Notes != null && t.Notes.Contains(term)))
            .OrderByDescending(t => t.StartDate)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<WorkTask>> GetByRecurrenceGroupAsync(Guid groupId)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.RecurrenceGroupId == groupId)
            .OrderBy(t => t.StartDate)
            .ToListAsync();
    }

    private IQueryable<WorkTask> QueryOverlapping(DateTime start, DateTime end) =>
        _context.Tasks.Where(t =>
            t.StartDate <= end &&
            (t.EndDate == null || t.EndDate >= start));
}
