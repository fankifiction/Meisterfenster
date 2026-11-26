using Wochenplanung.Presentation.Services;
using Wochenplanung.Presentation.ViewModels;
using Xunit;

namespace Wochenplanung.Presentation.Tests;

public sealed class ExportRangeCalculatorTests
{
    [Fact]
    public void GetDefaultRange_UsesCurrentView()
    {
        var weekStart = new DateTime(2026, 7, 6);
        var weekEnd = new DateTime(2026, 7, 12);
        var monthStart = new DateTime(2026, 7, 1);
        var selected = new DateTime(2026, 7, 9);

        Assert.Equal((selected, selected), ExportRangeCalculator.GetDefaultRange(ScheduleViewMode.Day, selected, monthStart, weekStart, weekEnd));
        Assert.Equal((weekStart, weekEnd), ExportRangeCalculator.GetDefaultRange(ScheduleViewMode.Week, selected, monthStart, weekStart, weekEnd));
        Assert.Equal((monthStart, new DateTime(2026, 7, 31)), ExportRangeCalculator.GetDefaultRange(ScheduleViewMode.Month, selected, monthStart, weekStart, weekEnd));
    }
}

public sealed class ScheduleColumnLayoutTests
{
    [Fact]
    public void MonthView_UsesFixedDayWidths()
    {
        Assert.False(ScheduleColumnLayout.UseFixedDayWidths(7));
        Assert.True(ScheduleColumnLayout.UseFixedDayWidths(31));
        Assert.Equal(ScheduleColumnLayout.MinDayColumnWidth, ScheduleColumnLayout.DayColumnMinWidth(31));
        Assert.True(ScheduleColumnLayout.DayColumnWidth(150, 31).IsAbsolute);
        Assert.True(ScheduleColumnLayout.DayColumnWidth(150, 7).IsStar);
    }
}

public sealed class SecretProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsOnWindows()
    {
        var protectedValue = SecretProtector.Protect("geheim-123");
        Assert.StartsWith("dpapi:", protectedValue);
        Assert.Equal("geheim-123", SecretProtector.Unprotect(protectedValue));
        Assert.Equal("plain", SecretProtector.Unprotect("plain"));
        Assert.Equal(string.Empty, SecretProtector.Unprotect("dpapi:not-valid-base64!!!"));
    }
}

public sealed class DialogOwnerTests
{
    [Fact]
    public void CanOwn_RejectsNull()
    {
        Assert.False(DialogOwner.CanOwn(null));
    }
}

public sealed class ServerStartupErrorTests
{
    [Fact]
    public void IsSqlPermissionError_DetectsMissingEmployeesTable()
    {
        Assert.True(ServerStartupError.IsSqlPermissionError(
            "Cannot find the object \"Employees\" because it does not exist or you do not have permissions."));
        Assert.False(ServerStartupError.IsSqlPermissionError("A network-related error occurred"));
    }

    [Fact]
    public void UsesWindowsAuthentication_DetectsIntegratedSecurity()
    {
        Assert.True(ServerStartupError.UsesWindowsAuthentication("Server=x;Integrated Security=True"));
        Assert.False(ServerStartupError.UsesWindowsAuthentication("Server=x;User ID=planuser;Password=secret"));
    }
}

public sealed class ExportSlotHeightTests
{
    [Fact]
    public void PadSlotHeights_KeepsEveryTaskRow_WhenNameCardNeedsMoreHeight()
    {
        var padded = ExportService.PadSlotHeightsToMinimum(new[] { 26.0, 26.0 }, 70);

        Assert.Equal(2, padded.Length);
        Assert.Equal(26, padded[0]);
        Assert.Equal(44, padded[1]);
    }

    [Fact]
    public void PadSlotHeights_LeavesTallerRowsUnchanged()
    {
        var source = new[] { 40.0, 50.0 };
        Assert.Same(source, ExportService.PadSlotHeightsToMinimum(source, 70));
    }

    [Fact]
    public void CountWrapLines_UsesEnoughRowsForLongGermanText()
    {
        var lines = ExportService.CountWrapLines("Glasersatz mit Katzentüre und Dichtungen ersetzen", 16);
        Assert.True(lines >= 3);
        Assert.True(ExportService.ExcelRowHeight(9, lines) > 40);
    }
}

public sealed class TaskAuditTextTests
{
    [Fact]
    public void FormatTooltip_FallsBackToDescription()
    {
        var task = new Wochenplanung.Domain.Entities.WorkTask { Description = "Montage" };
        Assert.Equal("Montage", TaskAuditText.FormatTooltip(task));
    }
}
