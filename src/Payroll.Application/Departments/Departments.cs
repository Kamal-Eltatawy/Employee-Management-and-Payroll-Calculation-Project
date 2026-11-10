using Mapster;
using Payroll.Application.Abstractions;

namespace Payroll.Application.Departments;

public sealed record DepartmentResponse(int Id, string Name);

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentResponse>> ListAsync(CancellationToken ct);
}

public sealed class DepartmentService(IDepartmentRepository departments, TypeAdapterConfig mapping) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentResponse>> ListAsync(CancellationToken ct) =>
        (await departments.ListAsync(ct)).Select(d => d.Adapt<DepartmentResponse>(mapping)).ToList();
}
