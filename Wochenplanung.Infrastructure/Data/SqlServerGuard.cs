using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wochenplanung.Infrastructure.Data;

/// <summary>
/// Keeps SQL Server tables in dbo (not the Windows user's default schema)
/// and probes whether the login may write.
/// </summary>
public static class SqlServerGuard
{
    private static readonly string[] Tables = ["Employees", "Tasks", "ScheduleTemplates", "__EFMigrationsHistory"];

    public static async Task ApplyAsync(ScheduleDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlServer())
            return;

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await SetDefaultSchemaDboAsync(connection, logger);
        await TransferUserSchemaTablesAsync(connection, logger);
    }

    public static async Task<bool> CanWriteAsync(ScheduleDbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlServer())
            return true;

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT CASE
                WHEN HAS_PERMS_BY_NAME(N'dbo.Tasks', N'OBJECT', N'INSERT') = 1
                 AND HAS_PERMS_BY_NAME(N'dbo.Tasks', N'OBJECT', N'UPDATE') = 1
                THEN 1
                WHEN HAS_PERMS_BY_NAME(N'dbo.Tasks', N'OBJECT', N'INSERT') IS NULL
                THEN 1
                ELSE 0
            END;
            """;
        try
        {
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        catch
        {
            return true;
        }
    }

    private static async Task SetDefaultSchemaDboAsync(DbConnection connection, ILogger logger)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            BEGIN TRY
                DECLARE @user sysname = USER_NAME();
                IF @user IS NOT NULL AND @user <> N'dbo' AND @user <> N'guest'
                BEGIN
                    DECLARE @sql nvarchar(400) = N'ALTER USER ' + QUOTENAME(@user) + N' WITH DEFAULT_SCHEMA = dbo';
                    EXEC (@sql);
                END
            END TRY
            BEGIN CATCH
                -- ALTER USER needs extra rights; tables are still transferred below.
            END CATCH
            """;
        try
        {
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("SQL-Standard-Schema auf dbo gesetzt (falls erlaubt).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DEFAULT_SCHEMA dbo konnte nicht gesetzt werden.");
        }
    }

    private static async Task TransferUserSchemaTablesAsync(DbConnection connection, ILogger logger)
    {
        await using var schemaCmd = connection.CreateCommand();
        schemaCmd.CommandText = "SELECT SCHEMA_NAME();";
        var current = (await schemaCmd.ExecuteScalarAsync())?.ToString();
        if (string.IsNullOrWhiteSpace(current) || string.Equals(current, "dbo", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var table in Tables)
        {
            await using var existsUser = connection.CreateCommand();
            existsUser.CommandText = """
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;
                """;
            Add(existsUser, "@schema", current);
            Add(existsUser, "@table", table);
            if (Convert.ToInt32(await existsUser.ExecuteScalarAsync()) == 0)
                continue;

            await using var existsDbo = connection.CreateCommand();
            existsDbo.CommandText = """
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = @table;
                """;
            Add(existsDbo, "@table", table);
            if (Convert.ToInt32(await existsDbo.ExecuteScalarAsync()) > 0)
            {
                logger.LogWarning(
                    "Tabelle {Table} existiert in Schema {Schema} und in dbo – Benutzer-Kopie wird nicht verschoben.",
                    table, current);
                continue;
            }

            await using var transfer = connection.CreateCommand();
            transfer.CommandText = $"ALTER SCHEMA dbo TRANSFER [{current.Replace("]", "]]")}].[{table}];";
            try
            {
                await transfer.ExecuteNonQueryAsync();
                logger.LogInformation("Tabelle {Schema}.{Table} nach dbo verschoben.", current, table);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tabelle {Schema}.{Table} konnte nicht nach dbo verschoben werden.", current, table);
            }
        }
    }

    private static void Add(DbCommand command, string name, string value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }
}
