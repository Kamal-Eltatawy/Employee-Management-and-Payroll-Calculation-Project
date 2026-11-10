using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Domain.Payroll;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure.Repositories;

public sealed class TaxSchemeRepository(AppDbContext db) : ITaxSchemeRepository
{
    public async Task<IReadOnlyList<TaxScheme>> ListAsync(CancellationToken ct) =>
        await db.TaxSchemes.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);

    public Task<TaxScheme?> GetByIdAsync(int id, CancellationToken ct) => db.TaxSchemes.SingleOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct) => db.TaxSchemes.AnyAsync(s => s.Id == id, ct);
}
