using Payroll.Application.Common;
using Payroll.Domain.Employees;

namespace Payroll.Application.Abstractions;

public interface IEmployeeRepository
{
    /// <summary>Excludes deleted employees. Includes Department and PayComponents (with their type).</summary>
    Task<Employee?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Excludes deleted employees. Items include Department.</summary>
    Task<PagedResult<Employee>> SearchAsync(EmployeeQuery query, CancellationToken ct);

    /// <summary>Includes deleted employees: an email stays reserved after soft delete (D6).</summary>
    Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeEmployeeId, CancellationToken ct);

    void Add(Employee employee);
}
