using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Presentation.Localization;
using Wochenplanung.Presentation.ViewModels;
using MessageBox = Wochenplanung.Presentation.Services.ModernMessageBox;

namespace Wochenplanung.Presentation.Services;

/// <summary>
/// Server mode: database is the only source of truth. JSON is a local emergency copy.
/// Local SQLite: database plus JSON backup.
/// </summary>
public class TaskTemplateStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _filePath;
    private readonly IScheduleTemplateRepository? _repository;
    private readonly bool _serverMode;

    public TaskTemplateStorage(IScheduleTemplateRepository? repository = null, IDatabaseInfo? database = null)
    {
        _repository = repository;
        _serverMode = database?.IsServer == true;
        _filePath = AppPaths.Combine("task_templates.json");
    }

    public Task<List<TaskTemplate>> LoadAsync() => LoadCoreAsync();

    public Task SaveAsync(IEnumerable<TaskTemplate> templates) => SaveCoreAsync(templates.ToList());

    private async Task<List<TaskTemplate>> LoadCoreAsync()
    {
        if (_repository != null)
        {
            try
            {
                var fromDb = await _repository.GetAllAsync().ConfigureAwait(false);
                if (fromDb.Count > 0)
                    return SortFavoritesFirst(fromDb.Select(ToView).ToList());

                var seed = _serverMode ? GetDefaultTemplates() : LoadFromJsonOrDefault();
                if (seed.Count > 0)
                    await SaveToDatabaseAsync(seed).ConfigureAwait(false);
                if (!_serverMode)
                    SaveToJson(seed);
                return SortFavoritesFirst(seed);
            }
            catch
            {
                if (_serverMode)
                    return SortFavoritesFirst(GetDefaultTemplates());
                return LoadFromJsonOrDefault();
            }
        }

        return LoadFromJsonOrDefault();
    }

    private async Task SaveCoreAsync(List<TaskTemplate> list)
    {
        if (!_serverMode)
            SaveToJson(list);
        else
            SaveToJson(list); // emergency copy only, never the source of truth on load
        await SaveToDatabaseAsync(list).ConfigureAwait(false);
    }

    private async Task SaveToDatabaseAsync(IReadOnlyList<TaskTemplate> templates)
    {
        if (_repository == null)
            return;
        try
        {
            var entities = templates.Select((t, i) => new ScheduleTemplate
            {
                Description = t.Description,
                Type = t.Type,
                Color = t.Color,
                IsFavorite = t.IsFavorite,
                DisplayOrder = i
            }).ToList();
            await _repository.MergeAllAsync(entities).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{UiText.Tr("Vorlagen konnten nicht in der Datenbank gespeichert werden", "Templates could not be saved to the database", "Les modèles n'ont pas pu être enregistrés dans la base", "I modelli non possono essere salvati nel database")}:\n{ex.Message}",
                UiText.Tr("Speicherfehler", "Save error", "Erreur d'enregistrement", "Errore di salvataggio"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private List<TaskTemplate> LoadFromJsonOrDefault()
    {
        if (!File.Exists(_filePath))
            return SortFavoritesFirst(GetDefaultTemplates());
        try
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<TaskTemplateDto>>(json, Options);
            if (list == null || list.Count == 0)
                return SortFavoritesFirst(GetDefaultTemplates());
            var templates = list.Select(d => new TaskTemplate
            {
                Description = d.Description ?? "",
                Type = d.Type,
                Color = d.Color,
                IsFavorite = d.IsFavorite
            }).ToList();
            return SortFavoritesFirst(templates);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{UiText.Tr("Vorlagen konnten nicht geladen werden", "Templates could not be loaded", "Les modèles n'ont pas pu être chargés", "I modelli non possono essere caricati")}:\n{ex.Message}\n\n{UiText.Tr("Standard-Vorlagen werden verwendet.", "Default templates will be used.", "Les modèles par défaut seront utilisés.", "Verranno usati i modelli predefiniti.")}",
                UiText.Tr("Ladefehler", "Load error", "Erreur de chargement", "Errore di caricamento"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return SortFavoritesFirst(GetDefaultTemplates());
        }
    }

    private void SaveToJson(IEnumerable<TaskTemplate> templates)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var list = templates.Select(t => new TaskTemplateDto
            {
                Description = t.Description,
                Type = t.Type,
                Color = t.Color,
                IsFavorite = t.IsFavorite
            }).ToList();
            File.WriteAllText(_filePath, JsonSerializer.Serialize(list, Options));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{UiText.Tr("Vorlagen konnten nicht gespeichert werden", "Templates could not be saved", "Les modèles n'ont pas pu être enregistrés", "I modelli non possono essere salvati")}:\n{ex.Message}\n\n{UiText.Tr("Pfad", "Path", "Chemin", "Percorso")}: {_filePath}",
                UiText.Tr("Speicherfehler", "Save error", "Erreur d'enregistrement", "Errore di salvataggio"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    public static List<TaskTemplate> SortFavoritesFirst(IEnumerable<TaskTemplate> templates)
    {
        var list = templates.ToList();
        return list
            .Where(t => t.IsFavorite)
            .Concat(list.Where(t => !t.IsFavorite))
            .ToList();
    }

    private static TaskTemplate ToView(ScheduleTemplate t) => new()
    {
        Description = t.Description,
        Type = t.Type,
        Color = t.Color,
        IsFavorite = t.IsFavorite
    };

    public static List<TaskTemplate> GetDefaultTemplates()
    {
        return new List<TaskTemplate>
        {
            new() { Description = "Schule", Type = TaskType.School, Color = TaskColor.Blue },
            new() { Description = "Ferien", Type = TaskType.Vacation, Color = TaskColor.Beige },
            new() { Description = "Krank", Type = TaskType.Absence, Color = TaskColor.Red },
            new() { Description = "Fortbildung", Type = TaskType.School, Color = TaskColor.Orange },
            new() { Description = "Sonstiges", Type = TaskType.Other, Color = TaskColor.Yellow }
        };
    }

    private class TaskTemplateDto
    {
        public string Description { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public TaskColor Color { get; set; }
        public bool IsFavorite { get; set; }
    }
}
