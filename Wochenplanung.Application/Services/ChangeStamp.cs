using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Services;

public static class ChangeStamp
{
    public static string Actor
    {
        get
        {
            var user = Environment.UserName;
            var machine = Environment.MachineName;
            if (string.IsNullOrWhiteSpace(user))
                return machine;
            if (string.IsNullOrWhiteSpace(machine))
                return user;
            return $"{user}@{machine}";
        }
    }

    public static DateTime UtcNow => DateTime.UtcNow;

    public static void Touch(Employee employee)
    {
        employee.LastModifiedBy = Actor;
        employee.LastModifiedAt = UtcNow;
    }

    public static void Touch(WorkTask task)
    {
        task.LastModifiedBy = Actor;
        task.LastModifiedAt = UtcNow;
    }

    public static string Format(string? actor, DateTime? utcTimestamp)
    {
        if (string.IsNullOrWhiteSpace(actor) && utcTimestamp == null)
            return string.Empty;

        var local = utcTimestamp?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "";
        if (string.IsNullOrWhiteSpace(actor))
            return local;
        if (string.IsNullOrWhiteSpace(local))
            return actor;
        return $"{actor} · {local}";
    }
}
