using System.Globalization;

namespace Wochenplanung.Presentation.Services;

/// <summary>
/// Parst Startzeiten aus der Eingabe: 10:00, 10.00, 1000, 930, 9.
/// </summary>
public static class TimeText
{
    public static string Format(TimeSpan time) => time.ToString(@"hh\:mm");

    public static bool TryParse(string? text, out TimeSpan? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var raw = text.Trim();
        var formats = new[] { @"h\:mm", @"hh\:mm", @"h\.mm", @"hh\.mm", @"h\,mm", @"hh\,mm" };
        if (TimeSpan.TryParseExact(raw, formats, CultureInfo.InvariantCulture, out var parsed))
        {
            time = parsed;
            return true;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length is < 1 or > 4)
            return false;

        int hour;
        int minute;
        switch (digits.Length)
        {
            case 1:
            case 2:
                hour = int.Parse(digits, CultureInfo.InvariantCulture);
                minute = 0;
                break;
            case 3:
                hour = int.Parse(digits[..1], CultureInfo.InvariantCulture);
                minute = int.Parse(digits[1..], CultureInfo.InvariantCulture);
                break;
            default:
                hour = int.Parse(digits[..2], CultureInfo.InvariantCulture);
                minute = int.Parse(digits[2..], CultureInfo.InvariantCulture);
                break;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
            return false;

        time = new TimeSpan(hour, minute, 0);
        return true;
    }
}
