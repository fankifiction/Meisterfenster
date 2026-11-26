using System.IO;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.Services;
using Wochenplanung.Presentation.ViewModels;
using Xunit;

namespace Wochenplanung.Presentation.Tests;

public sealed class TaskHistoryTests
{
    [Fact]
    public async Task UndoRedo_RestoresValuesAndIgnoresPushWhileApplying()
    {
        var history = new TaskHistory();
        var value = 1;
        history.Push(
            undoAsync: () => { value = 0; return Task.CompletedTask; },
            redoAsync: () => { value = 1; return Task.CompletedTask; });

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        await history.UndoAsync();
        Assert.Equal(0, value);
        Assert.True(history.CanRedo);

        await history.RedoAsync();
        Assert.Equal(1, value);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public async Task Push_DuringUndo_IsIgnored()
    {
        var history = new TaskHistory();
        var extraPushed = false;
        history.Push(
            undoAsync: () =>
            {
                history.Push(() => Task.CompletedTask, () => Task.CompletedTask);
                extraPushed = history.CanUndo;
                return Task.CompletedTask;
            },
            redoAsync: () => Task.CompletedTask);

        await history.UndoAsync();

        Assert.False(extraPushed);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
    }
}

public sealed class ScheduleHighlightTests
{
    [Fact]
    public void CellColumnAndRowModes_MatchExpectedTargets()
    {
        var day = new DateTime(2026, 7, 8);
        Assert.True(ScheduleHighlight.IsCellHighlighted(ScheduleHighlightMode.Cell, 3, day, null, null, 3, day));
        Assert.False(ScheduleHighlight.IsCellHighlighted(ScheduleHighlightMode.Cell, 3, day, null, null, 4, day));
        Assert.True(ScheduleHighlight.IsColumnHeaderHighlighted(ScheduleHighlightMode.Column, day, day));
        Assert.True(ScheduleHighlight.IsEmployeeNameHighlighted(ScheduleHighlightMode.Row, 9, 9));
        Assert.False(ScheduleHighlight.IsEmployeeNameHighlighted(ScheduleHighlightMode.None, 9, 9));
    }
}

public sealed class ScheduleGridLayoutTests
{
    [Fact]
    public void ColumnIndexes_FollowGripEmployeeSplitterPattern()
    {
        Assert.Equal(3, ScheduleGridLayout.DayContentColumn(0));
        Assert.Equal(5, ScheduleGridLayout.DayContentColumn(1));
        Assert.Equal(16, ScheduleGridLayout.TrailingSplitterColumn(7));
        Assert.Equal(17, ScheduleGridLayout.TrailingFillerColumn(7));
        Assert.Equal(18, ScheduleGridLayout.TotalColumnCount(7));
        Assert.True(ScheduleGridLayout.IsSplitterColumn(2));
        Assert.False(ScheduleGridLayout.IsSplitterColumn(3));
        Assert.True(ScheduleGridLayout.IsDayContentColumn(3, 7));
        Assert.True(ScheduleGridLayout.IsDayContentColumn(15, 7));
        Assert.False(ScheduleGridLayout.IsDayContentColumn(1, 7));
        Assert.False(ScheduleGridLayout.IsDayContentColumn(16, 7));
        Assert.True(ScheduleGridLayout.IsFillerColumn(17, 7));
        Assert.Equal(ScheduleGridLayout.GripColumnWidth, ScheduleGridLayout.GetDefaultColumnWidth(0));
        Assert.Equal(ScheduleGridLayout.DefaultEmployeeColumnWidth, ScheduleGridLayout.GetDefaultColumnWidth(1));
        Assert.Equal(ScheduleGridLayout.DefaultDayColumnWidth, ScheduleGridLayout.GetDefaultColumnWidth(3));
        Assert.Equal(0, ScheduleGridLayout.GetDefaultColumnWidth(17, 7));
    }
}

public sealed class SoftWrapTextTests
{
    [Fact]
    public void InsertHints_BreaksLongWordsAndSeparators()
    {
        var wrapped = SoftWrapText.InsertHints("einhängen+Werbetafel");
        Assert.Contains('\u200B', wrapped);
        Assert.Contains("Werbetafel", wrapped);
        Assert.Contains("+", wrapped);
    }
}

public sealed class UiCatalogTests
{
    [Fact]
    public void Tr_UsesCurrentUiCulture()
    {
        var original = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            Assert.Equal("Cancel", UiText.Tr("Abbrechen", "Cancel", "Annuler", "Annulla"));

            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de-CH");
            Assert.Equal("Abbrechen", UiText.Tr("Abbrechen", "Cancel", "Annuler", "Annulla"));
            Assert.Equal("Gelb", UiText.TaskColorName(Wochenplanung.Domain.Entities.TaskColor.Yellow));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = original;
        }
    }
}

