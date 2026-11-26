using Microsoft.EntityFrameworkCore;
using Wochenplanung.Application.Exceptions;
using Wochenplanung.Application.Interfaces;
using Wochenplanung.Application.Services;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Infrastructure.Data;

namespace Wochenplanung.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly ScheduleDbContext _context;

    public EmployeeRepository(ScheduleDbContext context)
    {
        _context = context;
    }

    public async Task<List<Employee>> GetAllAsync()
    {
        // Tasks bewusst nicht mitladen – kommen separat über GetTasksForWeek/DateRange.
        var list = await _context.Employees
            .AsNoTracking()
            .ToListAsync();

        return list
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => NaturalEmployeeNumberKey(e.EmployeeNumber), StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Employee?> GetByIdAsync(int id)
    {
        return await _context.Employees
            .Include(e => e.Tasks)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Employee> AddAsync(Employee employee)
    {
        var maxOrder = await _context.Employees.MaxAsync(e => (int?)e.DisplayOrder) ?? -1;
        var entity = new Employee
        {
            EmployeeNumber = string.IsNullOrWhiteSpace(employee.EmployeeNumber) ? null : employee.EmployeeNumber.Trim(),
            FirstName = employee.FirstName.Trim(),
            LastName = employee.LastName.Trim(),
            Role = string.IsNullOrWhiteSpace(employee.Role) ? null : employee.Role.Trim(),
            DisplayOrder = maxOrder + 1,
            IsActive = employee.IsActive
        };
        ChangeStamp.Touch(entity);
        _context.Employees.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Employee employee)
    {
        var existingEmployee = await _context.Employees.FindAsync(employee.Id);
        if (existingEmployee == null)
            return;

        _context.Entry(existingEmployee).Property(e => e.Version).OriginalValue = employee.Version;
        existingEmployee.EmployeeNumber = string.IsNullOrWhiteSpace(employee.EmployeeNumber) ? null : employee.EmployeeNumber.Trim();
        existingEmployee.FirstName = employee.FirstName.Trim();
        existingEmployee.LastName = employee.LastName.Trim();
        existingEmployee.Role = string.IsNullOrWhiteSpace(employee.Role) ? null : employee.Role.Trim();
        existingEmployee.DisplayOrder = employee.DisplayOrder;
        existingEmployee.IsActive = employee.IsActive;
        existingEmployee.Version = employee.Version + 1;
        ChangeStamp.Touch(existingEmployee);
        employee.LastModifiedBy = existingEmployee.LastModifiedBy;
        employee.LastModifiedAt = existingEmployee.LastModifiedAt;
        try
        {
            await _context.SaveChangesAsync();
            employee.Version = existingEmployee.Version;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }

    public async Task UpdateOrderAsync(IReadOnlyList<int> employeeIdsInOrder)
    {
        for (int i = 0; i < employeeIdsInOrder.Count; i++)
        {
            var emp = await _context.Employees.FindAsync(employeeIdsInOrder[i]);
            if (emp != null)
            {
                emp.DisplayOrder = i;
                emp.Version += 1;
                ChangeStamp.Touch(emp);
            }
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee != null)
        {
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            // Reihenfolge kompakt halten, ohne nach Nummer umzusortieren (Drag bleibt erhalten)
            var remaining = await _context.Employees
                .OrderBy(e => e.DisplayOrder)
                .ThenBy(e => e.LastName)
                .ToListAsync();
            for (var i = 0; i < remaining.Count; i++)
                remaining[i].DisplayOrder = i;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>Setzt DisplayOrder nach Mitarbeiternummer (natürlich numerisch).</summary>
    public async Task NormalizeOrderByEmployeeNumberAsync()
    {
        await ApplyDisplayOrderByEmployeeNumberAsync();
    }

    /// <summary>Setzt DisplayOrder nach Name (Nachname, Vorname).</summary>
    public async Task NormalizeOrderByNameAsync()
    {
        var all = await _context.Employees.ToListAsync();
        var ordered = all
            .OrderBy(e => e.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => NaturalEmployeeNumberKey(e.EmployeeNumber), StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
            ordered[i].DisplayOrder = i;

        await _context.SaveChangesAsync();
    }

    /// <summary>Setzt DisplayOrder nach Mitarbeiternummer (natürlich numerisch), damit die Liste nach Nummer sortiert bleibt.</summary>
    private async Task ApplyDisplayOrderByEmployeeNumberAsync()
    {
        var all = await _context.Employees.ToListAsync();
        var ordered = all
            .OrderBy(e => NaturalEmployeeNumberKey(e.EmployeeNumber), StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
            ordered[i].DisplayOrder = i;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Natürliche Sortierung: "2" vor "10", leere Nummern ans Ende.
    /// </summary>
    internal static string NaturalEmployeeNumberKey(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return "\uFFFF";

        var trimmed = number.Trim();
        if (long.TryParse(trimmed, out var numeric))
            return numeric.ToString("D20");

        // Gemischte Nummern: Ziffernblöcke auf gleiche Breite bringen
        var chars = trimmed.ToCharArray();
        var result = new System.Text.StringBuilder();
        var i = 0;
        while (i < chars.Length)
        {
            if (char.IsDigit(chars[i]))
            {
                var start = i;
                while (i < chars.Length && char.IsDigit(chars[i]))
                    i++;
                var digits = trimmed.Substring(start, i - start);
                if (long.TryParse(digits, out var n))
                    result.Append(n.ToString("D20"));
                else
                    result.Append(digits);
            }
            else
            {
                result.Append(char.ToUpperInvariant(chars[i]));
                i++;
            }
        }
        return result.ToString();
    }
}
