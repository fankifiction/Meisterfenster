using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Wochenplanung.Application.Exceptions;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Application.Services;
using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Tests;

public sealed class ScheduleServiceTests
{
    [Fact]
    public async Task CreateTask_DelegatesToRepository()
    {
        var employeeRepo = new FakeEmployeeRepository();
        var taskRepo = new FakeWorkTaskRepository();
        var service = new ScheduleService(employeeRepo, taskRepo, NullLogger<ScheduleService>.Instance);

        var created = await service.CreateTaskAsync(new WorkTask
        {
            EmployeeId = 10,
            Description = "Test",
            StartDate = DateTime.Today
        });

        Assert.Equal("Test", created.Description);
        Assert.Single(taskRepo.Tasks);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task CreateTask_ThrowsWhenDuplicateExistsOnSameDay()
    {
        var employeeRepo = new FakeEmployeeRepository();
        var taskRepo = new FakeWorkTaskRepository();
        var service = new ScheduleService(employeeRepo, taskRepo, NullLogger<ScheduleService>.Instance);
        var day = new DateTime(2026, 7, 10);

        await service.CreateTaskAsync(new WorkTask
        {
            EmployeeId = 10,
            Description = "Montage",
            StartDate = day
        });

        var duplicate = await Assert.ThrowsAsync<DuplicateTaskException>(() => service.CreateTaskAsync(new WorkTask
        {
            EmployeeId = 10,
            Description = " montage ",
            StartDate = day
        }));

        Assert.Equal("Montage", duplicate.TaskDescription);
        Assert.Equal(day, duplicate.Day);
    }

    [Fact]
    public async Task UpdateTask_ThrowsWhenResizeCreatesDuplicate()
    {
        var employeeRepo = new FakeEmployeeRepository();
        var taskRepo = new FakeWorkTaskRepository();
        var service = new ScheduleService(employeeRepo, taskRepo, NullLogger<ScheduleService>.Instance);
        var thursday = new DateTime(2026, 7, 9);
        var friday = new DateTime(2026, 7, 10);

        var first = await service.CreateTaskAsync(new WorkTask
        {
            EmployeeId = 10,
            Description = "Montage",
            StartDate = thursday
        });

        await service.CreateTaskAsync(new WorkTask
        {
            EmployeeId = 10,
            Description = "Montage",
            StartDate = friday
        });

        first.EndDate = friday;

        await Assert.ThrowsAsync<DuplicateTaskException>(() => service.UpdateTaskAsync(first));
    }

    [Fact]
    public async Task UpdateSeries_ChangesAllOccurrencesFromDate()
    {
        var employeeRepo = new FakeEmployeeRepository();
        var taskRepo = new FakeWorkTaskRepository();
        var service = new ScheduleService(employeeRepo, taskRepo, NullLogger<ScheduleService>.Instance);
        var group = Guid.NewGuid();
        var monday = new DateTime(2026, 7, 6);

        var first = await service.CreateTaskAsync(new WorkTask
        {
            EmployeeId = 1,
            Description = "Montage",
            StartDate = monday,
            RecurrenceGroupId = group
        });
        await service.CreateTaskAsync(new WorkTask
        {
            EmployeeId = 1,
            Description = "Montage",
            StartDate = monday.AddDays(7),
            RecurrenceGroupId = group
        });

        var template = first.Clone();
        template.Description = "Montage Nord";
        await service.UpdateSeriesAsync(template, fromDateInclusive: monday.AddDays(7));

        Assert.Equal("Montage", taskRepo.Tasks[0].Description);
        Assert.Equal("Montage Nord", taskRepo.Tasks[1].Description);
    }

    private sealed class FakeEmployeeRepository : IEmployeeRepository
    {
        public Task<Employee> AddAsync(Employee employee) => Task.FromResult(employee);
        public Task DeleteAsync(int id) => Task.CompletedTask;
        public Task<List<Employee>> GetAllAsync() => Task.FromResult(new List<Employee>());
        public Task<Employee?> GetByIdAsync(int id) => Task.FromResult<Employee?>(null);
        public Task UpdateAsync(Employee employee) => Task.CompletedTask;
        public Task UpdateOrderAsync(IReadOnlyList<int> employeeIdsInOrder) => Task.CompletedTask;
        public Task NormalizeOrderByEmployeeNumberAsync() => Task.CompletedTask;
        public Task NormalizeOrderByNameAsync() => Task.CompletedTask;
    }

    private sealed class FakeWorkTaskRepository : IWorkTaskRepository
    {
        private int _nextId = 1;
        public List<WorkTask> Tasks { get; } = new();
        public Task<WorkTask> AddAsync(WorkTask task)
        {
            task.Id = _nextId++;
            Tasks.Add(task);
            return Task.FromResult(task);
        }

        public Task DeleteAsync(int id)
        {
            Tasks.RemoveAll(t => t.Id == id);
            return Task.CompletedTask;
        }

        public Task<WorkTask?> GetByIdAsync(int id) => Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id));
        public Task<List<WorkTask>> GetTasksForDateRangeAsync(DateTime start, DateTime end) =>
            Task.FromResult(Tasks
                .Where(t => t.StartDate.Date <= end.Date &&
                            (t.EndDate == null
                                ? t.StartDate.Date >= start.Date
                                : t.EndDate.Value.Date >= start.Date))
                .ToList());
        public Task<List<WorkTask>> GetTasksForEmployeeDateRangeAsync(int employeeId, DateTime start, DateTime end) =>
            Task.FromResult(Tasks
                .Where(t => t.EmployeeId == employeeId &&
                            t.StartDate.Date <= end.Date &&
                            (t.EndDate == null || t.EndDate.Value.Date >= start.Date))
                .ToList());
        public Task<List<WorkTask>> GetTasksForEmployeeAsync(int employeeId, Domain.ValueObjects.WeekInfo week) => Task.FromResult(new List<WorkTask>());
        public Task<List<WorkTask>> GetTasksForWeekAsync(Domain.ValueObjects.WeekInfo week) => Task.FromResult(new List<WorkTask>());
        public Task UpdateAsync(WorkTask task)
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
                existing.Description = task.Description;
                existing.StartDate = task.StartDate;
                existing.EndDate = task.EndDate;
                existing.StartTime = task.StartTime;
                existing.EndTime = task.EndTime;
                existing.Location = task.Location;
                existing.Project = task.Project;
                existing.Notes = task.Notes;
                existing.Type = task.Type;
                existing.Color = task.Color;
                existing.RecurrenceGroupId = task.RecurrenceGroupId;
                existing.EmployeeId = task.EmployeeId;
            }
            return Task.CompletedTask;
        }

        public Task<List<WorkTask>> SearchAsync(string query, int take = 80) =>
            Task.FromResult(Tasks
                .Where(t => t.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(take)
                .ToList());

        public Task<List<WorkTask>> GetByRecurrenceGroupAsync(Guid recurrenceGroupId) =>
            Task.FromResult(Tasks.Where(t => t.RecurrenceGroupId == recurrenceGroupId).ToList());

        public Task DeleteByIdsAsync(IReadOnlyList<int> ids)
        {
            Tasks.RemoveAll(t => ids.Contains(t.Id));
            return Task.CompletedTask;
        }
    }
}
