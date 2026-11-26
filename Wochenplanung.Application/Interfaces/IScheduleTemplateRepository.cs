using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Interfaces;

public interface IScheduleTemplateRepository
{
    Task<List<ScheduleTemplate>> GetAllAsync();
    Task MergeAllAsync(IReadOnlyList<ScheduleTemplate> templates);
}
