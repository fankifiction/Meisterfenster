namespace Wochenplanung.Application.Interfaces;

/// <summary>Read-only view of the active database mode. Keeps UI free of EF types.</summary>
public interface IDatabaseInfo
{
    bool IsServer { get; }
    string DisplayName { get; }
    /// <summary>False when the SQL login may only read (meisterfenster_lesen).</summary>
    bool CanWrite { get; }
}
