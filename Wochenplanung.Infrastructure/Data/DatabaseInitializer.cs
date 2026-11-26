using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wochenplanung.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        ScheduleDbContext context,
        ILogger logger,
        DatabaseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.Database.IsSqlServer())
            {
                await InitializeSqlServerAsync(context, logger, options, cancellationToken);
                return;
            }

            await EnsureLegacySchemaCompatibilityAsync(context, logger, cancellationToken);
            await BaselineExistingDatabaseIfNeededAsync(context, logger, cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
            await SchemaPatcher.ApplyAsync(context, logger, cancellationToken);

            logger.LogInformation("Datenbank erfolgreich initialisiert");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fehler beim Initialisieren der Datenbank: {Message}", ex.Message);
            throw;
        }
    }

    private static async Task InitializeSqlServerAsync(
        ScheduleDbContext context,
        ILogger logger,
        DatabaseOptions? options,
        CancellationToken cancellationToken)
    {
        // Empty databases are created from the current model. Existing office databases
        // keep their data; SchemaPatcher adds missing columns/tables on every start.
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await SqlServerGuard.ApplyAsync(context, logger, cancellationToken);
        await SchemaPatcher.ApplyAsync(context, logger, cancellationToken);
        await BaselineSqlServerIfNeededAsync(context, logger, cancellationToken);
        if (options != null)
            options.CanWrite = await SqlServerGuard.CanWriteAsync(context, cancellationToken);
        logger.LogInformation("SQL-Server-Datenbank ist bereit (CanWrite={CanWrite})", options?.CanWrite ?? true);
    }

    private static async Task EnsureLegacySchemaCompatibilityAsync(
        ScheduleDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        if (!await SqliteTableExistsAsync(connection, "Employees"))
            return;

        await EnsureSqliteEmployeeColumnAsync(connection, "EmployeeNumber", "TEXT NULL", logger);
        await EnsureSqliteEmployeeColumnAsync(connection, "Role", "TEXT NULL", logger);
        await EnsureSqliteEmployeeColumnAsync(connection, "DisplayOrder", "INTEGER NOT NULL DEFAULT 0", logger);
    }

    private static async Task EnsureSqliteEmployeeColumnAsync(
        System.Data.Common.DbConnection connection,
        string columnName,
        string sqlTypeDefinition,
        ILogger logger)
    {
        if (await SqliteColumnExistsAsync(connection, "Employees", columnName))
            return;

        await using var alterCmd = connection.CreateCommand();
        alterCmd.CommandText = $"ALTER TABLE \"Employees\" ADD COLUMN \"{columnName}\" {sqlTypeDefinition};";
        await alterCmd.ExecuteNonQueryAsync();
        logger.LogInformation("Legacy-Spalte nachgezogen: Employees.{ColumnName}", columnName);
    }

    private static async Task<bool> SqliteTableExistsAsync(System.Data.Common.DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = $name;";
        var param = cmd.CreateParameter();
        param.ParameterName = "$name";
        param.Value = tableName;
        cmd.Parameters.Add(param);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> SqliteColumnExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var existingName = reader["name"]?.ToString();
            if (string.Equals(existingName, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task BaselineExistingDatabaseIfNeededAsync(
        ScheduleDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var migrations = context.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
            return;

        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (applied.Count > 0)
            return;

        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        if (!await SqliteTableExistsAsync(connection, "Employees") && !await SqliteTableExistsAsync(connection, "Tasks"))
            return;

        await using var createHistoryCmd = connection.CreateCommand();
        createHistoryCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """;
        await createHistoryCmd.ExecuteNonQueryAsync();

        await StampMigrationsAsync(connection, migrations, sqlite: true);
        logger.LogInformation("Bestehende Datenbank auf EF-Migrationshistorie umgestellt.");
    }

    private static async Task BaselineSqlServerIfNeededAsync(
        ScheduleDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var migrations = context.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
            return;

        try
        {
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            if (applied.Count > 0)
                return;
        }
        catch
        {
            // History table may not exist yet on EnsureCreated databases.
        }

        await using var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var existsCmd = connection.CreateCommand();
        existsCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Employees';";
        if (Convert.ToInt32(await existsCmd.ExecuteScalarAsync()) == 0)
            return;

        await using var createHistoryCmd = connection.CreateCommand();
        createHistoryCmd.CommandText = """
            IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
            BEGIN
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
            END
            """;
        await createHistoryCmd.ExecuteNonQueryAsync();

        await StampMigrationsAsync(connection, migrations, sqlite: false);
        logger.LogInformation("SQL-Server-Datenbank auf EF-Migrationshistorie umgestellt.");
    }

    private static async Task StampMigrationsAsync(
        System.Data.Common.DbConnection connection,
        IReadOnlyList<string> migrations,
        bool sqlite)
    {
        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "8.0.22";
        foreach (var migration in migrations)
        {
            await using var insertCmd = connection.CreateCommand();
            if (sqlite)
            {
                insertCmd.CommandText = """
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
                    VALUES ($id, $version);
                    """;
                AddParam(insertCmd, "$id", migration);
                AddParam(insertCmd, "$version", productVersion);
            }
            else
            {
                insertCmd.CommandText = """
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = @id)
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId],[ProductVersion])
                    VALUES (@id, @version);
                    """;
                AddParam(insertCmd, "@id", migration);
                AddParam(insertCmd, "@version", productVersion);
            }

            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private static void AddParam(System.Data.Common.DbCommand command, string name, string value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }
}
