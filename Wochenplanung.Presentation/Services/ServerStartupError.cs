namespace Wochenplanung.Presentation.Services;

public static class ServerStartupError
{
    public static bool IsSqlPermissionError(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return false;
        return detail.Contains("Cannot find the object", StringComparison.OrdinalIgnoreCase)
               || detail.Contains("do not have permissions", StringComparison.OrdinalIgnoreCase)
               || detail.Contains("permission was denied", StringComparison.OrdinalIgnoreCase);
    }

    public static string DescribeWindowsUser() =>
        $"{Environment.UserDomainName}\\{Environment.UserName}";

    public static bool UsesWindowsAuthentication(string connectionString) =>
        connectionString.Contains("Integrated Security=True", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("Trusted_Connection=True", StringComparison.OrdinalIgnoreCase);
}
