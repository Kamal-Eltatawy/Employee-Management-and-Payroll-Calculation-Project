using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Domain.PayComponents;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure.Repositories;

public sealed class PayComponentTypeRepository(AppDbContext db) : IPayComponentTypeRepository
{
    public async Task<IReadOnlyList<PayComponentType>> ListAsync(CancellationToken ct) =>
        await db.PayComponentTypes.AsNoTracking().OrderBy(t => t.Kind).ThenBy(t => t.Name).ToListAsync(ct);

    public Task<PayComponentType?> GetByIdAsync(int id, CancellationToken ct) =>
        db.PayComponentTypes.SingleOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<PayComponentType>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.PayComponentTypes.Where(t => ids.Contains(t.Id)).ToListAsync(ct);

    public Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct) =>
        db.PayComponentTypes.AnyAsync(t => t.Name == name.Trim() && (excludeId == null || t.Id != excludeId), ct);

    // Queries the join table directly, so pay components of soft-deleted employees count too (D7).
    public Task<bool> IsInUseAsync(int id, CancellationToken ct) =>
        db.EmployeePayComponents.AnyAsync(c => c.PayComponentTypeId == id, ct);

    public void Add(PayComponentType type) => db.PayComponentTypes.Add(type);

    public void Remove(PayComponentType type) => db.PayComponentTypes.Remove(type);
}
