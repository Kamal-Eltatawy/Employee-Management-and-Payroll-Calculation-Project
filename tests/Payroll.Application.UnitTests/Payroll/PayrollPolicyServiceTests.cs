using FluentValidation;
using Payroll.Application.Common;
using Payroll.Application.Payroll;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Application.UnitTests.Payroll;

public sealed class PayrollPolicyServiceTests
{
    private readonly FakeDatabase _db = new();
    private readonly PayrollPolicyService _service;

    public PayrollPolicyServiceTests()
    {
        _db.TaxSchemes.Seed(1, "Standard", TaxMethod.Progressive);
        _db.TaxSchemes.Seed(2, "Flat Rate", TaxMethod.Flat);
        _db.TaxSchemes.Seed(3, "Tax Exempt", TaxMethod.Exempt);
        var clock = TestData.Clock(); // today: 2026-03-15
        _service = new PayrollPolicyService(_db.PayrollPolicies, _db.TaxSchemes, _db.UnitOfWork,
            new PayrollPolicyRequestValidator(clock), clock);
    }

    private static PayrollPolicyRequest Request(DateOnly effectiveFrom, int taxSchemeId = 1, params TaxBracketDto[] brackets) =>
        new(taxSchemeId, effectiveFrom, 12m, 1800m,
            brackets.Length > 0 ? brackets : new TaxBracketDto[] { new(6000m, 0m), new(18000m, 10m), new(null, 22m) });

    private static void AssertField(ValidationException error, string property) =>
        Assert.Contains(error.Errors, failure => failure.PropertyName == property);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_returns_policies_with_brackets_and_modifiability()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2027, 1, 1), 12m, 1800m, null);
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        _db.PayrollPolicies.Seed(new DateOnly(2026, 3, 1)); // the current month: still open (D54)

        var policies = await _service.ListAsync(Ct);

        Assert.Equal(new[] { new DateOnly(2025, 1, 1), new DateOnly(2026, 3, 1), new DateOnly(2027, 1, 1) },
            policies.Select(p => p.EffectiveFrom).ToArray());
        Assert.Equal(new[] { false, true, true }, policies.Select(p => p.IsModifiable).ToArray());
        Assert.Equal(new TaxBracketDto(5000m, 0m), policies[0].TaxBrackets[0]);
        Assert.Null(policies[0].TaxBrackets[^1].UpperBound);
    }

    [Fact]
    public async Task Get_unknown_policy_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(9, Ct));

    [Fact]
    public async Task Effective_returns_the_policy_for_the_month_or_a_business_rule_error()
    {
        var v1 = _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));

        Assert.Equal(v1.Id, (await _service.GetEffectiveAsync(1, "2026-03", Ct)).Id);
        Assert.Equal(v1.Id, (await _service.GetEffectiveAsync(1, null, Ct)).Id);
        Assert.Equal(1, (await _service.GetEffectiveAsync(1, null, Ct)).TaxSchemeId);
        await Assert.ThrowsAsync<DomainException>(() => _service.GetEffectiveAsync(1, "2024-12", Ct));
        await Assert.ThrowsAsync<DomainException>(() => _service.GetEffectiveAsync(2, "2026-03", Ct));
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetEffectiveAsync(1, "2024-1", Ct));
    }

    [Fact]
    public async Task Create_for_a_future_month_returns_a_modifiable_policy()
    {
        var created = await _service.CreateAsync(Request(new DateOnly(2026, 4, 1)), Ct);

        Assert.True(created.IsModifiable);
        Assert.Equal(3, created.TaxBrackets.Count);
        Assert.Equal(1, _db.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_for_the_current_month_is_allowed_to_correct_it() =>
        Assert.True((await _service.CreateAsync(Request(new DateOnly(2026, 3, 1)), Ct)).IsModifiable);

    [Theory]
    [InlineData(2026, 2, 1)]  // the previous month: closed
    [InlineData(2025, 6, 1)]
    public async Task Create_for_a_past_month_is_rejected(int year, int month, int day)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request(new DateOnly(year, month, day)), Ct));

        AssertField(error, "EffectiveFrom");
    }

    [Fact]
    public async Task Create_not_on_the_first_of_a_month_is_rejected() =>
        AssertField(await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request(new DateOnly(2026, 4, 2)), Ct)), "EffectiveFrom");

    [Fact]
    public async Task Bracket_rules_are_reported_on_the_bracket()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(
            Request(new DateOnly(2026, 4, 1), 1, new(15000m, 0m), new(5000m, 10m), new(30000m, 20m)), Ct));

        AssertField(error, "TaxBrackets[1].UpperBound");
        AssertField(error, "TaxBrackets[2].UpperBound");
    }

    [Fact]
    public async Task Bracket_shape_must_fit_the_scheme_method()
    {
        var flatWithBands = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request(new DateOnly(2026, 4, 1), 2), Ct));
        var flat = await _service.CreateAsync(Request(new DateOnly(2026, 4, 1), 2, new TaxBracketDto(null, 15m)), Ct);
        var exempt = await _service.CreateAsync(new PayrollPolicyRequest(3, new DateOnly(2026, 4, 1), 11m, null, []), Ct);

        AssertField(flatWithBands, "TaxBrackets");
        Assert.Equal(2, flat.TaxSchemeId);
        Assert.Empty(exempt.TaxBrackets);
    }

    [Fact]
    public async Task Unknown_scheme_is_a_validation_error() =>
        AssertField(await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(Request(new DateOnly(2026, 4, 1), 9), Ct)), "TaxSchemeId");

    [Fact]
    public async Task Same_effective_date_in_the_same_scheme_is_a_conflict_but_another_scheme_is_fine()
    {
        await _service.CreateAsync(Request(new DateOnly(2026, 4, 1)), Ct);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request(new DateOnly(2026, 4, 1)), Ct));
        var otherScheme = await _service.CreateAsync(Request(new DateOnly(2026, 4, 1), 2, new TaxBracketDto(null, 15m)), Ct);

        Assert.True(conflict.Errors.ContainsKey("effectiveFrom"));
        Assert.Equal(2, otherScheme.TaxSchemeId);
    }

    [Fact]
    public async Task Update_and_delete_of_a_policy_covering_closed_months_are_conflicts()
    {
        var inEffect = _db.PayrollPolicies.Seed(new DateOnly(2026, 2, 1));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(inEffect.Id, Request(new DateOnly(2026, 5, 1)), Ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(inEffect.Id, Ct));
    }

    [Fact]
    public async Task Future_policy_can_be_updated_and_deleted()
    {
        var created = await _service.CreateAsync(Request(new DateOnly(2026, 4, 1)), Ct);

        var updated = await _service.UpdateAsync(created.Id,
            Request(new DateOnly(2026, 5, 1), 1, new(7000m, 0m), new(null, 15m)), Ct);
        await _service.DeleteAsync(created.Id, Ct);

        Assert.Equal((new DateOnly(2026, 5, 1), 2), (updated.EffectiveFrom, updated.TaxBrackets.Count));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(created.Id, Ct));
    }

    [Fact]
    public async Task Update_cannot_move_a_policy_to_another_scheme()
    {
        var created = await _service.CreateAsync(Request(new DateOnly(2026, 4, 1)), Ct);

        AssertField(await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(created.Id, Request(new DateOnly(2026, 4, 1), 2, new TaxBracketDto(null, 15m)), Ct)), "TaxSchemeId");
    }
}
