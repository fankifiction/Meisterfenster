using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Presentation.Services;

public static class ScheduleTableLayout
{
    public static string GetAlignmentKey(WorkTask task) => task.Description.Trim();

    /// <summary>
    /// Pro Mitarbeiter: gleiche Tätigkeit immer auf derselben Linie.
    /// Leere Linien (fehlende Mehrtages-/Mehrtages-Tätigkeit) werden bevorzugt
    /// mit Ein-Tages-Aufgaben gefüllt, die noch keine eigene Linie haben.
    /// </summary>
    public static Dictionary<DateTime, List<WorkTask?>> BuildEmployeeWeekLayout(
        Employee employee,
        IReadOnlyList<WorkTask> tasks,
        IReadOnlyList<DateTime> days)
    {
        var result = new Dictionary<DateTime, List<WorkTask?>>();
        if (days.Count == 0)
            return result;

        var dayKeys = days.Select(d => d.Date).ToList();
        var tasksByDay = new Dictionary<DateTime, List<WorkTask>>();
        var occurrenceDays = new Dictionary<string, HashSet<DateTime>>(StringComparer.OrdinalIgnoreCase);

        foreach (var day in dayKeys)
        {
            var dayTasks = GetTasksForDay(employee, day, tasks)
                .OrderBy(t => t.StartTime ?? TimeSpan.Zero)
                .ThenBy(t => t.Id)
                .ToList();
            tasksByDay[day] = dayTasks;

            foreach (var task in dayTasks)
            {
                var key = GetAlignmentKey(task);
                if (!occurrenceDays.TryGetValue(key, out var set))
                {
                    set = new HashSet<DateTime>();
                    occurrenceDays[key] = set;
                }
                set.Add(day);
            }
        }

        // Reihenfolge vom „vollsten“ Tag: so bleiben sichtbare Stapel konsistent
        // (gleiche Tätigkeit immer gleiche Zeile).
        var anchorKeys = BuildAnchorKeyOrder(dayKeys, tasksByDay, occurrenceDays);
        var anchorSlots = AssignAnchorSlots(anchorKeys, occurrenceDays);
        var anchorSlotCount = anchorSlots.Count == 0 ? 0 : anchorSlots.Values.Max() + 1;

        foreach (var day in dayKeys)
        {
            var dayTasks = tasksByDay[day];
            var slots = new List<WorkTask?>(Enumerable.Repeat<WorkTask?>(null, anchorSlotCount));

            var flex = new List<WorkTask>();
            foreach (var task in dayTasks)
            {
                var key = GetAlignmentKey(task);
                if (anchorSlots.TryGetValue(key, out var slotIndex))
                {
                    EnsureSlotCapacity(slots, slotIndex + 1);
                    if (slots[slotIndex] == null)
                        slots[slotIndex] = task;
                    else
                        flex.Add(task);
                }
                else
                {
                    flex.Add(task);
                }
            }

            var flexIndex = 0;
            for (var i = 0; i < slots.Count && flexIndex < flex.Count; i++)
            {
                if (slots[i] != null)
                    continue;
                slots[i] = flex[flexIndex++];
            }

            while (flexIndex < flex.Count)
                slots.Add(flex[flexIndex++]);

            result[day] = CompactTrailingEmptySlots(slots);
        }

        var maxCount = result.Values.Count == 0 ? 0 : result.Values.Max(v => v.Count);
        if (maxCount == 0)
        {
            foreach (var day in dayKeys)
                result[day] = new List<WorkTask?>();
            return result;
        }

        foreach (var day in dayKeys)
        {
            var list = result[day];
            while (list.Count < maxCount)
                list.Add(null);
        }

        return result;
    }

    /// <summary>
    /// Export: leere Ausrichtungs-Löcher entfernen, Aufgaben pro Tag nach oben packen.
    /// Im Plan bleiben die Löcher (gleiche Tätigkeit = gleiche Linie).
    /// </summary>
    public static Dictionary<DateTime, List<WorkTask?>> PackLayoutForExport(
        Dictionary<DateTime, List<WorkTask?>> layout)
    {
        var packed = new Dictionary<DateTime, List<WorkTask?>>();
        foreach (var (day, slots) in layout)
            packed[day] = CompactEmptySlots(slots);

        var maxCount = packed.Count == 0 ? 0 : packed.Values.Max(v => v.Count);
        if (maxCount == 0)
            return packed;

        foreach (var day in packed.Keys.ToList())
        {
            var list = packed[day];
            while (list.Count < maxCount)
                list.Add(null);
        }

        return packed;
    }

