using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Domain.Departments;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure.Repositories;

public sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public async Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct) =>
        await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct) => db.Departments.AnyAsync(d => d.Id == id, ct);
}
