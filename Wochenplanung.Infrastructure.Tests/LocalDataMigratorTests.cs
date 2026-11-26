using Microsoft.EntityFrameworkCore;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Infrastructure;
using Wochenplanung.Infrastructure.Data;
using Xunit;

namespace Wochenplanung.Infrastructure.Tests;

public sealed class LocalDataMigratorTests
{
    [Fact]
    public async Task CopyAsync_CopiesEmployeesTasksAndTemplates()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wp-migrate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = LocalDataMigrator.SqliteFromPath(Path.Combine(dir, "source.db"));
            var dest = LocalDataMigrator.SqliteFromPath(Path.Combine(dir, "dest.db"));

            await using (var context = CreateContext(source))
            {
                await context.Database.EnsureCreatedAsync();
                context.Employees.Add(new Employee
                {
                    FirstName = "Anna",
                    LastName = "Meier",
                    EmployeeNumber = "12",
                    DisplayOrder = 3
                });
                await context.SaveChangesAsync();
                var employeeId = context.Employees.Single().Id;
                context.Tasks.Add(new WorkTask
                {
                    EmployeeId = employeeId,
                    Description = "Schule",
                    StartDate = new DateTime(2026, 7, 6),
                    Type = TaskType.School,
                    Color = TaskColor.Blue
                });
                context.ScheduleTemplates.Add(new ScheduleTemplate
                {
                    Description = "Ferien",
                    Type = TaskType.Vacation,
                    Color = TaskColor.Beige,
                    IsFavorite = true
                });
                await context.SaveChangesAsync();
            }

            var result = await LocalDataMigrator.CopyAsync(source, dest);
            Assert.True(result.Copied);
            Assert.Equal(1, result.Employees);
            Assert.Equal(1, result.Tasks);
            Assert.Equal(1, result.Templates);

            await using var destContext = CreateContext(dest);
            var employee = Assert.Single(await destContext.Employees.ToListAsync());
            Assert.Equal("Anna", employee.FirstName);
            Assert.Equal("12", employee.EmployeeNumber);
            var task = Assert.Single(await destContext.Tasks.ToListAsync());
            Assert.Equal("Schule", task.Description);
            Assert.Equal(employee.Id, task.EmployeeId);
            var template = Assert.Single(await destContext.ScheduleTemplates.ToListAsync());
            Assert.Equal("Ferien", template.Description);
            Assert.True(template.IsFavorite);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                // SQLite kann die Datei kurz nach dem Dispose noch halten.
            }
        }
    }

    [Fact]
    public async Task CopyAsync_DoesNotOverwriteDestination()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wp-migrate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = LocalDataMigrator.SqliteFromPath(Path.Combine(dir, "source.db"));
            var dest = LocalDataMigrator.SqliteFromPath(Path.Combine(dir, "dest.db"));

            await using (var context = CreateContext(source))
            {
                await context.Database.EnsureCreatedAsync();
                context.Employees.Add(new Employee { FirstName = "Lokal", LastName = "Alt" });
                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext(dest))
            {
                await context.Database.EnsureCreatedAsync();
                context.Employees.Add(new Employee { FirstName = "Server", LastName = "Neu" });
                await context.SaveChangesAsync();
            }

            var result = await LocalDataMigrator.CopyAsync(source, dest);
            Assert.False(result.Copied);
            Assert.Equal(LocalDataMigrationStatus.DestinationNotEmpty, result.Status);

            await using var destContext = CreateContext(dest);
            var employee = Assert.Single(await destContext.Employees.ToListAsync());
            Assert.Equal("Server", employee.FirstName);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                // SQLite kann die Datei kurz nach dem Dispose noch halten.
            }
        }
    }

    private static ScheduleDbContext CreateContext(DatabaseOptions options)
    {
        var builder = new DbContextOptionsBuilder<ScheduleDbContext>();
        DependencyInjection.ConfigureProvider(builder, options);
        return new ScheduleDbContext(builder.Options);
    }
}
