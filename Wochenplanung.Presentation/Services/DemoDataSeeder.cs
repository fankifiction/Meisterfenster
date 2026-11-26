using Microsoft.EntityFrameworkCore;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Infrastructure.Data;
using Wochenplanung.Presentation.ViewModels;

namespace Wochenplanung.Presentation.Services;

/// <summary>
/// Testdaten analog zum Werkstatt-Screenshot (KW 06.–10.07.2026).
/// </summary>
public static class DemoDataSeeder
{
    public static readonly DateTime DemoWeekMonday = new(2026, 7, 6);
    public static readonly DateTime DemoWeekFriday = new(2026, 7, 10);

    public static async Task SeedAsync(ScheduleDbContext db, TaskTemplateStorage templateStorage)
    {
        await SeedTemplates(templateStorage);
        await SeedScheduleAsync(db);
    }

    private static async Task SeedTemplates(TaskTemplateStorage templateStorage)
    {
        var templates = new List<TaskTemplate>
        {
            T("Montage Pirracchio Kestenholz", TaskType.Work, TaskColor.Green, favorite: true),
            T("mit Roger", TaskType.Work, TaskColor.Green, favorite: true),
            T("MZG Matzendorf", TaskType.Work, TaskColor.Green, favorite: true),
            T("Montage Kaufmann Solothurn", TaskType.Work, TaskColor.Green),
            T("Pirracchio Mithilfe Glaseinsatz", TaskType.Work, TaskColor.Green),
            T("Masch. Arbeit S. 019+020", TaskType.Work, TaskColor.Orange, favorite: true),
            T("Putzen", TaskType.Work, TaskColor.Orange),
            T("Zumsteg einbauen, fertigmachen, glasen", TaskType.Work, TaskColor.Orange),
            T("Zumsteg glasen mit Dani", TaskType.Work, TaskColor.Orange),
            T("1000 LKW Kran für Glaseinsatz", TaskType.Work, TaskColor.Red, favorite: true),
            T("Rep. HST Herrengasse 11, Balsthal", TaskType.Work, TaskColor.Red),
            T("Glasersatz mit Katzentüre Stettler Wangen b. Olten", TaskType.Work, TaskColor.Red),
            T("Heiniger Dieter Offerte Glasbruch", TaskType.Other, TaskColor.Yellow),
            T("Evtl. Ledermann Sturzbleche (Storen) messen", TaskType.Other, TaskColor.Yellow),
            T("Schule", TaskType.School, TaskColor.Blue),
            T("Ferien", TaskType.Vacation, TaskColor.Beige),
            T("Krank", TaskType.Absence, TaskColor.Red),
            T("Sonstiges", TaskType.Other, TaskColor.Yellow),
        };

        await templateStorage.SaveAsync(TaskTemplateStorage.SortFavoritesFirst(templates));
    }

