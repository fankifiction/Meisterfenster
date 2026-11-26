using System.IO;
using System.Windows;
using Wochenplanung.Presentation.Localization;

namespace Wochenplanung.Presentation.Services;

public sealed class ScheduleBackupService
{
    public string AppDataFolder => AppPaths.Root;

    public string LocalDatabasePath => Path.Combine(AppDataFolder, "schedule.db");

    public string DefaultBackupDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Wochenplan_Backups");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public string SuggestedFileName => $"Wochenplan_Backup_{DateTime.Now:yyyy-MM-dd_HHmm}.db";

    public BackupResult CopyLocalDatabase(string destinationPath)
    {
        if (!File.Exists(LocalDatabasePath))
            return BackupResult.Failed(UiText.Tr(
                "Es gibt keine lokale Datenbank zum Sichern.",
                "There is no local database to back up.",
                "Il n'y a pas de base locale à sauvegarder.",
                "Non c'è un database locale da salvare."));

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(LocalDatabasePath, destinationPath, overwrite: true);

        var templatesPath = Path.Combine(AppDataFolder, "task_templates.json");
        string? templatesCopy = null;
        if (File.Exists(templatesPath))
        {
            templatesCopy = Path.Combine(
                Path.GetDirectoryName(destinationPath)!,
                $"task_templates_{DateTime.Now:yyyy-MM-dd_HHmm}.json");
            File.Copy(templatesPath, templatesCopy, overwrite: true);
        }

        return BackupResult.Ok(destinationPath, templatesCopy);
    }

    public void RestoreLocalDatabase(string sourcePath)
    {
        Directory.CreateDirectory(AppDataFolder);
        File.Copy(sourcePath, LocalDatabasePath, overwrite: true);
    }
}

public sealed record BackupResult(bool Succeeded, string? DatabasePath, string? TemplatesPath, string? Error)
{
    public static BackupResult Ok(string databasePath, string? templatesPath) =>
        new(true, databasePath, templatesPath, null);

    public static BackupResult Failed(string error) =>
        new(false, null, null, error);
}
