namespace Wochenplanung.Domain.Entities;

public class WorkTask
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? Location { get; set; }
    public string? Project { get; set; }
    public string? Notes { get; set; }
    public TaskType Type { get; set; } = TaskType.Work;
    public TaskColor Color { get; set; } = TaskColor.Orange;
    /// <summary>Same value for every occurrence created together as a series.</summary>
    public Guid? RecurrenceGroupId { get; set; }
    /// <summary>Optimistic-concurrency token for shared-server edits.</summary>
    public int Version { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    public bool IsMultiDay => EndDate.HasValue && EndDate.Value.Date > StartDate.Date;
    public int DurationInDays => IsMultiDay 
        ? (EndDate!.Value.Date - StartDate.Date).Days + 1 
        : 1;

    public WorkTask Clone(bool includeId = true) =>
        new()
        {
            Id = includeId ? Id : 0,
            EmployeeId = EmployeeId,
            Description = Description,
            StartDate = StartDate,
            EndDate = EndDate,
            StartTime = StartTime,
            EndTime = EndTime,
            Location = Location,
            Project = Project,
            Notes = Notes,
            Type = Type,
            Color = Color,
            RecurrenceGroupId = RecurrenceGroupId,
            Version = includeId ? Version : 0,
            LastModifiedBy = includeId ? LastModifiedBy : null,
            LastModifiedAt = includeId ? LastModifiedAt : null
        };
}

public enum TaskType
{
    Work,
    Absence,
    School,
    Vacation,
    Other
}

public enum TaskColor
{
    Orange,
    Green,
    Blue,
    Beige,
    Yellow,
    Red
}
