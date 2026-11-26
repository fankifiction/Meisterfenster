namespace Wochenplanung.Domain.Entities;

public class ScheduleTemplate
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public TaskType Type { get; set; } = TaskType.Work;
    public TaskColor Color { get; set; } = TaskColor.Orange;
    public bool IsFavorite { get; set; }
    public int DisplayOrder { get; set; }
}
