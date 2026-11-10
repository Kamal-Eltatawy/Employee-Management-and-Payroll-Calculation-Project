using Payroll.Application.Common;

namespace Payroll.Application.Employees;

public interface IEmployeeService
{
    Task<EmployeeResponse> GetAsync(int id, CancellationToken ct);

    Task<PagedResult<EmployeeListItem>> SearchAsync(EmployeeSearchRequest request, CancellationToken ct);

    Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken ct);

    Task<EmployeeResponse> UpdateAsync(int id, UpdateEmployeeRequest request, CancellationToken ct);

    Task DeleteAsync(int id, CancellationToken ct);
}
