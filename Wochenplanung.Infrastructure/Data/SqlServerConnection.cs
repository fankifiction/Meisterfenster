using Microsoft.Data.SqlClient;

namespace Wochenplanung.Infrastructure.Data;

public static class SqlServerConnection
{
    public const int ConnectTimeoutSeconds = 8;
    public const int StartupTimeoutSeconds = 15;

    public static string Build(
        string server,
        string database,
        bool windowsAuthentication,
        string? user,
        string? password)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = NormalizeDataSource(server),
            InitialCatalog = string.IsNullOrWhiteSpace(database) ? "Wochenplanung" : database.Trim(),
            TrustServerCertificate = true,
            Encrypt = true,
            ConnectTimeout = ConnectTimeoutSeconds,
            ConnectRetryCount = 0
        };

        if (windowsAuthentication)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = user ?? string.Empty;
            builder.Password = password ?? string.Empty;
        }

        return builder.ConnectionString;
    }

    public static string NormalizeDataSource(string server)
    {
        var trimmed = server.Trim();
        if (trimmed.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("np:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("lpc:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("admin:", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return "tcp:" + trimmed;
    }
}
