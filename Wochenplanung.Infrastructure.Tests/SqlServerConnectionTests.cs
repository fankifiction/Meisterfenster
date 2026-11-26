using Wochenplanung.Infrastructure.Data;
using Xunit;

namespace Wochenplanung.Infrastructure.Tests;

public sealed class SqlServerConnectionTests
{
    [Fact]
    public void NormalizeDataSource_PrefixesTcpOnce()
    {
        Assert.Equal(@"tcp:BUERO\SQLEXPRESS", SqlServerConnection.NormalizeDataSource(@"BUERO\SQLEXPRESS"));
        Assert.Equal(@"tcp:BUERO\SQLEXPRESS", SqlServerConnection.NormalizeDataSource(@"tcp:BUERO\SQLEXPRESS"));
        Assert.Equal("tcp:192.168.1.50,1433", SqlServerConnection.NormalizeDataSource("192.168.1.50,1433"));
    }

    [Fact]
    public void Build_UsesShortTimeoutAndNoRetries()
    {
        var connection = SqlServerConnection.Build(@"BUERO\SQLEXPRESS", "Wochenplanung", true, null, null);
        Assert.Contains("Connect Timeout=8", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Connect Retry Count=0", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"tcp:BUERO\SQLEXPRESS", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Integrated Security=True", connection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_SqlLogin_DoesNotUseWindowsAuth()
    {
        var connection = SqlServerConnection.Build("BUEROSERVER", "Wochenplanung", false, "planuser", "secret;pass");
        Assert.Contains("User ID=planuser", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret", connection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Integrated Security=True", connection, StringComparison.OrdinalIgnoreCase);
    }
}
