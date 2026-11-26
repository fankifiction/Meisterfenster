using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wochenplanung.Infrastructure.Data;

/// <summary>
/// Idempotent schema patches for SQLite and SQL Server.
/// EF Migrate remains the SQLite baseline; SQL Server uses EnsureCreated for empty DBs
/// plus these patches so existing office databases pick up new columns.
/// </summary>
public static class SchemaPatcher
{
    public static async Task ApplyAsync(
        ScheduleDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        if (context.Database.IsSqlite())
            await ApplySqliteAsync(connection, logger);
        else
            await ApplySqlServerAsync(connection, logger);
    }

    private static async Task ApplySqliteAsync(DbConnection connection, ILogger logger)
    {
        await EnsureSqliteColumnAsync(connection, "Employees", "Version", "INTEGER NOT NULL DEFAULT 0", logger);
        await EnsureSqliteColumnAsync(connection, "Employees", "LastModifiedBy", "TEXT NULL", logger);
        await EnsureSqliteColumnAsync(connection, "Employees", "LastModifiedAt", "TEXT NULL", logger);
        await EnsureSqliteColumnAsync(connection, "Employees", "IsActive", "INTEGER NOT NULL DEFAULT 1", logger);
        await EnsureSqliteColumnAsync(connection, "Tasks", "Version", "INTEGER NOT NULL DEFAULT 0", logger);
        await EnsureSqliteColumnAsync(connection, "Tasks", "LastModifiedBy", "TEXT NULL", logger);
        await EnsureSqliteColumnAsync(connection, "Tasks", "LastModifiedAt", "TEXT NULL", logger);
        await EnsureSqliteColumnAsync(connection, "Tasks", "RecurrenceGroupId", "TEXT NULL", logger);
        await EnsureSqliteTemplatesTableAsync(connection, logger);
    }

    private static async Task ApplySqlServerAsync(DbConnection connection, ILogger logger)
    {
        await EnsureSqlServerColumnAsync(connection, "Employees", "Version", "INT NOT NULL CONSTRAINT DF_Employees_Version DEFAULT 0", logger);
        await EnsureSqlServerColumnAsync(connection, "Employees", "LastModifiedBy", "NVARCHAR(200) NULL", logger);
        await EnsureSqlServerColumnAsync(connection, "Employees", "LastModifiedAt", "DATETIME2 NULL", logger);
        await EnsureSqlServerColumnAsync(connection, "Employees", "IsActive", "BIT NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT 1", logger);
        await EnsureSqlServerColumnAsync(connection, "Tasks", "Version", "INT NOT NULL CONSTRAINT DF_Tasks_Version DEFAULT 0", logger);
        await EnsureSqlServerColumnAsync(connection, "Tasks", "LastModifiedBy", "NVARCHAR(200) NULL", logger);
        await EnsureSqlServerColumnAsync(connection, "Tasks", "LastModifiedAt", "DATETIME2 NULL", logger);
        await EnsureSqlServerColumnAsync(connection, "Tasks", "RecurrenceGroupId", "UNIQUEIDENTIFIER NULL", logger);
        await EnsureSqlServerTemplatesTableAsync(connection, logger);
    }

    private static async Task EnsureSqliteColumnAsync(
        DbConnection connection,
        string table,
        string column,
        string sqlType,
        ILogger logger)
    {
        if (!await SqliteTableExistsAsync(connection, table))
            return;
        if (await SqliteColumnExistsAsync(connection, table, column))
            return;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType};";
        await cmd.ExecuteNonQueryAsync();
        logger.LogInformation("SQLite-Spalte nachgezogen: {Table}.{Column}", table, column);
    }

    private static async Task EnsureSqliteTemplatesTableAsync(DbConnection connection, ILogger logger)
    {
        if (await SqliteTableExistsAsync(connection, "ScheduleTemplates"))
            return;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "ScheduleTemplates" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ScheduleTemplates" PRIMARY KEY AUTOINCREMENT,
                "Description" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "Color" INTEGER NOT NULL,
                "IsFavorite" INTEGER NOT NULL,
                "DisplayOrder" INTEGER NOT NULL DEFAULT 0
            );
            """;
        await cmd.ExecuteNonQueryAsync();
        logger.LogInformation("Tabelle ScheduleTemplates angelegt");
    }

    private static async Task EnsureSqlServerColumnAsync(
        DbConnection connection,
        string table,
        string column,
        string sqlType,
        ILogger logger)
    {
        if (!await SqlServerTableExistsAsync(connection, table))
            return;
        if (await SqlServerColumnExistsAsync(connection, table, column))
            return;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE [{table}] ADD [{column}] {sqlType};";
        try
        {
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("SQL-Server-Spalte nachgezogen: {Table}.{Column}", table, column);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "SQL-Server-Spalte {Table}.{Column} konnte nicht ergänzt werden (Rechte?). Start geht weiter, wenn die Spalte schon existiert.",
                table,
                column);
        }
    }

    private static async Task EnsureSqlServerTemplatesTableAsync(DbConnection connection, ILogger logger)
    {
        if (await SqlServerTableExistsAsync(connection, "ScheduleTemplates"))
            return;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            IF OBJECT_ID(N'dbo.ScheduleTemplates', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ScheduleTemplates] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [Description] NVARCHAR(500) NOT NULL,
                    [Type] INT NOT NULL,
                    [Color] INT NOT NULL,
                    [IsFavorite] BIT NOT NULL,
                    [DisplayOrder] INT NOT NULL CONSTRAINT [DF_ScheduleTemplates_DisplayOrder] DEFAULT 0,
                    CONSTRAINT [PK_ScheduleTemplates] PRIMARY KEY ([Id])
                );
            END
            """;
        try
        {
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("SQL-Server-Tabelle ScheduleTemplates angelegt");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SQL-Server-Tabelle ScheduleTemplates konnte nicht angelegt werden (Rechte?).");
        }
    }

    private static async Task<bool> SqliteTableExistsAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = $name;";
        var param = cmd.CreateParameter();
        param.ParameterName = "$name";
        param.Value = tableName;
        cmd.Parameters.Add(param);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> SqliteColumnExistsAsync(DbConnection connection, string tableName, string columnName)
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

    private static async Task<bool> SqlServerTableExistsAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @name AND TABLE_SCHEMA IN (N'dbo', SCHEMA_NAME());
            """;
        var param = cmd.CreateParameter();
        param.ParameterName = "@name";
        param.Value = tableName;
        cmd.Parameters.Add(param);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> SqlServerColumnExistsAsync(DbConnection connection, string tableName, string columnName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @table AND COLUMN_NAME = @column;
            """;
        var tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@table";
        tableParam.Value = tableName;
        cmd.Parameters.Add(tableParam);
        var columnParam = cmd.CreateParameter();
        columnParam.ParameterName = "@column";
        columnParam.Value = columnName;
        cmd.Parameters.Add(columnParam);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }
}
