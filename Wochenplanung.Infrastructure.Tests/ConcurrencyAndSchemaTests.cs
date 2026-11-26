using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wochenplanung.Application.Exceptions;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Infrastructure.Data;
using Wochenplanung.Infrastructure.Repositories;
using Xunit;

namespace Wochenplanung.Infrastructure.Tests;

public sealed class EmployeeRepositoryConcurrencyTests
{
    [Fact]
    public async Task UpdateAsync_ThrowsWhenVersionDoesNotMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ScheduleDbContext>().UseSqlite(connection).Options;
        await using var context = new ScheduleDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var repo = new EmployeeRepository(context);
        var created = await repo.AddAsync(new Employee { FirstName = "Anna", LastName = "Meier" });

        var stale = new Employee
        {
            Id = created.Id,
            FirstName = "Anna",
            LastName = "Alt",
            Version = created.Version - 1,
            DisplayOrder = created.DisplayOrder
        };

        created.LastName = "Neu";
        await repo.UpdateAsync(created);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo.UpdateAsync(stale));
    }

    [Fact]
    public async Task UpdateAsync_IncrementsVersionAndStampsActor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ScheduleDbContext>().UseSqlite(connection).Options;
        await using var context = new ScheduleDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var repo = new EmployeeRepository(context);
        var created = await repo.AddAsync(new Employee { FirstName = "Jan", LastName = "Keller" });
        var originalVersion = created.Version;

        created.Role = "Meister";
        await repo.UpdateAsync(created);

        Assert.Equal(originalVersion + 1, created.Version);
        Assert.False(string.IsNullOrWhiteSpace(created.LastModifiedBy));
        Assert.NotNull(created.LastModifiedAt);
    }
}

public sealed class SchemaPatcherTests
{
    [Fact]
    public async Task ApplyAsync_AddsAuditColumnsToLegacySqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE Employees (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL
                );
                CREATE TABLE Tasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    EmployeeId INTEGER NOT NULL,
                    Description TEXT NOT NULL,
                    StartDate TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    Color INTEGER NOT NULL
                );
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ScheduleDbContext>().UseSqlite(connection).Options;
        await using var context = new ScheduleDbContext(options);
        await SchemaPatcher.ApplyAsync(context, NullLogger.Instance);

        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA table_info(Employees);";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader["name"]?.ToString() ?? "");

        Assert.Contains("Version", columns);
        Assert.Contains("LastModifiedBy", columns);
        Assert.Contains("LastModifiedAt", columns);
        Assert.Contains("IsActive", columns);

        await using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ScheduleTemplates';";
        Assert.Equal(1, Convert.ToInt32(await tableCheck.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task InitializeAsync_MigratesEmptySqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), "wp-init-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using (var context = new ScheduleDbContext(options))
            {
                await DatabaseInitializer.InitializeAsync(context, NullLogger.Instance);

                Assert.True(await context.Database.CanConnectAsync());
                var applied = await context.Database.GetAppliedMigrationsAsync();
                Assert.Contains(applied, m => m.Contains("InitialCreate"));
                Assert.Contains(applied, m => m.Contains("AddMultiUserAudit"));
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // Temp-Datei kann kurz von SQLite gehalten werden.
            }
        }
    }
}

public sealed class WorkTaskConcurrencyTests
{
    [Fact]
    public async Task UpdateAsync_ThrowsWhenVersionDoesNotMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ScheduleDbContext>().UseSqlite(connection).Options;
        await using var context = new ScheduleDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Employees.Add(new Employee { FirstName = "Roger", LastName = "Meier" });
        await context.SaveChangesAsync();
        var employeeId = context.Employees.Single().Id;

        var repo = new WorkTaskRepository(context);
        var created = await repo.AddAsync(new WorkTask
        {
            EmployeeId = employeeId,
            Description = "Montage",
            StartDate = DateTime.Today
        });

        var stale = created.Clone();
        stale.Version = created.Version - 1;
        stale.Description = "Alt";

        created.Description = "Neu";
        await repo.UpdateAsync(created);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo.UpdateAsync(stale));
    }
}