public sealed class UiLayoutSettingsTests
{
    [Fact]
    public void ClampTemplatePanelWidth_KeepsValueInsideBounds()
    {
        Assert.Equal(UiLayoutSettings.MinTemplatePanelWidth, UiLayoutSettings.ClampTemplatePanelWidth(10));
        Assert.Equal(UiLayoutSettings.MaxTemplatePanelWidth, UiLayoutSettings.ClampTemplatePanelWidth(5000));
        Assert.Equal(240, UiLayoutSettings.ClampTemplatePanelWidth(240));
    }
}

public sealed class DatabaseSettingsStoreTests
{
    [Fact]
    public void ToOptions_LocalMode_UsesSqliteFile()
    {
        var options = DatabaseSettingsStore.ToOptions(new DatabaseSettings { Provider = "Sqlite" });
        Assert.Equal(Wochenplanung.Infrastructure.Data.DatabaseProvider.Sqlite, options.Provider);
        Assert.False(options.IsServer);
        Assert.Contains("schedule.db", options.ConnectionString);
    }

    [Fact]
    public void ToOptions_ServerMode_UsesSqlServer()
    {
        var options = DatabaseSettingsStore.ToOptions(new DatabaseSettings
        {
            Provider = "SqlServer",
            Server = @"BUERO\SQLEXPRESS",
            Database = "Wochenplanung",
            UseWindowsAuthentication = true
        });
        Assert.True(options.IsServer);
        Assert.Contains(@"tcp:BUERO\SQLEXPRESS", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            options.ConnectionString.Contains("Integrated Security=True", StringComparison.OrdinalIgnoreCase)
            || options.ConnectionString.Contains("Trusted_Connection=True", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Connect Retry Count=0", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(@"BUERO\SQLEXPRESS", options.DisplayName);
    }

    [Fact]
    public void ToOptions_SqlLogin_IncludesUser()
    {
        var options = DatabaseSettingsStore.ToOptions(new DatabaseSettings
        {
            Provider = "SqlServer",
            Server = "BUEROSERVER",
            Database = "Wochenplanung",
            UseWindowsAuthentication = false,
            User = "planuser",
            Password = "secret"
        });
        Assert.True(options.IsServer);
        Assert.Contains("User ID=planuser", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Integrated Security=True", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Trusted_Connection=True", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tcp:BUEROSERVER", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToOptions_ServerWithoutName_FallsBackToSqlite()
    {
        var options = DatabaseSettingsStore.ToOptions(new DatabaseSettings
        {
            Provider = "SqlServer",
            Server = "  "
        });
        Assert.False(options.IsServer);
        Assert.Equal(Wochenplanung.Infrastructure.Data.DatabaseProvider.Sqlite, options.Provider);
    }

    [Fact]
    public void ToOptions_DoesNotDoubleTcpPrefix()
    {
        var options = DatabaseSettingsStore.ToOptions(new DatabaseSettings
        {
            Provider = "SqlServer",
            Server = @"tcp:BUERO\SQLEXPRESS",
            UseWindowsAuthentication = true
        });
        Assert.Contains(@"tcp:BUERO\SQLEXPRESS", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"tcp:tcp:", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyStoredPassword_MarksUnreadableDpapiPayload()
    {
        var settings = new DatabaseSettings { Password = "dpapi:%%%" };
        DatabaseSettingsStore.ApplyStoredPassword(settings);
        Assert.True(settings.PasswordCouldNotBeRead);
        Assert.Equal(string.Empty, settings.Password);
    }
}

public sealed class AppLaunchLocationTests
{
    [Fact]
    public void IsNetworkPath_DetectsUnc()
    {
        Assert.True(AppLaunchLocation.IsNetworkPath(@"\\buero\share\Wochenplanung.Presentation.exe"));
        Assert.True(AppLaunchLocation.IsNetworkPath("//buero/share/Wochenplanung.Presentation.exe"));
        Assert.False(AppLaunchLocation.IsNetworkPath(@"C:\Programme\Meisterfenster\Wochenplanung.Presentation.exe"));
        Assert.False(AppLaunchLocation.IsNetworkPath(""));
    }

    [Fact]
    public void IsLocalAppCopy_MatchesAppDataFolder()
    {
        var localExe = Path.Combine(AppLaunchLocation.LocalAppFolder, "Wochenplanung.Presentation.exe");
        Assert.True(AppLaunchLocation.IsLocalAppCopy(localExe));
        Assert.False(AppLaunchLocation.IsLocalAppCopy(@"\\buero\share\Wochenplanung.Presentation.exe"));
    }

    [Fact]
    public void CopyBundle_CopiesNewerFilesOnly()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), "mf-src-" + Guid.NewGuid().ToString("N"));
        var destDir = Path.Combine(Path.GetTempPath(), "mf-dst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDir);
        try
        {
            var sourceExe = Path.Combine(sourceDir, "Wochenplanung.Presentation.exe");
            File.WriteAllText(sourceExe, "exe-v1");
            File.WriteAllText(Path.Combine(sourceDir, "VERSION.txt"), "1");

            var copied = AppLaunchLocation.CopyBundle(sourceExe, destDir);
            Assert.True(File.Exists(copied));
            Assert.Equal("exe-v1", File.ReadAllText(copied));

            var destTime = File.GetLastWriteTimeUtc(copied);
            AppLaunchLocation.CopyBundle(sourceExe, destDir);
            Assert.Equal(destTime, File.GetLastWriteTimeUtc(copied));

            File.WriteAllText(sourceExe, "exe-v2");
            AppLaunchLocation.CopyBundle(sourceExe, destDir);
            Assert.Equal("exe-v2", File.ReadAllText(copied));
        }
        finally
        {
            try { Directory.Delete(sourceDir, true); } catch { }
            try { Directory.Delete(destDir, true); } catch { }
        }
    }
}

public sealed class TimeTextTests
{
    [Theory]
    [InlineData("10:00", 10, 0)]
    [InlineData("9:30", 9, 30)]
    [InlineData("09.30", 9, 30)]
    [InlineData("1000", 10, 0)]
    [InlineData("930", 9, 30)]
    [InlineData("9", 9, 0)]
    [InlineData("10", 10, 0)]
    [InlineData("  0830  ", 8, 30)]
    public void TryParse_AcceptsCommonWorkshopInputs(string input, int hour, int minute)
    {
        Assert.True(TimeText.TryParse(input, out var time));
        Assert.Equal(new TimeSpan(hour, minute, 0), time);
    }

    [Fact]
    public void TryParse_Empty_MeansNoTime()
    {
        Assert.True(TimeText.TryParse("  ", out var time));
        Assert.Null(time);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("1099")]
    [InlineData("abc")]
    public void TryParse_RejectsInvalidValues(string input)
    {
        Assert.False(TimeText.TryParse(input, out _));
    }
}
