using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Services;

public sealed class CopiedTaskPayload
{
    public string Description { get; init; } = string.Empty;
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public string? Location { get; init; }
    public string? Project { get; init; }
    public string? Notes { get; init; }
    public TaskType Type { get; init; }
    public TaskColor Color { get; init; }
    public IReadOnlyList<(int StartOffset, int EndOffset)> Segments { get; init; } = Array.Empty<(int, int)>();

    public static CopiedTaskPayload From(WorkTask source, IReadOnlyList<(int StartOffset, int EndOffset)> segments) =>
        new()
        {
            Description = source.Description,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            Location = source.Location,
            Project = source.Project,
            Notes = source.Notes,
            Type = source.Type,
            Color = source.Color,
            Segments = segments
        };
}

public static class CopiedTaskPlanner
{
    public static IReadOnlyList<(int StartOffset, int EndOffset)> BuildSegments(
        DateTime sourceStart,
        DateTime sourceEnd,
        IEnumerable<DateTime>? selectedDays)
    {
        sourceStart = sourceStart.Date;
        sourceEnd = sourceEnd.Date;

        var days = (selectedDays ?? Enumerable.Empty<DateTime>())
            .Select(d => d.Date)
            .Where(d => d >= sourceStart && d <= sourceEnd)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (days.Count == 0)
            return new[] { (0, Math.Max(0, (sourceEnd - sourceStart).Days)) };

        var ranges = new List<(int StartOffset, int EndOffset)>();
        var minDay = days[0];
        var segStart = minDay;
        var prev = minDay;
        for (var i = 1; i < days.Count; i++)
        {
            var day = days[i];
            if ((day - prev).Days == 1)
            {
                prev = day;
                continue;
            }

            ranges.Add(((segStart - minDay).Days, (prev - minDay).Days));
            segStart = day;
            prev = day;
        }

        ranges.Add(((segStart - minDay).Days, (prev - minDay).Days));
        return ranges;
    }
}
