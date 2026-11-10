namespace Payroll.Domain.Payroll;

/// <summary>One implementation per <see cref="TaxMethod"/>. All implementations are registered; the payroll calculator picks by method (D53).</summary>
public interface ITaxCalculator
{
    TaxMethod Method { get; }

    decimal Calculate(decimal taxableIncome, IReadOnlyList<TaxBracket> brackets);
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
