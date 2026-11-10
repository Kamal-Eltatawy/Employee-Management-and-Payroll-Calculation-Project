namespace Payroll.Domain.Payroll;

public interface ISocialInsuranceCalculator
{
    decimal Calculate(decimal proratedBaseSalary, decimal ratePercent, decimal? cap, decimal prorationFactor);
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
