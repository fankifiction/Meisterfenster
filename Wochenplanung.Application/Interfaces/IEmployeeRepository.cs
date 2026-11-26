using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<List<Employee>> GetAllAsync();
    Task<Employee?> GetByIdAsync(int id);
    Task<Employee> AddAsync(Employee employee);
    Task UpdateAsync(Employee employee);
    Task UpdateOrderAsync(IReadOnlyList<int> employeeIdsInOrder);
    Task DeleteAsync(int id);
    Task NormalizeOrderByEmployeeNumberAsync();
    Task NormalizeOrderByNameAsync();
}
