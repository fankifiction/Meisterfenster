namespace Wochenplanung.Application.Interfaces;

public interface IDatabaseStartup
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
