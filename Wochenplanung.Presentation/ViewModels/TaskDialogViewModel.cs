using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;

namespace Wochenplanung.Presentation.ViewModels;

public enum RecurrencePattern
{
    None,
    Weekly,
    Monthly
}

public enum SeriesEditScope
{
    ThisOnly,
    ThisAndFuture,
    EntireSeries
}

public partial class TaskDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private Employee? _selectedEmployee;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Now;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private TimeSpan? _startTime;

    [ObservableProperty]
    private TimeSpan? _endTime;

    [ObservableProperty]
    private string? _location;

    [ObservableProperty]
    private string? _project;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private TaskType _taskType = TaskType.Work;

    [ObservableProperty]
    private TaskColor _taskColor = TaskColor.Orange;

    [ObservableProperty]
    private bool _isMultiDay;

    [ObservableProperty]
    private List<Employee> _employees = new();

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private RecurrencePattern _recurrencePattern = RecurrencePattern.None;

    [ObservableProperty]
    private int _repeatCount = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSeries))]
    [NotifyPropertyChangedFor(nameof(ShowRecurrenceEditor))]
    private Guid? _recurrenceGroupId;

    [ObservableProperty]
    private SeriesEditScope _seriesEditScope = SeriesEditScope.ThisOnly;

    public bool HasSeries => RecurrenceGroupId.HasValue;
    public bool ShowRecurrenceEditor => !IsEditMode;

    [ObservableProperty]
    private string _lastChangedText = string.Empty;

    public WorkTask? Task { get; set; }

    public TaskDialogViewModel(List<Employee> employees, WorkTask? task = null, WeekInfo? week = null)
    {
        Employees = employees;
        if (employees.Count > 0)
            SelectedEmployee = employees[0];
        
        if (week != null)
        {
            StartDate = week.StartDate;
        }

        if (task != null)
        {
            Task = task;
            SelectedEmployee = employees.FirstOrDefault(e => e.Id == task.EmployeeId);
            Description = task.Description;
            StartDate = task.StartDate;
            EndDate = task.EndDate;
            StartTime = task.StartTime;
            EndTime = task.EndTime;
            Location = task.Location;
            Project = task.Project;
            Notes = task.Notes;
            TaskType = task.Type;
            TaskColor = task.Color;
            RecurrenceGroupId = task.RecurrenceGroupId;
            IsMultiDay = task.IsMultiDay;
            IsEditMode = true;
            LastChangedText = Wochenplanung.Presentation.Services.TaskAuditText.FormatDialogLine(task.LastModifiedBy, task.LastModifiedAt);
        }
    }

    partial void OnIsMultiDayChanged(bool value)
    {
        if (!value)
        {
            EndDate = null;
        }
        else if (!EndDate.HasValue)
        {
            EndDate = StartDate;
        }
    }

    partial void OnStartDateChanged(DateTime value)
    {
        if (IsMultiDay && EndDate.HasValue && EndDate.Value.Date < value.Date)
        {
            EndDate = value;
        }
    }

    partial void OnEndDateChanged(DateTime? value)
    {
        if (IsMultiDay && value.HasValue && value.Value.Date < StartDate.Date)
        {
            EndDate = StartDate;
        }
    }

    public WorkTask GetTask()
    {
        if (Task != null)
        {
            Task.EmployeeId = SelectedEmployee?.Id ?? 0;
            Task.Description = Description;
            Task.StartDate = StartDate;
            Task.EndDate = IsMultiDay ? EndDate : null;
            Task.StartTime = StartTime;
            Task.EndTime = EndTime;
            Task.Location = Location;
            Task.Project = Project;
            Task.Notes = Notes;
            Task.Type = TaskType;
            Task.Color = TaskColor;
            Task.RecurrenceGroupId = RecurrenceGroupId;
            return Task;
        }

        return new WorkTask
        {
            EmployeeId = SelectedEmployee?.Id ?? 0,
            Description = Description,
            StartDate = StartDate,
            EndDate = IsMultiDay ? EndDate : null,
            StartTime = StartTime,
            EndTime = EndTime,
            Location = Location,
            Project = Project,
            Notes = Notes,
            Type = TaskType,
            Color = TaskColor,
            RecurrenceGroupId = RecurrenceGroupId
        };
    }

    public List<WorkTask> GetTasksToCreate()
    {
        var baseTask = GetTask();
        if (IsEditMode || RecurrencePattern == RecurrencePattern.None || RepeatCount <= 1)
            return new List<WorkTask> { baseTask };

        var tasks = new List<WorkTask>();
        var safeRepeatCount = Math.Clamp(RepeatCount, 1, 52);
        var groupId = Guid.NewGuid();
        for (int i = 0; i < safeRepeatCount; i++)
        {
            var copy = new WorkTask
            {
                EmployeeId = baseTask.EmployeeId,
                Description = baseTask.Description,
                StartDate = ShiftDate(baseTask.StartDate, i),
                EndDate = baseTask.EndDate.HasValue ? ShiftDate(baseTask.EndDate.Value, i) : null,
                StartTime = baseTask.StartTime,
                EndTime = baseTask.EndTime,
                Location = baseTask.Location,
                Project = baseTask.Project,
                Notes = baseTask.Notes,
                Type = baseTask.Type,
                Color = baseTask.Color,
                RecurrenceGroupId = groupId
            };
            tasks.Add(copy);
        }

        return tasks;
    }

    private DateTime ShiftDate(DateTime date, int index)
    {
        return RecurrencePattern switch
        {
            RecurrencePattern.Weekly => date.AddDays(index * 7),
            RecurrencePattern.Monthly => date.AddMonths(index),
            _ => date
        };
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // Speichern wird vom Dialog gehandhabt
    }

    private bool CanSave()
    {
        return SelectedEmployee != null && !string.IsNullOrWhiteSpace(Description);
    }
}
