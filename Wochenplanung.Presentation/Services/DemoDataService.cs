using Wochenplanung.Infrastructure.Data;

namespace Wochenplanung.Presentation.Services;

public sealed class DemoDataService
{
    private readonly ScheduleDbContext _db;
    private readonly TaskTemplateStorage _templateStorage;

    public DemoDataService(ScheduleDbContext db, TaskTemplateStorage templateStorage)
    {
        _db = db;
        _templateStorage = templateStorage;
    }

    public Task SeedFeedbackDemoAsync() => DemoDataSeeder.SeedAsync(_db, _templateStorage);
}
