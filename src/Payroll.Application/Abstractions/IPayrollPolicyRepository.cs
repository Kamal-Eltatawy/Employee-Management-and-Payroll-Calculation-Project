using Payroll.Domain.Payroll;

namespace Payroll.Application.Abstractions;

public interface IPayrollPolicyRepository
{
    /// <summary>Ordered by EffectiveFrom. Includes tax brackets.</summary>
    Task<IReadOnlyList<PayrollPolicy>> ListAsync(CancellationToken ct);

    Task<PayrollPolicy?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The scheme's policy with the latest EffectiveFrom on or before the first day of the month (D29/D53).</summary>
    Task<PayrollPolicy?> GetEffectiveForAsync(int taxSchemeId, PayrollMonth month, CancellationToken ct);

    Task<bool> EffectiveFromExistsAsync(int taxSchemeId, DateOnly effectiveFrom, int? excludeId, CancellationToken ct);

    void Add(PayrollPolicy policy);

    void Remove(PayrollPolicy policy);
}
