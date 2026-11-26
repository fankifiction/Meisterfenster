namespace Wochenplanung.Application.Services;

public sealed record SwissHoliday(
    DateTime Date,
    string NameDe,
    string NameEn,
    string NameFr,
    string NameIt,
    bool IsFederal,
    bool IsSolothurn);

public static class SwissPublicHolidays
{
    public static IReadOnlyList<SwissHoliday> ForYear(int year)
    {
        if (year is < 1990 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), year, "Jahr muss zwischen 1990 und 2100 liegen.");

        var easter = EasterSunday(year);
        var list = new List<SwissHoliday>
        {
            Day(year, 1, 1, "Neujahr", "New Year's Day", "Nouvel An", "Capodanno", federal: true, solothurn: true),
            Day(year, 1, 2, "Berchtoldstag", "Berchtold's Day", "Saint-Berthold", "Giorno di Bertoldo", federal: false, solothurn: true),
            Offset(easter, -2, "Karfreitag", "Good Friday", "Vendredi saint", "Venerdì santo", federal: true, solothurn: true),
            Offset(easter, 1, "Ostermontag", "Easter Monday", "Lundi de Pâques", "Lunedì di Pasqua", federal: true, solothurn: true),
            Offset(easter, 39, "Auffahrt", "Ascension", "Ascension", "Ascensione", federal: true, solothurn: true),
            Offset(easter, 50, "Pfingstmontag", "Whit Monday", "Lundi de Pentecôte", "Lunedì di Pentecoste", federal: true, solothurn: true),
            Offset(easter, 60, "Fronleichnam", "Corpus Christi", "Fête-Dieu", "Corpus Domini", federal: false, solothurn: true),
            Day(year, 8, 1, "Nationalfeiertag", "Swiss National Day", "Fête nationale", "Festa nazionale", federal: true, solothurn: true),
            Day(year, 8, 15, "Mariä Himmelfahrt", "Assumption Day", "Assomption", "Assunzione", federal: false, solothurn: true),
            Day(year, 11, 1, "Allerheiligen", "All Saints' Day", "Toussaint", "Ognissanti", federal: false, solothurn: true),
            Day(year, 12, 8, "Mariä Empfängnis", "Immaculate Conception", "Immaculée Conception", "Immacolata Concezione", federal: false, solothurn: true),
            Day(year, 12, 25, "Weihnachten", "Christmas Day", "Noël", "Natale", federal: true, solothurn: true),
            Day(year, 12, 26, "Stephanstag", "St Stephen's Day", "Saint-Étienne", "Santo Stefano", federal: false, solothurn: true)
        };

        return list.OrderBy(h => h.Date).ToList();
    }

    public static IReadOnlyList<SwissHoliday> ForYearRange(int fromYear, int toYear)
    {
        if (toYear < fromYear)
            (fromYear, toYear) = (toYear, fromYear);

        return Enumerable.Range(fromYear, toYear - fromYear + 1)
            .SelectMany(ForYear)
            .OrderBy(h => h.Date)
            .ToList();
    }

    public static DateTime EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(year, month, day);
    }

    private static SwissHoliday Day(int year, int month, int day, string de, string en, string fr, string it, bool federal, bool solothurn) =>
        new(new DateTime(year, month, day), de, en, fr, it, federal, solothurn);

    private static SwissHoliday Offset(DateTime easter, int days, string de, string en, string fr, string it, bool federal, bool solothurn) =>
        new(easter.AddDays(days), de, en, fr, it, federal, solothurn);
}
