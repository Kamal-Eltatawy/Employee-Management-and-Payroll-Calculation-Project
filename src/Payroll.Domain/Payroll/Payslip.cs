namespace Payroll.Domain.Payroll;

/// <summary>One allowance or deduction line; the type id lets callers translate or link the line without matching names.</summary>
public sealed record PayslipLine(int PayComponentTypeId, string Name, decimal Amount, bool IsTaxable);

/// <summary>The calculation result. Every amount is at full precision (D32).</summary>
public sealed record Payslip(
    int EmployeeId,
    string EmployeeName,
    PayrollMonth Month,
    int PolicyId,
    DateOnly PolicyEffectiveFrom,
    string TaxSchemeName,
    TaxMethod TaxMethod,
    ProrationResult Proration,
    decimal BaseSalary,
    IReadOnlyList<PayslipLine> Allowances,
    decimal GrossPay,
    decimal TaxableIncome,
    decimal IncomeTax,
    decimal SocialInsurance,
    IReadOnlyList<PayslipLine> Deductions,
    decimal OtherDeductionsTotal,
    decimal NetPay)
{
    public bool IsNetNegative => NetPay < 0;
}
