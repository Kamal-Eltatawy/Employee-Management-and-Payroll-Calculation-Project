using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Payroll;

/// <summary>Read side (Phase 3). The write side (create / update / delete future policies) is added in Phase 5.</summary>
public interface IPayrollPolicyService
{
    Task<IReadOnlyList<PayrollPolicyResponse>> ListAsync(CancellationToken ct);

    Task<PayrollPolicyResponse> GetAsync(int id, CancellationToken ct);

    Task<PayrollPolicyResponse> GetEffectiveAsync(int taxSchemeId, string? month, CancellationToken ct);
}

public sealed class PayrollPolicyService(IPayrollPolicyRepository policies, TimeProvider clock) : IPayrollPolicyService
{
    public async Task<IReadOnlyList<PayrollPolicyResponse>> ListAsync(CancellationToken ct)
    {
        var currentMonth = clock.CurrentMonth();
        return (await policies.ListAsync(ct)).Select(p => ToResponse(p, currentMonth)).ToList();
    }

    public async Task<PayrollPolicyResponse> GetAsync(int id, CancellationToken ct) =>
        ToResponse(await policies.GetByIdAsync(id, ct) ?? throw new NotFoundException("Payroll policy", id), clock.CurrentMonth());

    public async Task<PayrollPolicyResponse> GetEffectiveAsync(int taxSchemeId, string? month, CancellationToken ct)
    {
        var payrollMonth = MonthParameter.ParseOrCurrent(month, clock);
        var policy = await policies.GetEffectiveForAsync(taxSchemeId, payrollMonth, ct)
            ?? throw new DomainException($"No payroll policy is effective for {payrollMonth}.");
        return ToResponse(policy, clock.CurrentMonth());
    }

    // Explicit rather than Mapster: IsModifiable depends on the clock, not only on the entity.
    private static PayrollPolicyResponse ToResponse(PayrollPolicy policy, PayrollMonth currentMonth) => new(
        policy.Id,
        policy.TaxSchemeId,
        policy.EffectiveFrom,
        policy.InsuranceRatePercent,
        policy.InsuranceCap,
        policy.TaxBrackets.Select(b => new TaxBracketDto(b.UpperBound, b.RatePercent)).ToList(),
        IsModifiable: policy.EffectiveFrom > currentMonth.FirstDay);
}
