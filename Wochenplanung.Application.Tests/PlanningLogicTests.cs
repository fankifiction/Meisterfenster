using Wochenplanung.Application.Services;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;
using Xunit;

namespace Wochenplanung.Application.Tests;

public sealed class TaskFilterTests
{
    private static WorkTask Task(string description, int employeeId = 1, TaskType type = TaskType.Work, string? location = null, string? project = null, string? notes = null) =>
        new()
        {
            EmployeeId = employeeId,
            Description = description,
            Location = location,
            Project = project,
            Notes = notes,
            Type = type,
            StartDate = new DateTime(2026, 7, 6)
        };

    [Fact]
    public void Apply_FiltersByEmployeeTypeAndText()
    {
        var employee = new Employee { Id = 2, FirstName = "Anna", LastName = "Meier" };
        var tasks = new[]
        {
            Task("Montage Halle", employeeId: 2, location: "Werkstatt"),
            Task("Montage Halle", employeeId: 1),
            Task("Schule", employeeId: 2, type: TaskType.School),
            Task("Offerte", employeeId: 2, project: "Halle Nord")
        };

        var filtered = TaskFilter.Apply(tasks, employee, TaskType.Work, "halle");

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, t => Assert.Equal(2, t.EmployeeId));
        Assert.Contains(filtered, t => t.Description == "Montage Halle");
        Assert.Contains(filtered, t => t.Project == "Halle Nord");
    }

    [Fact]
    public void Apply_WithoutCriteria_ReturnsAll()
    {
        var tasks = new[] { Task("A"), Task("B") };
        var filtered = TaskFilter.Apply(tasks, null, null, null);
        Assert.Equal(2, filtered.Count);
    }
}

public sealed class CopiedTaskPlannerTests
{
    [Fact]
    public void BuildSegments_WithoutSelection_CopiesWholeRange()
    {
        var start = new DateTime(2026, 7, 6);
        var end = new DateTime(2026, 7, 10);

        var segments = CopiedTaskPlanner.BuildSegments(start, end, Array.Empty<DateTime>());

        Assert.Equal(new[] { (0, 4) }, segments);
    }

    [Fact]
    public void BuildSegments_SplitsNonConsecutiveDays()
    {
        var start = new DateTime(2026, 7, 6);
        var end = new DateTime(2026, 7, 10);
        var selected = new[]
        {
            new DateTime(2026, 7, 6),
            new DateTime(2026, 7, 7),
            new DateTime(2026, 7, 10)
        };

        var segments = CopiedTaskPlanner.BuildSegments(start, end, selected);

        Assert.Equal(new[] { (0, 1), (4, 4) }, segments);
    }
}

public sealed class WeekInfoTests
{
    [Fact]
    public void EndDate_IsSunday_SevenDaysFromMonday()
    {
        var week = new WeekInfo(new DateTime(2026, 7, 8)); // Mittwoch
        Assert.Equal(DayOfWeek.Monday, week.StartDate.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, week.EndDate.DayOfWeek);
        Assert.Equal(6, (week.EndDate.Date - week.StartDate.Date).Days);
        Assert.Equal(7, week.GetDays().Count);
        Assert.Equal(5, week.GetDays(includeWeekend: false).Count);
        Assert.Equal(DayOfWeek.Friday, week.GetDays(includeWeekend: false)[^1].DayOfWeek);
    }
}

public sealed class WorkTaskCloneTests
{
    [Fact]
    public void Clone_CanDropId()
    {
        var source = new WorkTask
        {
            Id = 42,
            EmployeeId = 7,
            Description = "Montage",
            StartDate = new DateTime(2026, 7, 6),
            Type = TaskType.Work,
            Color = TaskColor.Blue
        };

        var copy = source.Clone(includeId: false);

        Assert.Equal(0, copy.Id);
        Assert.Equal(source.EmployeeId, copy.EmployeeId);
        Assert.Equal(source.Description, copy.Description);
        Assert.Equal(source.Color, copy.Color);
        copy.RecurrenceGroupId = Guid.NewGuid();
        var withSeries = source.Clone();
        withSeries.RecurrenceGroupId = copy.RecurrenceGroupId;
        Assert.Equal(copy.RecurrenceGroupId, withSeries.RecurrenceGroupId);
        Assert.NotSame(source, copy);
    }
}

public sealed class WorkloadCalculatorTests
{
    [Fact]
    public void UntimedTask_CountsEightHoursPerDayInRange()
    {
        var tasks = new[]
        {
            new WorkTask
            {
                EmployeeId = 1,
                Type = TaskType.Work,
                StartDate = new DateTime(2026, 8, 17),
                EndDate = new DateTime(2026, 8, 19)
            }
        };

        var (days, hours) = WorkloadCalculator.ForTasks(tasks, new DateTime(2026, 8, 17), new DateTime(2026, 8, 23));

        Assert.Equal(3, days);
        Assert.Equal(24, hours);
    }

    [Fact]
    public void TimedMultiDayTask_MultipliesDailyHoursByDays()
    {
        var tasks = new[]
        {
            new WorkTask
            {
                EmployeeId = 1,
                Type = TaskType.Work,
                StartDate = new DateTime(2026, 8, 17),
                EndDate = new DateTime(2026, 8, 18),
                StartTime = TimeSpan.FromHours(7),
                EndTime = TimeSpan.FromHours(16)
            }
        };

        var (_, hours) = WorkloadCalculator.ForTasks(tasks, new DateTime(2026, 8, 17), new DateTime(2026, 8, 23));

        Assert.Equal(18, hours);
    }

    [Fact]
    public void VacationAndAbsence_AreExcluded()
    {
        var tasks = new[]
        {
            new WorkTask { EmployeeId = 1, Type = TaskType.Vacation, StartDate = new DateTime(2026, 8, 20) },
            new WorkTask { EmployeeId = 1, Type = TaskType.Absence, StartDate = new DateTime(2026, 8, 21) },
            new WorkTask { EmployeeId = 1, Type = TaskType.Work, StartDate = new DateTime(2026, 8, 17) }
        };

        var (days, hours) = WorkloadCalculator.ForTasks(tasks, new DateTime(2026, 8, 17), new DateTime(2026, 8, 23));

        Assert.Equal(1, days);
        Assert.Equal(8, hours);
    }

    [Fact]
    public void OverlappingTasks_DoNotDoubleCountDays()
    {
        var tasks = new[]
        {
            new WorkTask { EmployeeId = 1, Type = TaskType.Work, StartDate = new DateTime(2026, 8, 17) },
            new WorkTask { EmployeeId = 1, Type = TaskType.Other, StartDate = new DateTime(2026, 8, 17) }
        };

        var (days, hours) = WorkloadCalculator.ForTasks(tasks, new DateTime(2026, 8, 17), new DateTime(2026, 8, 17));

        Assert.Equal(1, days);
        Assert.Equal(16, hours);
    }
}
