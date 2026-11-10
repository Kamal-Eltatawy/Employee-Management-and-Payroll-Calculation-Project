using Payroll.Domain.Payroll;

namespace Payroll.Application.Payroll;

public sealed record PayslipLineResponse(string Name, decimal Amount, bool IsTaxable);

/// <summary>Every amount at full precision; the client rounds for display (D32/D33).</summary>
public sealed record PayslipResponse(
    int EmployeeId, string EmployeeName, string Month, int PolicyId, DateOnly PolicyEffectiveFrom,
    string TaxSchemeName, TaxMethod TaxMethod, decimal ProrationFactor, int DaysPaid, int DaysInMonth, decimal BaseSalary,
    IReadOnlyList<PayslipLineResponse> Allowances, decimal GrossPay, decimal TaxableIncome, decimal IncomeTax,
    decimal SocialInsurance, IReadOnlyList<PayslipLineResponse> Deductions, decimal OtherDeductionsTotal,
    decimal NetPay, bool IsNetNegative);

public sealed record TaxBracketDto(decimal? UpperBound, decimal RatePercent);

public sealed record PayrollPolicyRequest(
    int TaxSchemeId, DateOnly EffectiveFrom, decimal InsuranceRatePercent, decimal? InsuranceCap, IReadOnlyList<TaxBracketDto> TaxBrackets);

public sealed record PayrollPolicyResponse(
    int Id, int TaxSchemeId, DateOnly EffectiveFrom, decimal InsuranceRatePercent, decimal? InsuranceCap,
    IReadOnlyList<TaxBracketDto> TaxBrackets, bool IsModifiable);
