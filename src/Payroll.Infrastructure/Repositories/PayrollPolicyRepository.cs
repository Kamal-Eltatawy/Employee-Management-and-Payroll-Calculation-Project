using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Domain.Payroll;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure.Repositories;

public sealed class PayrollPolicyRepository(AppDbContext db) : IPayrollPolicyRepository
{
    public async Task<IReadOnlyList<PayrollPolicy>> ListAsync(CancellationToken ct) =>
        await db.PayrollPolicies.AsNoTracking().Include(p => p.TaxBrackets)
            .OrderBy(p => p.TaxSchemeId).ThenBy(p => p.EffectiveFrom).ToListAsync(ct);

    public Task<PayrollPolicy?> GetByIdAsync(int id, CancellationToken ct) =>
        db.PayrollPolicies.Include(p => p.TaxBrackets).SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<PayrollPolicy?> GetEffectiveForAsync(int taxSchemeId, PayrollMonth month, CancellationToken ct)
    {
        var firstDay = month.FirstDay;
        return db.PayrollPolicies
            .AsNoTracking()
            .Include(p => p.TaxBrackets)
            .Where(p => p.TaxSchemeId == taxSchemeId && p.EffectiveFrom <= firstDay)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> EffectiveFromExistsAsync(int taxSchemeId, DateOnly effectiveFrom, int? excludeId, CancellationToken ct) =>
        db.PayrollPolicies.AnyAsync(
            p => p.TaxSchemeId == taxSchemeId && p.EffectiveFrom == effectiveFrom && (excludeId == null || p.Id != excludeId), ct);

    public void Add(PayrollPolicy policy) => db.PayrollPolicies.Add(policy);

    public void Remove(PayrollPolicy policy) => db.PayrollPolicies.Remove(policy);
}
