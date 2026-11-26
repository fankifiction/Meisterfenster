using Microsoft.EntityFrameworkCore;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Infrastructure.Data;

namespace Wochenplanung.Infrastructure.Repositories;

public class ScheduleTemplateRepository : IScheduleTemplateRepository
{
    private readonly ScheduleDbContext _context;

    public ScheduleTemplateRepository(ScheduleDbContext context)
    {
        _context = context;
    }

    public async Task<List<ScheduleTemplate>> GetAllAsync()
    {
        return await _context.ScheduleTemplates
            .AsNoTracking()
            .OrderByDescending(t => t.IsFavorite)
            .ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.Id)
            .ToListAsync();
    }

    public async Task MergeAllAsync(IReadOnlyList<ScheduleTemplate> templates)
    {
        var existing = await _context.ScheduleTemplates.ToListAsync();
        var incomingKeys = new HashSet<string>(
            templates.Select(TemplateKey),
            StringComparer.OrdinalIgnoreCase);

        foreach (var leftover in existing.Where(e => !incomingKeys.Contains(TemplateKey(e))).ToList())
            _context.ScheduleTemplates.Remove(leftover);

        for (var i = 0; i < templates.Count; i++)
        {
            var incoming = templates[i];
            var key = TemplateKey(incoming);
            var match = existing.FirstOrDefault(e =>
                string.Equals(TemplateKey(e), key, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                _context.ScheduleTemplates.Add(new ScheduleTemplate
                {
                    Description = incoming.Description,
                    Type = incoming.Type,
                    Color = incoming.Color,
                    IsFavorite = incoming.IsFavorite,
                    DisplayOrder = i
                });
            }
            else
            {
                match.Description = incoming.Description;
                match.Type = incoming.Type;
                match.Color = incoming.Color;
                match.IsFavorite = incoming.IsFavorite;
                match.DisplayOrder = i;
            }
        }

        await _context.SaveChangesAsync();
    }

    private static string TemplateKey(ScheduleTemplate template) =>
        (template.Description ?? string.Empty).Trim();
}
