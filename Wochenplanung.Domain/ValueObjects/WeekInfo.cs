using System.Globalization;

namespace Wochenplanung.Domain.ValueObjects;

public class WeekInfo
{
    public int Year { get; set; }
    public int WeekNumber { get; set; }
    public DateTime StartDate { get; set; } // Montag (ISO 8601)
    public DateTime EndDate { get; set; } // Sonntag (ISO 8601, 7 Tage)

    public WeekInfo(int year, int weekNumber)
    {
        Year = year;
        WeekNumber = weekNumber;
        StartDate = GetMondayOfIsoWeek(year, weekNumber);
        EndDate = StartDate.AddDays(6); // Montag bis Sonntag (7 Tage)
    }

    public WeekInfo(DateTime date)
    {
        // ISO 8601: konsistente Berechnung für Jahr, KW und tatsächliche Daten
        Year = ISOWeek.GetYear(date);
        WeekNumber = ISOWeek.GetWeekOfYear(date);
        StartDate = GetMondayOfIsoWeek(Year, WeekNumber);
        EndDate = StartDate.AddDays(6);
    }

    public List<DateTime> GetDays(bool includeWeekend = true)
    {
        var count = includeWeekend ? 7 : 5;
        var days = new List<DateTime>(count);
        for (int i = 0; i < count; i++)
            days.Add(StartDate.AddDays(i));
        return days;
    }

    public WeekInfo GetPreviousWeek()
    {
        return new WeekInfo(StartDate.AddDays(-7));
    }

    public WeekInfo GetNextWeek()
    {
        return new WeekInfo(StartDate.AddDays(7));
    }

    /// <summary>
    /// Montag der angegebenen ISO-Kalenderwoche.
    /// ISO 8601: Woche 1 ist die Woche mit dem ersten Donnerstag des Jahres.
    /// </summary>
    private static DateTime GetMondayOfIsoWeek(int year, int weekNumber)
    {
        // 4. Januar liegt immer in KW 1 des entsprechenden Jahres
        var jan4 = new DateTime(year, 1, 4);
        var dayOfWeek = (int)jan4.DayOfWeek; // 0=So, 1=Mo, ...
        var mondayOfWeek1 = jan4.AddDays(-(dayOfWeek == 0 ? 6 : dayOfWeek - 1));
        return mondayOfWeek1.AddDays((weekNumber - 1) * 7);
    }
}
