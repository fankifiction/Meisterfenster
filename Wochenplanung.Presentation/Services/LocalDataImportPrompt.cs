using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Windows;
using Wochenplanung.Infrastructure.Data;
using Wochenplanung.Presentation.Localization;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Services;

public static class LocalDataImportPrompt
{
    public static async Task<LocalDataMigrationResult> ImportIfNeededAsync(
        DatabaseOptions destination,
        ILogger? logger,
        bool promptUser)
    {
        logger ??= NullLogger.Instance;
        var sqlitePath = DatabaseSettingsStore.LocalSqlitePath;
        if (!File.Exists(sqlitePath))
            return new LocalDataMigrationResult { Status = LocalDataMigrationStatus.SourceMissing };

        var source = LocalDataMigrator.SqliteFromPath(sqlitePath);
        var inspect = await LocalDataMigrator.InspectAsync(source, destination, logger).ConfigureAwait(true);
        if (!inspect.CanCopy)
        {
            if (inspect.Status is LocalDataMigrationStatus.SourceEmpty
                or LocalDataMigrationStatus.SourceMissing
                or LocalDataMigrationStatus.DestinationNotEmpty)
                MarkPromptHandled();
            return inspect;
        }

        if (promptUser)
        {
            var answer = MessageBox.Show(
                string.Format(
                    UiText.MsgImportLocalDataQuestion,
                    inspect.Employees,
                    inspect.Tasks),
                UiText.CaptionImportLocalData,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                MarkPromptHandled();
                return inspect with { CanCopy = false };
            }
        }

        var result = await LocalDataMigrator.CopyAsync(source, destination, logger).ConfigureAwait(true);
        MarkPromptHandled();
        return result;
    }

    public static void ShowResult(LocalDataMigrationResult result)
    {
        if (result.Copied)
        {
            MessageBox.Show(
                string.Format(UiText.MsgImportLocalDataDone, result.Employees, result.Tasks, result.Templates),
                UiText.CaptionImportLocalData,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (result.Status == LocalDataMigrationStatus.DestinationNotEmpty)
        {
            MessageBox.Show(
                UiText.MsgImportLocalDataServerNotEmpty,
                UiText.CaptionImportLocalData,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (result.Status == LocalDataMigrationStatus.Failed)
        {
            MessageBox.Show(
                $"{UiText.MsgImportLocalDataFailed}\n{result.Error}",
                UiText.CaptionImportLocalData,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public static void MarkPromptHandled()
    {
        var settings = DatabaseSettingsStore.Load();
        if (settings.SkipLocalImportPrompt)
            return;
        settings.SkipLocalImportPrompt = true;
        DatabaseSettingsStore.Save(settings);
    }
}
