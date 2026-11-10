using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;

namespace Payroll.Domain.Payroll;

public interface IPayrollCalculator
{
    Payslip Calculate(Employee employee, PayrollPolicy policy, PayrollMonth month);
}

/// <summary>Orchestrates the payslip formula of spec §11.1. Pure: no I/O, no clock, no rounding.</summary>
public sealed class PayrollCalculator(
    IEnumerable<ITaxCalculator> taxCalculators,
    ISocialInsuranceCalculator insuranceCalculator,
    IProrationCalculator prorationCalculator) : IPayrollCalculator
{
    private readonly Dictionary<TaxMethod, ITaxCalculator> _taxCalculators =
        taxCalculators.ToDictionary(calculator => calculator.Method);

    public Payslip Calculate(Employee employee, PayrollPolicy policy, PayrollMonth month)
    {
        employee.EnsureEligibleForPayslip(month);

        var taxScheme = employee.TaxScheme
            ?? throw new InvalidOperationException("The employee's tax scheme must be loaded to calculate a payslip.");
        if (policy.TaxSchemeId != taxScheme.Id)
        {
            throw new InvalidOperationException("The payroll policy belongs to another tax scheme.");
        }

        var taxCalculator = _taxCalculators.GetValueOrDefault(taxScheme.Method)
            ?? throw new InvalidOperationException($"No tax calculator is registered for {taxScheme.Method}.");

        var proration = prorationCalculator.Calculate(employee.HireDate, month);
        var factor = proration.Factor;
        var baseSalary = employee.BaseMonthlySalary * factor;

        var components = employee.PayComponents
            .OrderBy(c => c.PayComponentTypeId)
            .Select(c => (c.PayComponentTypeId, Type: c.PayComponentType
                ?? throw new InvalidOperationException("Pay component types must be loaded to calculate a payslip."),
                c.Amount))
            .ToList();

        // Allowances are prorated; deductions are taken in full (D36). Deductions are never taxable.
        var allowances = components
            .Where(c => c.Type.Kind == PayComponentKind.Allowance)
            .Select(c => new PayslipLine(c.PayComponentTypeId, c.Type.Name, c.Amount * factor, c.Type.IsTaxable))
            .ToList();
        var deductions = components
            .Where(c => c.Type.Kind == PayComponentKind.Deduction)
            .Select(c => new PayslipLine(c.PayComponentTypeId, c.Type.Name, c.Amount, IsTaxable: false))
            .ToList();

        var grossPay = baseSalary + allowances.Sum(a => a.Amount);
        var taxableIncome = baseSalary + allowances.Where(a => a.IsTaxable).Sum(a => a.Amount);
        var incomeTax = taxCalculator.Calculate(taxableIncome, policy.TaxBrackets);
        var socialInsurance = insuranceCalculator.Calculate(baseSalary, policy.InsuranceRatePercent, policy.InsuranceCap, factor);
        var otherDeductionsTotal = deductions.Sum(d => d.Amount);
        var netPay = grossPay - incomeTax - socialInsurance - otherDeductionsTotal;

        return new Payslip(
            employee.Id, employee.FullName, month, policy.Id, policy.EffectiveFrom, taxScheme.Name, taxScheme.Method, proration,
            baseSalary, allowances, grossPay, taxableIncome, incomeTax, socialInsurance,
            deductions, otherDeductionsTotal, netPay);
    }
}
