using Wochenplanung.Application.Interfaces;

namespace Wochenplanung.Infrastructure.Data;

public enum DatabaseProvider
{
    Sqlite,
    SqlServer
}

public sealed class DatabaseOptions : IDatabaseInfo
{
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.Sqlite;
    public string ConnectionString { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "Lokal";
    public bool CanWrite { get; set; } = true;

    public bool IsServer => Provider == DatabaseProvider.SqlServer;
}
