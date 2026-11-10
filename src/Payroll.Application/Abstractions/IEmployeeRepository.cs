using Payroll.Application.Common;
using Payroll.Domain.Employees;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Abstractions;

public interface IEmployeeRepository
{
    /// <summary>Excludes deleted employees. Includes Department and PayComponents (with their type).</summary>
    Task<Employee?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Excludes deleted employees. Items include Department.</summary>
    Task<PagedResult<Employee>> SearchAsync(EmployeeQuery query, CancellationToken ct);

    /// <summary>Includes deleted employees: an email stays reserved after soft delete (D6).</summary>
    Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeEmployeeId, CancellationToken ct);

    /// <summary>Head counts computed by the database (GROUP BY), not in memory (D62).</summary>
    Task<EmployeeHeadcount> GetHeadcountAsync(PayrollMonth month, CancellationToken ct);

    /// <summary>
    /// The next batch (keyset paging by Id) of Active employees hired on or before the month, read-only,
    /// with TaxScheme and PayComponents (with their type). Lets the dashboard total a month's payroll without loading everyone.
    /// </summary>
    Task<IReadOnlyList<Employee>> ListPayableBatchAsync(PayrollMonth month, int afterId, int batchSize, CancellationToken ct);

    void Add(Employee employee);
}

public sealed record NamedCount(string Name, string? NameAr, int Count);

/// <summary>Non-deleted employees: totals, hires in a month, and Active employees per department and per tax scheme.</summary>
public sealed record EmployeeHeadcount(
    int Total, int Active, int HiredInMonth, IReadOnlyList<NamedCount> ActiveByDepartment, IReadOnlyList<NamedCount> ActiveByTaxScheme);
