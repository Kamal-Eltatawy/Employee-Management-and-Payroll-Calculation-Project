using Payroll.Domain.Common;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;

namespace Payroll.Domain.Payroll;

public readonly record struct ProrationResult(decimal Factor, int DaysPaid, int DaysInMonth);

public interface IProrationCalculator
{
    ProrationResult Calculate(DateOnly hireDate, PayrollMonth month);
}

/// <summary>One implementation per <see cref="TaxMethod"/>. All implementations are registered; the payroll calculator picks by method (D53).</summary>
public interface ITaxCalculator
{
    TaxMethod Method { get; }

    decimal Calculate(decimal taxableIncome, IReadOnlyList<TaxBracket> brackets);
}

public interface ISocialInsuranceCalculator
{
    decimal Calculate(decimal proratedBaseSalary, decimal ratePercent, decimal? cap, decimal prorationFactor);
}

public interface IPayrollCalculator
{
    Payslip Calculate(Employee employee, PayrollPolicy policy, PayrollMonth month);
}

/// <summary>Hire-month proration by calendar days; the hire day counts (D35). Full precision, no rounding.</summary>
public sealed class CalendarDayProrationCalculator : IProrationCalculator
{
    public ProrationResult Calculate(DateOnly hireDate, PayrollMonth month)
    {
        var hireMonth = PayrollMonth.From(hireDate);
        if (month < hireMonth)
        {
            throw new DomainException($"Employee was hired in {hireMonth}.");
        }

        if (month > hireMonth)
        {
            return new ProrationResult(1m, month.DaysInMonth, month.DaysInMonth);
        }

        var daysPaid = month.DaysInMonth - hireDate.Day + 1;
        return new ProrationResult((decimal)daysPaid / month.DaysInMonth, daysPaid, month.DaysInMonth);
    }
}

/// <summary>Σ max(0, min(taxable, upper_k) − lower_k) × rate_k / 100, with lower_k = upper_(k−1) (D27).</summary>
public sealed class ProgressiveTaxCalculator : ITaxCalculator
{
    public TaxMethod Method => TaxMethod.Progressive;

    public decimal Calculate(decimal taxableIncome, IReadOnlyList<TaxBracket> brackets)
    {
        decimal tax = 0;
        decimal lower = 0;
        foreach (var bracket in brackets.OrderBy(b => b.SortOrder))
        {
            if (taxableIncome <= lower)
            {
                break;
            }

            var upper = bracket.UpperBound ?? decimal.MaxValue;
            tax += (Math.Min(taxableIncome, upper) - lower) * bracket.RatePercent / 100m;
            lower = upper;
        }

        return tax;
    }
}

/// <summary>One rate (held by the single open bracket) on the whole taxable income.</summary>
public sealed class FlatTaxCalculator : ITaxCalculator
{
    public TaxMethod Method => TaxMethod.Flat;

    public decimal Calculate(decimal taxableIncome, IReadOnlyList<TaxBracket> brackets) =>
        taxableIncome <= 0 ? 0m : taxableIncome * brackets.Single().RatePercent / 100m;
}

/// <summary>No income tax.</summary>
public sealed class ExemptTaxCalculator : ITaxCalculator
{
    public TaxMethod Method => TaxMethod.Exempt;

    public decimal Calculate(decimal taxableIncome, IReadOnlyList<TaxBracket> brackets) => 0m;
}

/// <summary>A percentage of the (prorated) base salary only, capped by the prorated cap when there is one (D36).</summary>
public sealed class SocialInsuranceCalculator : ISocialInsuranceCalculator
{
    public decimal Calculate(decimal proratedBaseSalary, decimal ratePercent, decimal? cap, decimal prorationFactor)
    {
        var insurance = proratedBaseSalary * ratePercent / 100m;
        return cap is { } value ? Math.Min(insurance, value * prorationFactor) : insurance;
    }
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
            .Select(c => (Type: c.PayComponentType
                ?? throw new InvalidOperationException("Pay component types must be loaded to calculate a payslip."),
                c.Amount))
            .ToList();

        // Allowances are prorated; deductions are taken in full (D36). Deductions are never taxable.
        var allowances = components
            .Where(c => c.Type.Kind == PayComponentKind.Allowance)
            .Select(c => new PayslipLine(c.Type.Name, c.Amount * factor, c.Type.IsTaxable))
            .ToList();
        var deductions = components
            .Where(c => c.Type.Kind == PayComponentKind.Deduction)
            .Select(c => new PayslipLine(c.Type.Name, c.Amount, IsTaxable: false))
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
