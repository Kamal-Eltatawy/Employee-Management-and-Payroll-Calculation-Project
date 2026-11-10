using FluentValidation;
using Payroll.Application.Common;
using Payroll.Application.Payroll;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.Common;

namespace Payroll.Application.UnitTests.Payroll;

public sealed class PayrollPolicyServiceTests
{
    private readonly FakeDatabase _db = new();
    private readonly PayrollPolicyService _service;

    public PayrollPolicyServiceTests() => _service = new PayrollPolicyService(_db.PayrollPolicies, TestData.Clock());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_returns_policies_with_brackets_and_modifiability()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2027, 1, 1), 12m, 1800m, null);
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        _db.PayrollPolicies.Seed(new DateOnly(2026, 3, 1)); // the current month: in effect

        var policies = await _service.ListAsync(Ct);

        Assert.Equal(new[] { new DateOnly(2025, 1, 1), new DateOnly(2026, 3, 1), new DateOnly(2027, 1, 1) },
            policies.Select(p => p.EffectiveFrom).ToArray());
        Assert.Equal(new[] { false, false, true }, policies.Select(p => p.IsModifiable).ToArray());
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
}