    /// <summary>
    /// Abwärtskompatibel: Map nur noch über eine Tätigkeit→Reihenfolge (ohne Mitarbeiter-Kontext).
    /// Bevorzugt BuildEmployeeWeekLayout verwenden.
    /// </summary>
    public static Dictionary<string, int> BuildGlobalSlotMap(
        IReadOnlyList<Employee> employees,
        IReadOnlyList<WorkTask> tasks,
        IReadOnlyList<DateTime> days)
    {
        var slotMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var nextSlot = 0;

        foreach (var day in days)
        {
            foreach (var employee in employees)
            {
                foreach (var task in GetTasksForDay(employee, day, tasks).OrderBy(t => t.StartTime ?? TimeSpan.Zero))
                {
                    var key = GetAlignmentKey(task);
                    if (!slotMap.ContainsKey(key))
                        slotMap[key] = nextSlot++;
                }
            }
        }

        return slotMap;
    }

    public static int GetMaxSlotCount(Dictionary<string, int> slotMap) =>
        slotMap.Count == 0 ? 1 : Math.Max(1, slotMap.Values.Max() + 1);

    public static int GetMaxPackedSlotCount(
        Employee employee,
        IReadOnlyList<WorkTask> tasks,
        IReadOnlyList<DateTime> days,
        IReadOnlyDictionary<string, int>? slotMap = null)
    {
        var layout = BuildEmployeeWeekLayout(employee, tasks, days);
        if (layout.Count == 0)
            return 1;
        return Math.Max(1, layout.Values.Max(v => v.Count));
    }

    public static List<WorkTask?> OrderTasksIntoSlots(
        IReadOnlyList<WorkTask> dayTasks,
        IReadOnlyDictionary<string, int> slotMap)
    {
        if (dayTasks.Count == 0)
            return new List<WorkTask?>();

        if (slotMap.Count == 0)
            return dayTasks.Cast<WorkTask?>().ToList();

        var slots = new WorkTask?[slotMap.Values.Max() + 1];
        foreach (var task in dayTasks)
        {
            if (slotMap.TryGetValue(GetAlignmentKey(task), out var slotIndex))
                slots[slotIndex] = task;
        }

        return CompactEmptySlots(slots);
    }

    public static List<WorkTask?> CompactEmptySlots(IReadOnlyList<WorkTask?> slots)
    {
        var packed = new List<WorkTask?>(slots.Count);
        foreach (var slot in slots)
        {
            if (slot != null)
                packed.Add(slot);
        }
        return packed;
    }

    public static List<WorkTask?> CompactLeadingEmptySlots(List<WorkTask?> slots) =>
        CompactEmptySlots(slots);

    public static List<WorkTask?> TrimTrailingEmptySlots(List<WorkTask?> slots) =>
        CompactTrailingEmptySlots(slots);

    public static IEnumerable<WorkTask> GetTasksForDay(Employee employee, DateTime day, IReadOnlyList<WorkTask> tasks) =>
        tasks.Where(t => t.EmployeeId == employee.Id &&
                         t.StartDate.Date <= day.Date &&
                         (t.EndDate == null ? t.StartDate.Date == day.Date : t.EndDate.Value.Date >= day.Date));

    private static List<string> BuildAnchorKeyOrder(
        IReadOnlyList<DateTime> dayKeys,
        IReadOnlyDictionary<DateTime, List<WorkTask>> tasksByDay,
        IReadOnlyDictionary<string, HashSet<DateTime>> occurrenceDays)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var densestDays = dayKeys
            .OrderByDescending(d => tasksByDay[d].Count)
            .ThenBy(d => d)
            .ToList();

        foreach (var day in densestDays)
        {
            foreach (var task in tasksByDay[day])
            {
                var key = GetAlignmentKey(task);
                if (occurrenceDays[key].Count < 2)
                    continue;
                if (seen.Add(key))
                    ordered.Add(key);
            }
        }

        foreach (var key in occurrenceDays.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            if (occurrenceDays[key].Count < 2)
                continue;
            if (seen.Add(key))
                ordered.Add(key);
        }

        return ordered;
    }

    private static Dictionary<string, int> AssignAnchorSlots(
        IReadOnlyList<string> anchorKeys,
        IReadOnlyDictionary<string, HashSet<DateTime>> occurrenceDays)
    {
        var slotOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var slotOccupancy = new Dictionary<int, HashSet<DateTime>>();

        foreach (var key in anchorKeys)
        {
            var days = occurrenceDays[key];
            var slot = 0;
            while (true)
            {
                if (!slotOccupancy.TryGetValue(slot, out var occupied) || !occupied.Overlaps(days))
                    break;
                slot++;
            }

            slotOf[key] = slot;
            if (!slotOccupancy.TryGetValue(slot, out var set))
            {
                set = new HashSet<DateTime>();
                slotOccupancy[slot] = set;
            }
            foreach (var day in days)
                set.Add(day);
        }

        return slotOf;
    }

    private static void EnsureSlotCapacity(List<WorkTask?> slots, int count)
    {
        while (slots.Count < count)
            slots.Add(null);
    }

    private static List<WorkTask?> CompactTrailingEmptySlots(List<WorkTask?> slots)
    {
        while (slots.Count > 0 && slots[^1] == null)
            slots.RemoveAt(slots.Count - 1);
        return slots;
    }
}
