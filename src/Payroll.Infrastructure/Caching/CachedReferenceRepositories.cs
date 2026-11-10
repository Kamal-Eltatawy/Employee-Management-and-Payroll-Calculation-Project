using Microsoft.Extensions.Caching.Memory;
using Payroll.Application.Abstractions;
using Payroll.Domain.Departments;
using Payroll.Domain.Payroll;

namespace Payroll.Infrastructure.Caching;

/// <summary>
/// Caching decorators (D62): consumers keep depending on the repository interfaces; only the composition root
/// knows about the cache. Departments and tax schemes are read-only seed data, so they are cached for a while.
/// </summary>
public sealed class CachedDepartmentRepository(IDepartmentRepository inner, IMemoryCache cache) : IDepartmentRepository
{
    private const string Key = "departments";

    public async Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct) =>
        (await cache.GetOrCreateAsync(Key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDurations.ReferenceData;
            return inner.ListAsync(ct);
        }))!;

    public async Task<bool> ExistsAsync(int id, CancellationToken ct) => (await ListAsync(ct)).Any(d => d.Id == id);
}

public sealed class CachedTaxSchemeRepository(ITaxSchemeRepository inner, IMemoryCache cache) : ITaxSchemeRepository
{
    private const string Key = "tax-schemes";

    public async Task<IReadOnlyList<TaxScheme>> ListAsync(CancellationToken ct) =>
        (await cache.GetOrCreateAsync(Key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDurations.ReferenceData;
            return inner.ListAsync(ct);
        }))!;

    /// <summary>Tracked lookup used by policy writes: not cached.</summary>
    public Task<TaxScheme?> GetByIdAsync(int id, CancellationToken ct) => inner.GetByIdAsync(id, ct);

    public async Task<bool> ExistsAsync(int id, CancellationToken ct) => (await ListAsync(ct)).Any(s => s.Id == id);
}

/// <summary>
/// The policy effective for a closed (past) month can never change (D54), so it is cached. The current and future months
/// still go to the database, because their policies can be created, edited or deleted.
/// </summary>
public sealed class CachedPayrollPolicyRepository(IPayrollPolicyRepository inner, IMemoryCache cache, TimeProvider clock)
    : IPayrollPolicyRepository
{
    public Task<IReadOnlyList<PayrollPolicy>> ListAsync(CancellationToken ct) => inner.ListAsync(ct);

    public Task<PayrollPolicy?> GetByIdAsync(int id, CancellationToken ct) => inner.GetByIdAsync(id, ct);

    public async Task<PayrollPolicy?> GetEffectiveForAsync(int taxSchemeId, PayrollMonth month, CancellationToken ct)
    {
        var currentMonth = PayrollMonth.From(DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        if (month >= currentMonth)
        {
            return await inner.GetEffectiveForAsync(taxSchemeId, month, ct);
        }

        return await cache.GetOrCreateAsync($"policy:{taxSchemeId}:{month}", entry =>
        {
            entry.SlidingExpiration = CacheDurations.ClosedMonthPolicy;
            return inner.GetEffectiveForAsync(taxSchemeId, month, ct);
        });
    }

    public Task<bool> EffectiveFromExistsAsync(int taxSchemeId, DateOnly effectiveFrom, int? excludeId, CancellationToken ct) =>
        inner.EffectiveFromExistsAsync(taxSchemeId, effectiveFrom, excludeId, ct);

    public void Add(PayrollPolicy policy) => inner.Add(policy);

    public void Remove(PayrollPolicy policy) => inner.Remove(policy);
}

internal static class CacheDurations
{
    public static readonly TimeSpan ReferenceData = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ClosedMonthPolicy = TimeSpan.FromHours(1);
}
