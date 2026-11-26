using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Services;
using Xunit;

namespace Wochenplanung.Presentation.Tests;

public sealed class ScheduleTableLayoutTests
{
    private static WorkTask Task(string description, int employeeId = 1, DateTime? start = null, DateTime? end = null, int? id = null) =>
        new()
        {
            Id = id ?? Math.Abs(description.GetHashCode()),
            EmployeeId = employeeId,
            Description = description,
            StartDate = start ?? new DateTime(2026, 7, 6),
            EndDate = end
        };

    [Fact]
    public void SameActivity_StaysOnSameLine_AndGapFilledByOtherTask()
    {
        // Test Di-Fr Linie 0, Sonstiges Mo-Mi Linie 1, Schule Mo-Do Linie 2.
        // Ferien Do-Fr teilt Linie 1 mit Sonstiges (kein Tages-Overlap).
        // Mo ohne Test: Linie 0 bleibt leer (Sonstiges bleibt auf Linie 1).
        var mon = new DateTime(2026, 7, 6);
        var tue = new DateTime(2026, 7, 7);
        var wed = new DateTime(2026, 7, 8);
        var thu = new DateTime(2026, 7, 9);
        var fri = new DateTime(2026, 7, 10);
        var employee = new Employee { Id = 1, FirstName = "tt", LastName = "asda" };
        var days = new[] { mon, tue, wed, thu, fri };
        var tasks = new List<WorkTask>
        {
            Task("Test", start: tue, end: fri, id: 1),
            Task("Sonstiges", start: mon, end: wed, id: 2),
            Task("Schule", start: mon, end: thu, id: 3),
            Task("Ferien", start: thu, end: fri, id: 4)
        };

        var layout = ScheduleTableLayout.BuildEmployeeWeekLayout(employee, tasks, days);

        Assert.Equal("Test", layout[tue][0]!.Description);
        Assert.Equal("Test", layout[wed][0]!.Description);
        Assert.Equal("Test", layout[thu][0]!.Description);
        Assert.Equal("Test", layout[fri][0]!.Description);

        Assert.Equal("Sonstiges", layout[tue][1]!.Description);
        Assert.Equal("Sonstiges", layout[wed][1]!.Description);
        Assert.Equal("Sonstiges", layout[mon][1]!.Description);

        Assert.Equal("Schule", layout[tue][2]!.Description);
        Assert.Equal("Schule", layout[wed][2]!.Description);
        Assert.Equal("Schule", layout[thu][2]!.Description);
        Assert.Equal("Schule", layout[mon][2]!.Description);

        Assert.Null(layout[mon][0]);

        Assert.Equal("Ferien", layout[thu][1]!.Description);
        Assert.Equal("Ferien", layout[fri][1]!.Description);
    }

    [Fact]
    public void SingleDayTask_FillsEmptyAnchorGap()
    {
        var mon = new DateTime(2026, 7, 6);
        var tue = new DateTime(2026, 7, 7);
        var employee = new Employee { Id = 1, FirstName = "A", LastName = "B" };
        var days = new[] { mon, tue };
        var tasks = new List<WorkTask>
        {
            Task("Montage", start: mon, end: tue, id: 1),
            Task("Schule", start: tue, end: tue, id: 2),
            Task("Krank", start: mon, id: 3)
        };

        var layout = ScheduleTableLayout.BuildEmployeeWeekLayout(employee, tasks, days);

        Assert.Equal("Montage", layout[mon][0]!.Description);
        Assert.Equal("Montage", layout[tue][0]!.Description);
        Assert.Equal("Krank", layout[mon][1]!.Description);
        Assert.Equal("Schule", layout[tue][1]!.Description);
    }

    [Fact]
    public void NonOverlappingMultiDayTasks_CanShareSameLine()
    {
        var mon = new DateTime(2026, 7, 6);
        var tue = new DateTime(2026, 7, 7);
        var wed = new DateTime(2026, 7, 8);
        var thu = new DateTime(2026, 7, 9);
        var employee = new Employee { Id = 1, FirstName = "A", LastName = "B" };
        var days = new[] { mon, tue, wed, thu };
        var tasks = new List<WorkTask>
        {
            Task("Offerte", start: mon, end: tue, id: 1),
            Task("Ferien", start: wed, end: thu, id: 2)
        };

        var layout = ScheduleTableLayout.BuildEmployeeWeekLayout(employee, tasks, days);

        Assert.Equal("Offerte", layout[mon][0]!.Description);
        Assert.Equal("Offerte", layout[tue][0]!.Description);
        Assert.Equal("Ferien", layout[wed][0]!.Description);
        Assert.Equal("Ferien", layout[thu][0]!.Description);
        Assert.Single(layout[mon]);
    }

    [Fact]
    public void CompactEmptySlots_RemovesNulls()
    {
        var a = Task("A");
        var b = Task("B");
        var packed = ScheduleTableLayout.CompactEmptySlots(new WorkTask?[] { null, a, null, b, null });

        Assert.Equal(2, packed.Count);
        Assert.Same(a, packed[0]);
        Assert.Same(b, packed[1]);
    }

    [Fact]
    public void SameDescriptionOnSameDay_DoesNotOverwrite()
    {
        var mon = new DateTime(2026, 7, 6);
        var tue = new DateTime(2026, 7, 7);
        var employee = new Employee { Id = 1, FirstName = "A", LastName = "B" };
        var days = new[] { mon, tue };
        var tasks = new List<WorkTask>
        {
            Task("Putzen", start: mon, end: tue, id: 1),
            Task("Putzen", start: mon, id: 2)
        };

        var layout = ScheduleTableLayout.BuildEmployeeWeekLayout(employee, tasks, days);
        var monday = layout[mon].Where(t => t != null).Select(t => t!.Id).ToHashSet();

        Assert.Contains(1, monday);
        Assert.Contains(2, monday);
        Assert.Equal("Putzen", layout[tue][0]!.Description);
    }

    [Fact]
    public void PackLayoutForExport_RemovesAlignmentGaps()
    {
        var mon = new DateTime(2026, 7, 6);
        var tue = new DateTime(2026, 7, 7);
        var employee = new Employee { Id = 1, FirstName = "A", LastName = "B" };
        var days = new[] { mon, tue };
        var layout = ScheduleTableLayout.BuildEmployeeWeekLayout(employee, new List<WorkTask>
        {
            Task("Linie", start: mon, end: tue, id: 1),
            Task("NurDienstag", start: tue, id: 2)
        }, days);

        Assert.Null(layout[mon][1]);

        var packed = ScheduleTableLayout.PackLayoutForExport(layout);
        Assert.Equal("Linie", packed[tue][0]!.Description);
        Assert.Equal("NurDienstag", packed[tue][1]!.Description);
        Assert.Null(packed[mon][1]);
        Assert.Equal("Linie", packed[mon][0]!.Description);
    }
}
