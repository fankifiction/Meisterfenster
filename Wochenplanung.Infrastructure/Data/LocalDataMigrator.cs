using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wochenplanung.Domain.Entities;
using Infra = Wochenplanung.Infrastructure;

namespace Wochenplanung.Infrastructure.Data;

public sealed record LocalDataMigrationResult
{
    public bool Copied { get; init; }
    public bool CanCopy { get; init; }
    public LocalDataMigrationStatus Status { get; init; }
    public int Employees { get; init; }
    public int Tasks { get; init; }
    public int Templates { get; init; }
    public string? Error { get; init; }
}

public enum LocalDataMigrationStatus
{
    Copied,
    CanCopy,
    SourceMissing,
    SourceEmpty,
    DestinationNotEmpty,
    SameDatabase,
    Failed
}

public static class LocalDataMigrator
{
    public static async Task<LocalDataMigrationResult> InspectAsync(
        DatabaseOptions source,
        DatabaseOptions dest,
        ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        if (IsSameDatabase(source, dest))
            return new LocalDataMigrationResult { Status = LocalDataMigrationStatus.SameDatabase };

        try
        {
            await using var sourceContext = CreateContext(source);
            if (!await sourceContext.Database.CanConnectAsync())
                return new LocalDataMigrationResult { Status = LocalDataMigrationStatus.SourceMissing };

            await PrepareSourceAsync(sourceContext, logger);
            var employeeCount = await sourceContext.Employees.AsNoTracking().CountAsync();
            var taskCount = await sourceContext.Tasks.AsNoTracking().CountAsync();
            var templateCount = await sourceContext.ScheduleTemplates.AsNoTracking().CountAsync();
            if (employeeCount == 0 && taskCount == 0 && templateCount == 0)
                return new LocalDataMigrationResult { Status = LocalDataMigrationStatus.SourceEmpty };

            await using var destContext = CreateContext(dest);
            await DatabaseInitializer.InitializeAsync(destContext, logger);
            if (await destContext.Employees.AsNoTracking().AnyAsync() || await destContext.Tasks.AsNoTracking().AnyAsync())
            {
                return new LocalDataMigrationResult
                {
                    Status = LocalDataMigrationStatus.DestinationNotEmpty,
                    Employees = employeeCount,
                    Tasks = taskCount,
                    Templates = templateCount
                };
            }

            return new LocalDataMigrationResult
            {
                CanCopy = true,
                Status = LocalDataMigrationStatus.CanCopy,
                Employees = employeeCount,
                Tasks = taskCount,
                Templates = templateCount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lokale Daten konnten nicht geprüft werden");
            return new LocalDataMigrationResult
            {
                Status = LocalDataMigrationStatus.Failed,
                Error = ex.InnerException?.Message ?? ex.Message
            };
        }
    }

    public static async Task<LocalDataMigrationResult> CopyAsync(
        DatabaseOptions source,
        DatabaseOptions dest,
        ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        var inspect = await InspectAsync(source, dest, logger);
        if (!inspect.CanCopy)
            return inspect;

        try
        {
            await using var sourceContext = CreateContext(source);
            await PrepareSourceAsync(sourceContext, logger);
            var employees = await sourceContext.Employees.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
            var tasks = await sourceContext.Tasks.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
            var templates = await sourceContext.ScheduleTemplates.AsNoTracking().OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).ToListAsync();

            await using var destContext = CreateContext(dest);
            await DatabaseInitializer.InitializeAsync(destContext, logger);
            if (await destContext.Employees.AnyAsync() || await destContext.Tasks.AnyAsync())
            {
                return inspect with { CanCopy = false, Status = LocalDataMigrationStatus.DestinationNotEmpty };
            }

            var idMap = new Dictionary<int, int>();
            foreach (var employee in employees)
            {
                var copy = new Employee
                {
                    EmployeeNumber = employee.EmployeeNumber,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    Role = employee.Role,
                    DisplayOrder = employee.DisplayOrder,
                    Version = 0,
                    LastModifiedBy = employee.LastModifiedBy,
                    LastModifiedAt = employee.LastModifiedAt
                };
                destContext.Employees.Add(copy);
                await destContext.SaveChangesAsync();
                idMap[employee.Id] = copy.Id;
                destContext.Entry(copy).State = EntityState.Detached;
            }

            foreach (var task in tasks)
            {
                if (!idMap.TryGetValue(task.EmployeeId, out var newEmployeeId))
                    continue;

                destContext.Tasks.Add(new WorkTask
                {
                    EmployeeId = newEmployeeId,
                    Description = task.Description,
                    StartDate = task.StartDate,
                    EndDate = task.EndDate,
                    StartTime = task.StartTime,
                    EndTime = task.EndTime,
                    Location = task.Location,
                    Project = task.Project,
                    Notes = task.Notes,
                    Type = task.Type,
                    Color = task.Color,
                    Version = 0,
                    LastModifiedBy = task.LastModifiedBy,
                    LastModifiedAt = task.LastModifiedAt
                });
            }

            foreach (var template in templates)
            {
                destContext.ScheduleTemplates.Add(new ScheduleTemplate
                {
                    Description = template.Description,
                    Type = template.Type,
                    Color = template.Color,
                    IsFavorite = template.IsFavorite,
                    DisplayOrder = template.DisplayOrder
                });
            }

            await destContext.SaveChangesAsync();
            logger.LogInformation(
                "Lokale Daten auf den Server kopiert: {Employees} Mitarbeiter, {Tasks} Aufgaben, {Templates} Vorlagen",
                employees.Count, tasks.Count, templates.Count);

            return new LocalDataMigrationResult
            {
                Copied = true,
                Status = LocalDataMigrationStatus.Copied,
                Employees = employees.Count,
                Tasks = tasks.Count,
                Templates = templates.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lokale Daten konnten nicht auf den Server kopiert werden");
            return new LocalDataMigrationResult
            {
                Status = LocalDataMigrationStatus.Failed,
                Error = ex.InnerException?.Message ?? ex.Message
            };
        }
    }

    public static DatabaseOptions SqliteFromPath(string path) => new()
    {
        Provider = DatabaseProvider.Sqlite,
        ConnectionString = $"Data Source={path}",
        DisplayName = "Lokal"
    };

    private static bool IsSameDatabase(DatabaseOptions source, DatabaseOptions dest) =>
        source.Provider == dest.Provider
        && string.Equals(source.ConnectionString, dest.ConnectionString, StringComparison.OrdinalIgnoreCase);

    private static ScheduleDbContext CreateContext(DatabaseOptions database)
    {
        var builder = new DbContextOptionsBuilder<ScheduleDbContext>();
        Infra.DependencyInjection.ConfigureProvider(builder, database);
        return new ScheduleDbContext(builder.Options);
    }

    private static async Task PrepareSourceAsync(ScheduleDbContext source, ILogger logger)
    {
        if (source.Database.IsSqlite())
        {
            try
            {
                await DatabaseInitializer.InitializeAsync(source, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Lokale SQLite-Datenbank konnte nicht vollständig vorbereitet werden");
            }
        }
        else
        {
            await DatabaseInitializer.InitializeAsync(source, logger);
        }
    }
}