    private static async System.Threading.Tasks.Task SeedScheduleAsync(ScheduleDbContext db)
    {
        var mon = DemoWeekMonday;
        var tue = mon.AddDays(1);
        var wed = mon.AddDays(2);
        var thu = mon.AddDays(3);
        var fri = DemoWeekFriday;

        var names = new (string First, string Last, string Role, string Number)[]
        {
            ("Simon", "Wyss", "Lehrling", "1"),
            ("Jan", "Ledermann", "Monteur", "2"),
            ("Martin", "Bader", "Werkstatt", "3"),
            ("Lukas", "Steiner", "Monteur", "4"),
            ("Peter", "Keller", "Monteur", "5"),
            ("Thomas", "Brunner", "Werkstatt", "6"),
            ("Andreas", "Weber", "Werkstatt", "7"),
            ("Anna", "Meister", "Admin", "8"),
        };

        var previousDemoNames = new HashSet<(string First, string Last)>
        {
            ("Anna", "Meister"), ("Jan", "Ledermann"), ("Roger", "Meier"), ("Martin", "Bader"),
            ("Lukas", "Steiner"), ("Peter", "Keller"), ("Thomas", "Brunner"), ("Michael", "Fischer"),
            ("Andreas", "Weber"), ("Daniel", "Schmid"), ("Markus", "Huber"), ("Stefan", "Müller"),
            ("Christoph", "Gerber"), ("Fabian", "Roth"), ("Simon", "Wyss"), ("Nina", "Graf"),
            ("Sarah", "Frei"), ("Patrick", "Baumann")
        };
        foreach (var n in names)
            previousDemoNames.Add((n.First, n.Last));

        var oldDemo = await db.Employees.Include(e => e.Tasks).ToListAsync();
        var toRemove = oldDemo.Where(e => previousDemoNames.Contains((e.FirstName, e.LastName))).ToList();
        if (toRemove.Count > 0)
        {
            db.Employees.RemoveRange(toRemove);
            await db.SaveChangesAsync();
        }

        var employees = names.Select((n, i) => new Employee
        {
            EmployeeNumber = n.Number,
            FirstName = n.First,
            LastName = n.Last,
            DisplayOrder = i,
            Role = n.Role
        }).ToList();
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();

        var e = employees;
        var tasks = new List<WorkTask>
        {
            // Zeile 1: nur Donnerstag Putzen
            MakeTask(e[0].Id, "Putzen", thu, thu, TaskColor.Orange),

            // Zeile 2: Montage Pirracchio ganze Woche + rote Zusätze
            MakeTask(e[1].Id, "Montage Pirracchio Kestenholz", mon, fri, TaskColor.Green),
            MakeTask(e[1].Id, "1000 LKW Kran für Glaseinsatz", wed, wed, TaskColor.Red),
            MakeTask(e[1].Id, "Rep. HST Herrengasse 11, Balsthal", thu, thu, TaskColor.Red),

            // Zeile 3: Maschinenarbeit + Zusätze
            MakeTask(e[2].Id, "Masch. Arbeit S. 019+020", mon, fri, TaskColor.Orange),
            MakeTask(e[2].Id, "HST Kaufmann Solothurn", tue, tue, TaskColor.Orange),
            MakeTask(e[2].Id, "Zusch. neue S. 022", fri, fri, TaskColor.Orange),

            // Zeile 4
            MakeTask(e[3].Id, "mit Roger", mon, fri, TaskColor.Green),

            // Zeile 5
            MakeTask(e[4].Id, "MZG Matzendorf", mon, tue, TaskColor.Green),
            MakeTask(e[4].Id, "Montage Pirracchio Kestenholz, grosse Festverglasung", wed, wed, TaskColor.Green),
            MakeTask(e[4].Id, "Montage Kaufmann Solothurn", thu, fri, TaskColor.Green),
            MakeTask(e[4].Id, "Glasersatz mit Katzentüre Stettler Wangen b. Olten", fri, fri, TaskColor.Red),

            // Zeile 6
            MakeTask(e[5].Id, "EFH Gilgen FLG + RHM verleimen", mon, mon, TaskColor.Orange),
            MakeTask(e[5].Id, "Zumsteg Solothurn fertigmachen", tue, tue, TaskColor.Orange),
            MakeTask(e[5].Id, "Pirracchio Mithilfe Glaseinsatz", wed, wed, TaskColor.Green),
            MakeTask(e[5].Id, "Zumsteg glasen mit Dani", thu, fri, TaskColor.Orange),

            // Zeile 7
            MakeTask(e[6].Id, "Zumsteg (Eichen Fenster) + Kaufmann Oberfläche", mon, mon, TaskColor.Orange),
            MakeTask(e[6].Id, "Kaufmann einbauen, fertigmacher, glasen", tue, tue, TaskColor.Orange),
            MakeTask(e[6].Id, "Zumsteg einbauen, fertigmachen, glasen", wed, fri, TaskColor.Orange),
            MakeTask(e[6].Id, "Fluri Peter + Strähl Franz Dichtungen messen vorher Tel.", wed, wed, TaskColor.Red),

            // Zeile 8: Offerten
            MakeTask(e[7].Id, "Heiniger Dieter Offerte Glasbruch", tue, tue, TaskColor.Yellow, TaskType.Other),
            MakeTask(e[7].Id, "Evtl. Ledermann Sturzbleche (Storen) messen", wed, wed, TaskColor.Yellow, TaskType.Other),
        };

        db.Tasks.AddRange(tasks);
        await db.SaveChangesAsync();
    }

    private static TaskTemplate T(string description, TaskType type, TaskColor color, bool favorite = false) =>
        new() { Description = description, Type = type, Color = color, IsFavorite = favorite };

    private static WorkTask MakeTask(
        int employeeId,
        string description,
        DateTime start,
        DateTime end,
        TaskColor color,
        TaskType type = TaskType.Work) =>
        new()
        {
            EmployeeId = employeeId,
            Description = description,
            StartDate = start.Date,
            EndDate = end.Date > start.Date ? end.Date : null,
            Color = color,
            Type = type
        };
}
