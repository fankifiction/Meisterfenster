using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Presentation.ViewModels;

public partial class TaskTemplateDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _description = string.Empty;

    [ObservableProperty]
    private TaskType _taskType = TaskType.Work;

    [ObservableProperty]
    private TaskColor _taskColor = TaskColor.Orange;

    public TaskTemplateDialogViewModel(TaskTemplate? template = null)
    {
        if (template != null)
        {
            Description = template.Description;
            TaskType = template.Type;
            TaskColor = template.Color;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // Dialog schließt mit DialogResult true; GetTemplate() liefert das Ergebnis
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Description);

    public TaskTemplate GetTemplate(bool isFavorite = false)
    {
        return new TaskTemplate
        {
            Description = Description.Trim(),
            Type = TaskType,
            Color = TaskColor,
            IsFavorite = isFavorite
        };
    }
}
