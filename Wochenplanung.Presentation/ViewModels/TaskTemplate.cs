using CommunityToolkit.Mvvm.ComponentModel;
using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Presentation.ViewModels;

/// <summary>Vorlage für eine Standard-Aufgabe (Drag &amp; Drop auf Plan).</summary>
public partial class TaskTemplate : ObservableObject
{
    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private TaskType _type = TaskType.Work;

    [ObservableProperty]
    private TaskColor _color = TaskColor.Orange;

    /// <summary>Favoriten erscheinen oben in der Vorlagen-Liste.</summary>
    [ObservableProperty]
    private bool _isFavorite;
}
