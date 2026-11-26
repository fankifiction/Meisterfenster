using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Infrastructure.Data;
using Wochenplanung.Infrastructure.Repositories;

namespace Wochenplanung.Infrastructure.Tests;

public sealed class WorkTaskRepositoryIntegrationTests
{
    [Fact]
    public async Task AddAndLoadTask_WorksWithSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ScheduleDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Employees.Add(new Employee { FirstName = "Max", LastName = "Muster", Role = "Meister" });
        await context.SaveChangesAsync();

        var employee = await context.Employees.FirstAsync();
        var repo = new WorkTaskRepository(context);
        await repo.AddAsync(new WorkTask
        {
            EmployeeId = employee.Id,
            Description = "Integration",
            StartDate = DateTime.Today
        });

        var inRange = await repo.GetTasksForDateRangeAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
        Assert.Single(inRange);
        Assert.Equal("Integration", inRange[0].Description);
    }
}
