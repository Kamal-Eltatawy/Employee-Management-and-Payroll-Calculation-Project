using FluentValidation;
using FluentValidation.Results;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Payroll;

/// <summary>Past months are closed (D31/D54): policies starting in the current month or later can be created, changed or deleted.</summary>
public interface IPayrollPolicyService
{
    Task<IReadOnlyList<PayrollPolicyResponse>> ListAsync(CancellationToken ct);

    Task<PayrollPolicyResponse> GetAsync(int id, CancellationToken ct);

    Task<PayrollPolicyResponse> GetEffectiveAsync(int taxSchemeId, string? month, CancellationToken ct);

    Task<PayrollPolicyResponse> CreateAsync(PayrollPolicyRequest request, CancellationToken ct);

    Task<PayrollPolicyResponse> UpdateAsync(int id, PayrollPolicyRequest request, CancellationToken ct);

    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class PayrollPolicyService(
    IPayrollPolicyRepository policies,
    ITaxSchemeRepository taxSchemes,
    IUnitOfWork unitOfWork,
    IValidator<PayrollPolicyRequest> validator,
    TimeProvider clock) : IPayrollPolicyService
{
    public async Task<IReadOnlyList<PayrollPolicyResponse>> ListAsync(CancellationToken ct)
    {
        var currentMonth = clock.CurrentMonth();
        return (await policies.ListAsync(ct)).Select(p => ToResponse(p, currentMonth)).ToList();
    }

    public async Task<PayrollPolicyResponse> GetAsync(int id, CancellationToken ct) =>
        ToResponse(await LoadAsync(id, ct), clock.CurrentMonth());

    public async Task<PayrollPolicyResponse> GetEffectiveAsync(int taxSchemeId, string? month, CancellationToken ct)
    {
        var payrollMonth = MonthParameter.ParseOrCurrent(month, clock);
        var policy = await policies.GetEffectiveForAsync(taxSchemeId, payrollMonth, ct)
            ?? throw new DomainException(ErrorCodes.NoPolicyForMonth, $"No payroll policy is effective for {payrollMonth}.", payrollMonth.ToString());
        return ToResponse(policy, clock.CurrentMonth());
    }

    public async Task<PayrollPolicyResponse> CreateAsync(PayrollPolicyRequest request, CancellationToken ct)
    {
        var scheme = await ValidateAsync(request, excludeId: null, ct);

        var policy = new PayrollPolicy(scheme, request.EffectiveFrom, request.InsuranceRatePercent, request.InsuranceCap,
            ToDefinitions(request), clock.CurrentMonth(), clock.UtcNow());
        policies.Add(policy);
        await unitOfWork.SaveChangesAsync(ct);

        return ToResponse(policy, clock.CurrentMonth());
    }

    public async Task<PayrollPolicyResponse> UpdateAsync(int id, PayrollPolicyRequest request, CancellationToken ct)
    {
        var policy = await LoadModifiableAsync(id, ct);
        if (request.TaxSchemeId != policy.TaxSchemeId)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.TaxSchemeId),
                "A payroll policy cannot move to another tax scheme. Create a policy in that scheme instead.")
            { ErrorCode = ErrorCodes.PolicyCannotMoveScheme }]);
        }

        var scheme = await ValidateAsync(request, excludeId: id, ct);
        policy.Replace(scheme, request.EffectiveFrom, request.InsuranceRatePercent, request.InsuranceCap,
            ToDefinitions(request), clock.CurrentMonth());
        await unitOfWork.SaveChangesAsync(ct);

        return ToResponse(policy, clock.CurrentMonth());
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var policy = await LoadModifiableAsync(id, ct);
        policies.Remove(policy);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<PayrollPolicy> LoadAsync(int id, CancellationToken ct) =>
        await policies.GetByIdAsync(id, ct) ?? throw new NotFoundException("Payroll policy", id);

    /// <summary>A policy that started before the current month covers closed months (D31/D54 → 409).</summary>
    private async Task<PayrollPolicy> LoadModifiableAsync(int id, CancellationToken ct)
    {
        var policy = await LoadAsync(id, ct);
        if (!policy.IsModifiable(clock.CurrentMonth()))
        {
            throw ConflictException.Coded(ErrorCodes.PolicyCoversClosedMonths,
                $"The payroll policy effective from {policy.EffectiveFrom:yyyy-MM-dd} covers closed months and cannot be changed or deleted. " +
                "To correct the current month, create a policy effective from the first day of this month.",
                field: null, policy.EffectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        return policy;
    }

    /// <summary>Input rules, then the rules that need data: the scheme exists, the bracket shape fits its method, the date is free.</summary>
    private async Task<TaxScheme> ValidateAsync(PayrollPolicyRequest request, int? excludeId, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var scheme = await taxSchemes.GetByIdAsync(request.TaxSchemeId, ct)
            ?? throw new ValidationException([new ValidationFailure(nameof(request.TaxSchemeId), "Tax scheme does not exist.")
            { ErrorCode = ErrorCodes.TaxSchemeNotFound }]);
        if (scheme.DescribeBracketShapeProblem(request.TaxBrackets.Count) is { } problem)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.TaxBrackets), problem)
            { ErrorCode = $"{ErrorCodes.BracketShape}.{scheme.Method}" }]);
        }

        if (await policies.EffectiveFromExistsAsync(request.TaxSchemeId, request.EffectiveFrom, excludeId, ct))
        {
            throw ConflictException.Coded(ErrorCodes.PolicyDateTaken,
                $"The {scheme.Name} scheme already has a policy effective from {request.EffectiveFrom:yyyy-MM-dd}.", "effectiveFrom",
                scheme.Name, request.EffectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        return scheme;
    }

    private static List<TaxBracketDefinition> ToDefinitions(PayrollPolicyRequest request) =>
        request.TaxBrackets.Select(b => new TaxBracketDefinition(b.UpperBound, b.RatePercent)).ToList();

    // Explicit rather than Mapster: IsModifiable depends on the clock, not only on the entity.
    private static PayrollPolicyResponse ToResponse(PayrollPolicy policy, PayrollMonth currentMonth) => new(
        policy.Id,
        policy.TaxSchemeId,
        policy.EffectiveFrom,
        policy.InsuranceRatePercent,
        policy.InsuranceCap,
        policy.TaxBrackets.Select(b => new TaxBracketDto(b.UpperBound, b.RatePercent)).ToList(),
        IsModifiable: policy.IsModifiable(currentMonth));
}
