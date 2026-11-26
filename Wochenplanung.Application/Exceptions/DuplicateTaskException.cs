namespace Wochenplanung.Application.Exceptions;

public class DuplicateTaskException : Exception
{
    public string TaskDescription { get; }
    public DateTime Day { get; }

    public DuplicateTaskException(string taskDescription, DateTime day)
        : base($"Duplicate task '{taskDescription}' on {day:yyyy-MM-dd}")
    {
        TaskDescription = taskDescription;
        Day = day;
    }
}
