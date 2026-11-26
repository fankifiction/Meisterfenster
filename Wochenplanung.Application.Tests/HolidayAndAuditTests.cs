using Wochenplanung.Application.Services;
using Wochenplanung.Domain.Entities;
using Xunit;

namespace Wochenplanung.Application.Tests;

public sealed class SwissPublicHolidaysTests
{
    [Fact]
    public void ForYear_2026_ContainsNationalDayAndEasterMonday()
    {
        var holidays = SwissPublicHolidays.ForYear(2026);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 8, 1) && h.IsFederal);
        Assert.Equal(new DateTime(2026, 4, 5), SwissPublicHolidays.EasterSunday(2026));
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 4, 6) && h.NameDe == "Ostermontag");
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 1, 1));
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 12, 25));
        Assert.True(holidays.SequenceEqual(holidays.OrderBy(h => h.Date)));
    }

    [Fact]
    public void ForYear_2026_ContainsSolothurnHolidays()
    {
        var holidays = SwissPublicHolidays.ForYear(2026);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 6, 4) && h.NameDe == "Fronleichnam" && h.IsSolothurn && !h.IsFederal);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 1, 2) && h.NameDe == "Berchtoldstag" && h.IsSolothurn && !h.IsFederal);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 8, 15) && h.NameDe == "Mariä Himmelfahrt" && h.IsSolothurn && !h.IsFederal);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 11, 1) && h.NameDe == "Allerheiligen" && h.IsSolothurn && !h.IsFederal);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 12, 8) && h.NameDe == "Mariä Empfängnis" && h.IsSolothurn && !h.IsFederal);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 12, 26) && h.NameDe == "Stephanstag" && h.IsSolothurn && !h.IsFederal);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 5, 14) && h.NameDe == "Auffahrt" && h.IsFederal && h.IsSolothurn);
        Assert.Equal(13, holidays.Count);
    }

    [Fact]
    public void ForYearRange_IncludesElevenYears()
    {
        var holidays = SwissPublicHolidays.ForYearRange(2026, 2036);
        Assert.Contains(holidays, h => h.Date == new DateTime(2026, 8, 1));
        Assert.Contains(holidays, h => h.Date == new DateTime(2036, 8, 1));
        Assert.Equal(11, holidays.Select(h => h.Date.Year).Distinct().Count());
    }
}

public sealed class HolidayPlannerTests
{
    [Fact]
    public void CreateTasks_CreatesOneEntryPerEmployeeAndDay()
    {
        var employees = new[]
        {
            new Employee { Id = 1, FirstName = "Anna", LastName = "Meier" },
            new Employee { Id = 2, FirstName = "Jan", LastName = "Keller" }
        };
        var days = HolidayPlanner.FromRange(new DateTime(2026, 8, 1), new DateTime(2026, 8, 1), "Nationalfeiertag");
        var tasks = HolidayPlanner.CreateTasks(employees, days);

        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, t =>
        {
            Assert.Equal("Nationalfeiertag", t.Description);
            Assert.Equal(TaskType.Absence, t.Type);
            Assert.Equal(new DateTime(2026, 8, 1), t.StartDate);
        });
    }

    [Fact]
    public void CreateRangeTasks_CreatesOneMultiDayEntryPerEmployee()
    {
        var employees = new[]
        {
            new Employee { Id = 1, FirstName = "Anna", LastName = "Meier" },
            new Employee { Id = 2, FirstName = "Jan", LastName = "Keller" }
        };
        var tasks = HolidayPlanner.CreateRangeTasks(
            employees,
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 7),
            "Betriebsferien");

        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, t =>
        {
            Assert.Equal("Betriebsferien", t.Description);
            Assert.Equal(TaskType.Vacation, t.Type);
            Assert.Equal(new DateTime(2026, 7, 20), t.StartDate);
            Assert.Equal(new DateTime(2026, 8, 7), t.EndDate);
        });
    }
}

public sealed class ChangeStampTests
{
    [Fact]
    public void Format_CombinesActorAndLocalTime()
    {
        var utc = new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
        var text = ChangeStamp.Format("anna@BUERO", utc);
        Assert.Contains("anna@BUERO", text);
        Assert.Contains("17.08.2026", text);
    }

    [Fact]
    public void Touch_SetsActorAndTimestamp()
    {
        var task = new WorkTask { Description = "Test" };
        ChangeStamp.Touch(task);
        Assert.False(string.IsNullOrWhiteSpace(task.LastModifiedBy));
        Assert.NotNull(task.LastModifiedAt);
    }
}
