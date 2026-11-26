namespace Wochenplanung.Domain.Entities;

public class Employee
{
    public int Id { get; set; }
    public string? EmployeeNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Role { get; set; }
    /// <summary>Anzeige-Reihenfolge (0, 1, 2, …) – per Drag &amp; Drop änderbar.</summary>
    public int DisplayOrder { get; set; }
    /// <summary>False = ausgeblendet, Aufgaben bleiben in der Datenbank.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Optimistic-concurrency token for shared-server edits.</summary>
    public int Version { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public string DisplayName => string.IsNullOrWhiteSpace(EmployeeNumber) ? FullName : $"{EmployeeNumber} {FullName}";
    
    public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
}
