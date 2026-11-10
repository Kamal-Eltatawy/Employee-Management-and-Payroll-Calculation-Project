using Payroll.Domain.Departments;

namespace Payroll.Application.Abstractions;

public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct);

    Task<bool> ExistsAsync(int id, CancellationToken ct);
}
