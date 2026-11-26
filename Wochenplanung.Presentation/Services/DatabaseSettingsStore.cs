using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wochenplanung.Infrastructure.Data;

namespace Wochenplanung.Presentation.Services;

public sealed class DatabaseSettings
{
    public string Provider { get; set; } = "Sqlite";
    public string Server { get; set; } = "";
    public string Database { get; set; } = "Wochenplanung";
    public bool UseWindowsAuthentication { get; set; } = true;
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool SkipLocalImportPrompt { get; set; }

    /// <summary>True when a DPAPI-protected password could not be read on this Windows user.</summary>
    [JsonIgnore]
    public bool PasswordCouldNotBeRead { get; set; }
}

public static class DatabaseSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string FilePath
    {
        get
        {
            return AppPaths.Combine("database.json");
        }
    }

    public static string LocalSqlitePath
    {
        get
        {
            return AppPaths.Combine("schedule.db");
        }
    }

    public static string LocalDemoSqlitePath
    {
        get
        {
            return AppPaths.Combine("schedule-demo.db");
        }
    }

    public static DatabaseOptions ToLocalDemoOptions() =>
        new()
        {
            Provider = DatabaseProvider.Sqlite,
            ConnectionString = $"Data Source={LocalDemoSqlitePath}",
            DisplayName = "Demo (lokal)"
        };

    public static DatabaseSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new DatabaseSettings();
            var settings = JsonSerializer.Deserialize<DatabaseSettings>(File.ReadAllText(FilePath), JsonOptions)
                   ?? new DatabaseSettings();
            ApplyStoredPassword(settings);
            return settings;
        }
        catch
        {
            return new DatabaseSettings();
        }
    }

    public static void Save(DatabaseSettings settings)
    {
        var toStore = new DatabaseSettings
        {
            Provider = settings.Provider,
            Server = settings.Server,
            Database = settings.Database,
            UseWindowsAuthentication = settings.UseWindowsAuthentication,
            User = settings.User,
            Password = settings.UseWindowsAuthentication ? string.Empty : SecretProtector.Protect(settings.Password),
            SkipLocalImportPrompt = settings.SkipLocalImportPrompt
        };
        File.WriteAllText(FilePath, JsonSerializer.Serialize(toStore, JsonOptions));
    }

    public static void ApplyStoredPassword(DatabaseSettings settings)
    {
        var stored = settings.Password;
        if (string.IsNullOrEmpty(stored) || !SecretProtector.IsProtected(stored))
            return;

        var plain = SecretProtector.Unprotect(stored);
        settings.PasswordCouldNotBeRead = string.IsNullOrEmpty(plain);
        settings.Password = plain;
    }

    public static DatabaseOptions ToOptions(DatabaseSettings settings)
    {
        var useServer = string.Equals(settings.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(settings.Server);

        if (!useServer)
        {
            return new DatabaseOptions
            {
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = $"Data Source={LocalSqlitePath}",
                DisplayName = "Lokal"
            };
        }

        var server = settings.Server.Trim();
        var database = string.IsNullOrWhiteSpace(settings.Database) ? "Wochenplanung" : settings.Database.Trim();
        var password = SecretProtector.Unprotect(settings.Password);
        var connection = SqlServerConnection.Build(
            server,
            database,
            settings.UseWindowsAuthentication,
            settings.User,
            password);

        return new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = connection,
            DisplayName = server
        };
    }
}
