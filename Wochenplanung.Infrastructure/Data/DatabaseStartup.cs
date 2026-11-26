using Microsoft.Extensions.Logging;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Infrastructure.Data;

namespace Wochenplanung.Infrastructure;

public sealed class DatabaseStartup : IDatabaseStartup
{
    private readonly ScheduleDbContext _context;
    private readonly ILogger<DatabaseStartup> _logger;
    private readonly DatabaseOptions _options;

    public DatabaseStartup(ScheduleDbContext context, ILogger<DatabaseStartup> logger, DatabaseOptions options)
    {
        _context = context;
        _logger = logger;
        _options = options;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        DatabaseInitializer.InitializeAsync(_context, _logger, _options, cancellationToken);
}
