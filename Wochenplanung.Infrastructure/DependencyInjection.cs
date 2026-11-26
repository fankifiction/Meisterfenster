using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Application.Services;
using Wochenplanung.Infrastructure.Data;
using Wochenplanung.Infrastructure.Repositories;

namespace Wochenplanung.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, DatabaseOptions database)
    {
        services.AddSingleton(database);
        services.AddSingleton<IDatabaseInfo>(database);
        services.AddDbContext<ScheduleDbContext>(options => ConfigureProvider(options, database));

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
        services.AddScoped<IScheduleTemplateRepository, ScheduleTemplateRepository>();
        services.AddScoped<IDatabaseStartup, DatabaseStartup>();
        services.AddScoped<ScheduleService>();

        return services;
    }

    public static void ConfigureProvider(DbContextOptionsBuilder options, DatabaseOptions database)
    {
        if (database.Provider == DatabaseProvider.SqlServer)
            options.UseSqlServer(database.ConnectionString);
        else
            options.UseSqlite(database.ConnectionString);
    }

    public static async Task<(bool Ok, string Message)> TestConnectionAsync(DatabaseOptions database)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(SqlServerConnection.StartupTimeoutSeconds));
        try
        {
            var builder = new DbContextOptionsBuilder<ScheduleDbContext>();
            ConfigureProvider(builder, database);
            await using var context = new ScheduleDbContext(builder.Options);
            if (await context.Database.CanConnectAsync(cts.Token).WaitAsync(cts.Token))
                return (true, "OK");

            if (database.Provider == DatabaseProvider.SqlServer
                && await CanConnectToMasterAsync(database.ConnectionString, cts.Token))
                return (true, "database-missing");

            return (false, "Keine Verbindung.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            return (false, "Zeitüberschreitung. Server nicht erreichbar (Netzwerk, Firewall oder SQL Browser).");
        }
        catch (Exception ex)
        {
            if (database.Provider == DatabaseProvider.SqlServer)
            {
                try
                {
                    if (await CanConnectToMasterAsync(database.ConnectionString, cts.Token))
                        return (true, "database-missing");
                }
                catch
                {
                    // Fall through to the original error.
                }
            }

            return (false, ex.InnerException?.Message ?? ex.Message);
        }
    }

    private static async Task<bool> CanConnectToMasterAsync(string connectionString, CancellationToken cancellationToken)
    {
        var masterBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
        masterBuilder.UseSqlServer(ToMasterConnectionString(connectionString));
        await using var masterContext = new ScheduleDbContext(masterBuilder.Options);
        return await masterContext.Database.CanConnectAsync(cancellationToken).WaitAsync(cancellationToken);
    }

    private static string ToMasterConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }
}
