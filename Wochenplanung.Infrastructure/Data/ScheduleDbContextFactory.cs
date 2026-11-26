using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wochenplanung.Infrastructure.Data;

public sealed class ScheduleDbContextFactory : IDesignTimeDbContextFactory<ScheduleDbContext>
{
    public ScheduleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScheduleDbContext>();
        optionsBuilder.UseSqlite("Data Source=schedule.design.db");
        return new ScheduleDbContext(optionsBuilder.Options);
    }
}
