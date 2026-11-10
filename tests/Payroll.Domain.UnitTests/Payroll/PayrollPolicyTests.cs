using Payroll.Domain.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Domain.UnitTests.Payroll;

public sealed class PayrollPolicyTests
{
    private static PayrollPolicy Create(DateOnly? effectiveFrom = null, decimal rate = 11m, decimal? cap = 1500m,
        params TaxBracketDefinition[] brackets) =>
        new(PolicyBuilder.Standard, effectiveFrom ?? new DateOnly(2026, 6, 1), rate, cap,
            brackets.Length == 0 ? new TaxBracketDefinition[] { new(5000m, 0m), new(null, 10m) } : brackets, PolicyBuilder.UtcNow);

    [Fact]
    public void Valid_policy_keeps_brackets_in_order()
    {
        var policy = Create(brackets: [new(5000m, 0m), new(15000m, 10m), new(null, 20m)]);

        Assert.Equal(new decimal?[] { 5000m, 15000m, null }, policy.TaxBrackets.Select(b => b.UpperBound).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, policy.TaxBrackets.Select(b => b.SortOrder).ToArray());
    }

    [Fact]
    public void Single_open_bracket_is_valid() => Assert.Single(Create(brackets: [new(null, 10m)]).TaxBrackets);

    [Fact]
    public void Non_ascending_brackets_are_rejected() =>
        Assert.Throws<DomainException>(() => Create(brackets: [new(15000m, 0m), new(5000m, 10m), new(null, 20m)]));

    [Fact]
    public void Equal_upper_bounds_are_rejected() =>
        Assert.Throws<DomainException>(() => Create(brackets: [new(5000m, 0m), new(5000m, 10m), new(null, 20m)]));

    [Fact]
    public void Last_bracket_must_be_open() =>
        Assert.Throws<DomainException>(() => Create(brackets: [new(5000m, 0m), new(15000m, 10m)]));

    [Fact]
    public void Only_the_last_bracket_may_be_open() =>
        Assert.Throws<DomainException>(() => Create(brackets: [new(null, 0m), new(null, 10m)]));

    [Fact]
    public void Zero_upper_bound_is_rejected() =>
        Assert.Throws<DomainException>(() => Create(brackets: [new(0m, 0m), new(null, 10m)]));

    [Fact]
    public void No_brackets_are_rejected() =>
        Assert.Throws<DomainException>(() => new PayrollPolicy(PolicyBuilder.Standard, new DateOnly(2026, 6, 1), 11m, null, [], PolicyBuilder.UtcNow));

    [Fact]
    public void Effective_from_must_be_the_first_of_a_month() =>
        Assert.Throws<DomainException>(() => Create(effectiveFrom: new DateOnly(2026, 6, 2)));

    [Theory]
    [InlineData(-1.0)]
    [InlineData(100.01)]
    public void Insurance_rate_must_be_a_percentage(double rate) =>
        Assert.Throws<DomainException>(() => Create(rate: (decimal)rate));

    [Fact]
    public void Tax_rate_must_be_a_percentage() =>
        Assert.Throws<DomainException>(() => Create(brackets: [new(5000m, 0m), new(null, 101m)]));

    [Fact]
    public void Negative_cap_is_rejected_and_zero_cap_is_allowed()
    {
        Assert.Throws<DomainException>(() => Create(cap: -1m));
        Assert.Equal(0m, Create(cap: 0m).InsuranceCap);
    }

    [Theory]
    [InlineData(2026, 6)] // the policy's own month: in effect
    [InlineData(2026, 7)] // a later month: past
    public void In_effect_or_past_policy_cannot_be_modified(int year, int month)
    {
        var policy = Create(effectiveFrom: new DateOnly(2026, 6, 1));
        var current = new PayrollMonth(year, month);

        Assert.Throws<DomainException>(() => policy.EnsureModifiable(current));
        Assert.Throws<DomainException>(() =>
            policy.Replace(PolicyBuilder.Standard, new DateOnly(2026, 9, 1), 10m, null, [new(null, 5m)], current));
    }

    [Fact]
    public void Future_policy_can_be_replaced_and_brackets_are_reused_by_position()
    {
        var policy = Create(effectiveFrom: new DateOnly(2026, 6, 1), brackets: [new(5000m, 0m), new(15000m, 10m), new(null, 20m)]);
        var firstBracket = policy.TaxBrackets[0];

        policy.Replace(PolicyBuilder.Standard, new DateOnly(2026, 7, 1), 12m, null, [new(6000m, 0m), new(null, 22m)],
            new PayrollMonth(2026, 5));

        Assert.Equal(new DateOnly(2026, 7, 1), policy.EffectiveFrom);
        Assert.Null(policy.InsuranceCap);
        Assert.Equal(2, policy.TaxBrackets.Count);
        Assert.Same(firstBracket, policy.TaxBrackets[0]);
        Assert.Equal((6000m, 0m), (policy.TaxBrackets[0].UpperBound!.Value, policy.TaxBrackets[0].RatePercent));
        Assert.Null(policy.TaxBrackets[1].UpperBound);
        Assert.Equal(22m, policy.TaxBrackets[1].RatePercent);
    }

    [Fact]
    public void Flat_scheme_needs_exactly_one_open_bracket()
    {
        var from = new DateOnly(2026, 6, 1);

        Assert.Single(new PayrollPolicy(PolicyBuilder.Flat, from, 11m, null, [new(null, 15m)], PolicyBuilder.UtcNow).TaxBrackets);
        Assert.Throws<DomainException>(() =>
            new PayrollPolicy(PolicyBuilder.Flat, from, 11m, null, [new(5000m, 0m), new(null, 15m)], PolicyBuilder.UtcNow));
        Assert.Throws<DomainException>(() => new PayrollPolicy(PolicyBuilder.Flat, from, 11m, null, [], PolicyBuilder.UtcNow));
    }

    [Fact]
    public void Exempt_scheme_has_no_brackets()
    {
        var from = new DateOnly(2026, 6, 1);

        Assert.Empty(new PayrollPolicy(PolicyBuilder.Exempt, from, 11m, 1500m, [], PolicyBuilder.UtcNow).TaxBrackets);
        Assert.Throws<DomainException>(() =>
            new PayrollPolicy(PolicyBuilder.Exempt, from, 11m, null, [new(null, 10m)], PolicyBuilder.UtcNow));
    }

    [Fact]
    public void Policy_cannot_move_to_another_scheme()
    {
        var policy = Create(effectiveFrom: new DateOnly(2026, 6, 1));

        Assert.Throws<DomainException>(() =>
            policy.Replace(PolicyBuilder.Flat, new DateOnly(2026, 6, 1), 11m, null, [new(null, 15m)], new PayrollMonth(2026, 5)));
    }
}
