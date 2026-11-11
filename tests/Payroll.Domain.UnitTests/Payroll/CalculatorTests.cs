using Payroll.Domain.Payroll;

namespace Payroll.Domain.UnitTests.Payroll;

public sealed class ProgressiveTaxCalculatorTests
{
    private static readonly IReadOnlyList<TaxBracket> V1 = PolicyBuilder.V1().TaxBrackets;
    private readonly ProgressiveTaxCalculator _calculator = new();

    [Theory]
    [InlineData("0", "0")]
    [InlineData("5000", "0")]          // exactly on a boundary
    [InlineData("5000.01", "0.001")]   // boundary + 0.01
    [InlineData("15000", "1000")]
    [InlineData("19000", "1800")]      // spans all brackets
    [InlineData("-10", "0")]
    public void Applies_each_rate_only_to_its_band(string taxable, string expected) =>
        Assert.Equal(decimal.Parse(expected), _calculator.Calculate(decimal.Parse(taxable), V1));

    [Fact]
    public void Single_open_bracket_taxes_everything_at_its_rate() =>
        Assert.Equal(1234.5m, _calculator.Calculate(12345m, PolicyBuilder.Brackets(new TaxBracketDefinition(null, 10m))));

    [Fact]
    public void Long_fractional_input_is_not_rounded()
    {
        var taxable = 7354.838709677419354838709677m;

        Assert.Equal((taxable - 5000m) * 10m / 100m, _calculator.Calculate(taxable, V1));
    }
}

public sealed class SocialInsuranceCalculatorTests
{
    private readonly SocialInsuranceCalculator _calculator = new();

    [Fact]
    public void No_cap_uses_the_rate() => Assert.Equal(1870m, _calculator.Calculate(17000m, 11m, null, 1m));

    [Fact]
    public void Below_the_cap_uses_the_rate() => Assert.Equal(1100m, _calculator.Calculate(10000m, 11m, 1500m, 1m));

    [Fact]
    public void Exactly_at_the_cap() => Assert.Equal(1500m, _calculator.Calculate(15000m, 10m, 1500m, 1m));

    [Fact]
    public void Above_the_cap_is_capped() => Assert.Equal(1500m, _calculator.Calculate(17000m, 11m, 1500m, 1m));

    [Fact]
    public void Cap_is_prorated_when_the_factor_is_below_one()
    {
        var factor = 12m / 31m;

        Assert.Equal(1500m * factor, _calculator.Calculate(17000m * factor, 11m, 1500m, factor));
    }

    [Fact]
    public void Zero_cap_means_zero_insurance() => Assert.Equal(0m, _calculator.Calculate(17000m, 11m, 0m, 1m));
}

public sealed class CalendarDayProrationCalculatorTests
{
    private readonly CalendarDayProrationCalculator _calculator = new();

    [Fact]
    public void Hired_on_the_first_is_a_full_month() =>
        Assert.Equal(new ProrationResult(1m, 31, 31), _calculator.Calculate(new DateOnly(2026, 3, 1), new PayrollMonth(2026, 3)));

    [Fact]
    public void Hired_on_the_20th_of_a_31_day_month_is_12_of_31() =>
        Assert.Equal(new ProrationResult(12m / 31m, 12, 31), _calculator.Calculate(new DateOnly(2026, 3, 20), new PayrollMonth(2026, 3)));

    [Fact]
    public void Hired_on_the_last_day_counts_that_day() =>
        Assert.Equal(new ProrationResult(1m / 30m, 1, 30), _calculator.Calculate(new DateOnly(2026, 4, 30), new PayrollMonth(2026, 4)));

    [Theory]
    [InlineData(2026, 28)]
    [InlineData(2024, 29)]
    public void February_uses_its_real_length(int year, int days) =>
        Assert.Equal(new ProrationResult(15m / days, 15, days),
            _calculator.Calculate(new DateOnly(year, 2, days - 14), new PayrollMonth(year, 2)));

    [Fact]
    public void A_later_month_is_a_full_month() =>
        Assert.Equal(new ProrationResult(1m, 30, 30), _calculator.Calculate(new DateOnly(2026, 3, 20), new PayrollMonth(2026, 4)));
}

public sealed class FlatAndExemptTaxCalculatorTests
{
    private static readonly IReadOnlyList<TaxBracket> FlatFifteen =
        new PayrollPolicy(PolicyBuilder.Flat, new DateOnly(2025, 1, 1), 0m, null, [new(null, 15m)], PolicyBuilder.Entered, PolicyBuilder.UtcNow).TaxBrackets;

    [Theory]
    [InlineData("0", "0")]
    [InlineData("-5", "0")]
    [InlineData("1000", "150")]
    [InlineData("7354.84", "1103.226")]
    public void Flat_applies_one_rate_to_everything(string taxable, string expected) =>
        Assert.Equal(decimal.Parse(expected), new FlatTaxCalculator().Calculate(decimal.Parse(taxable), FlatFifteen));

    [Fact]
    public void Exempt_is_always_zero() => Assert.Equal(0m, new ExemptTaxCalculator().Calculate(50000m, []));

    [Fact]
    public void Each_calculator_declares_its_method()
    {
        Assert.Equal(TaxMethod.Progressive, new ProgressiveTaxCalculator().Method);
        Assert.Equal(TaxMethod.Flat, new FlatTaxCalculator().Method);
        Assert.Equal(TaxMethod.Exempt, new ExemptTaxCalculator().Method);
    }
}
