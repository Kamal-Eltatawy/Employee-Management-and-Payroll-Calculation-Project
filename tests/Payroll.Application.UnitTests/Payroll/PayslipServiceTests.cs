using FluentValidation;
using Payroll.Application.Common;
using Payroll.Application.Employees;
using Payroll.Application.Payroll;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.Common;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Application.UnitTests.Payroll;

public sealed class PayslipServiceTests
{
    private readonly FakeDatabase _db = new();
    private readonly EmployeeService _employees;
    private readonly PayslipService _payslips;
    private readonly IPayrollCalculator _calculator =
        new PayrollCalculator([new ProgressiveTaxCalculator(), new FlatTaxCalculator(), new ExemptTaxCalculator()],
            new SocialInsuranceCalculator(), new CalendarDayProrationCalculator());
    private readonly int _transportId;
    private readonly int _housingId;
    private readonly int _loanId;
    private readonly TaxScheme _standard;
    private readonly TaxScheme _flat;
    private readonly TaxScheme _exempt;

    public PayslipServiceTests()
    {
        _db.Departments.Seed(1, "Engineering");
        _standard = _db.TaxSchemes.Seed(1, "Standard", TaxMethod.Progressive);
        _flat = _db.TaxSchemes.Seed(2, "Flat Rate", TaxMethod.Flat);
        _exempt = _db.TaxSchemes.Seed(3, "Tax Exempt", TaxMethod.Exempt);
        _transportId = _db.PayComponentTypes.Seed("Transport", PayComponentKind.Allowance).Id;
        _housingId = _db.PayComponentTypes.Seed("Housing", PayComponentKind.Allowance, isTaxable: true).Id;
        _loanId = _db.PayComponentTypes.Seed("Loan Repayment", PayComponentKind.Deduction).Id;

        var clock = TestData.Clock();
        var mapping = MappingConfig.Create();
        _employees = TestData.EmployeeService(_db, clock);
        _payslips = PayslipsFor(FakeCurrentUser.Hr());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PayslipService PayslipsFor(FakeCurrentUser user) =>
        new(_db.Employees, _db.PayrollPolicies, _calculator, user, TestData.Clock(), MappingConfig.Create());

    private async Task<int> CreateEmployeeAsync(DateOnly? hireDate = null, EmployeeStatus status = EmployeeStatus.Active,
        int taxSchemeId = 1) =>
        (await _employees.CreateAsync(TestData.CreateRequest(hireDate: hireDate, status: status, taxSchemeId: taxSchemeId,
            components: [new(_transportId, 1000m), new(_housingId, 2000m), new(_loanId, 750m)]), Ct)).Id;

    [Fact]
    public async Task Returns_the_spec_example_with_full_precision()
    {
        var policy = _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync();

        var payslip = await _payslips.GetAsync(id, "2026-03", Ct);

        Assert.Equal(("2026-03", policy.Id, new DateOnly(2025, 1, 1)), (payslip.Month, payslip.PolicyId, payslip.PolicyEffectiveFrom));
        Assert.Equal((1m, 31, 31), (payslip.ProrationFactor, payslip.DaysPaid, payslip.DaysInMonth));
        Assert.Equal((20000m, 19000m, 1800m, 1500m, 750m, 15950m),
            (payslip.GrossPay, payslip.TaxableIncome, payslip.IncomeTax, payslip.SocialInsurance, payslip.OtherDeductionsTotal, payslip.NetPay));
        Assert.Equal(new[] { "Transport", "Housing" }, payslip.Allowances.Select(a => a.Name).ToArray());
        Assert.False(payslip.IsNetNegative);
    }

    [Fact]
    public async Task Missing_month_means_the_current_month()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync();

        Assert.Equal("2026-03", (await _payslips.GetAsync(id, null, Ct)).Month);
    }

    [Theory]
    [InlineData("2026-3")]
    [InlineData("2026-13")]
    [InlineData("abc")]
    public async Task Malformed_month_is_a_validation_error_on_month(string month)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => _payslips.GetAsync(1, month, Ct));

        Assert.Equal("month", Assert.Single(error.Errors).PropertyName);
    }

    [Fact]
    public async Task Unknown_or_deleted_employee_is_not_found()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync();
        await _employees.DeleteAsync(id, Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => _payslips.GetAsync(id, "2026-03", Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _payslips.GetAsync(999, "2026-03", Ct));
    }

    [Fact]
    public async Task Inactive_employee_is_a_business_rule_error()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync(status: EmployeeStatus.Inactive);

        var error = await Assert.ThrowsAsync<DomainException>(() => _payslips.GetAsync(id, "2026-03", Ct));

        Assert.Equal("Employee is inactive.", error.Message);
    }

    [Fact]
    public async Task Month_before_hire_is_a_business_rule_error()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync(hireDate: new DateOnly(2026, 3, 10));

        var error = await Assert.ThrowsAsync<DomainException>(() => _payslips.GetAsync(id, "2026-02", Ct));

        Assert.Equal("Employee was hired in 2026-03.", error.Message);
    }

    [Fact]
    public async Task No_effective_policy_is_a_business_rule_error_checked_after_eligibility()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync(hireDate: new DateOnly(2020, 1, 1));

        var error = await Assert.ThrowsAsync<DomainException>(() => _payslips.GetAsync(id, "2024-12", Ct));

        Assert.Equal("No payroll policy is effective for 2024-12.", error.Message);
    }

    [Fact]
    public async Task The_latest_policy_effective_on_or_before_the_month_is_used()
    {
        var v1 = _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var v2 = _db.PayrollPolicies.Seed(new DateOnly(2026, 3, 1), 12m, 1800m, _standard);
        var v3 = _db.PayrollPolicies.Seed(new DateOnly(2027, 1, 1));
        var id = await CreateEmployeeAsync();

        Assert.Equal(v1.Id, (await _payslips.GetAsync(id, "2026-02", Ct)).PolicyId);
        Assert.Equal(v2.Id, (await _payslips.GetAsync(id, "2026-03", Ct)).PolicyId);
        Assert.Equal(v2.Id, (await _payslips.GetAsync(id, "2026-12", Ct)).PolicyId);
        Assert.Equal(v3.Id, (await _payslips.GetAsync(id, "2030-05", Ct)).PolicyId);
    }

    [Fact]
    public async Task Future_month_is_allowed_as_a_preview()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync();

        Assert.Equal(15950m, (await _payslips.GetAsync(id, "2027-06", Ct)).NetPay);
    }

    [Fact]
    public async Task Each_employee_gets_the_policy_and_tax_method_of_their_own_scheme()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1), scheme: _standard);
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1), scheme: _flat, brackets: [new(null, 15m)]);
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1), scheme: _exempt);
        var standard = await CreateEmployeeAsync(taxSchemeId: 1);
        var flat = await _employees.CreateAsync(TestData.CreateRequest(email: "flat@demo.local", taxSchemeId: 2,
            components: [new(_transportId, 1000m), new(_housingId, 2000m), new(_loanId, 750m)]), Ct);
        var exempt = await _employees.CreateAsync(TestData.CreateRequest(email: "exempt@demo.local", taxSchemeId: 3,
            components: [new(_transportId, 1000m), new(_housingId, 2000m), new(_loanId, 750m)]), Ct);

        var standardSlip = await _payslips.GetAsync(standard, "2026-03", Ct);
        var flatSlip = await _payslips.GetAsync(flat.Id, "2026-03", Ct);
        var exemptSlip = await _payslips.GetAsync(exempt.Id, "2026-03", Ct);

        Assert.Equal((TaxMethod.Progressive, 1800m, 15950m), (standardSlip.TaxMethod, standardSlip.IncomeTax, standardSlip.NetPay));
        Assert.Equal((TaxMethod.Flat, 2850m, 14900m), (flatSlip.TaxMethod, flatSlip.IncomeTax, flatSlip.NetPay));
        Assert.Equal((TaxMethod.Exempt, 0m, 17750m), (exemptSlip.TaxMethod, exemptSlip.IncomeTax, exemptSlip.NetPay));
        Assert.Equal("Tax Exempt", exemptSlip.TaxSchemeName);
    }

    [Fact]
    public async Task Scheme_without_an_effective_policy_is_a_business_rule_error()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1), scheme: _standard);
        var flat = await CreateEmployeeAsync(taxSchemeId: 2);

        await Assert.ThrowsAsync<DomainException>(() => _payslips.GetAsync(flat, "2026-03", Ct));
    }

    [Fact]
    public async Task Employee_can_read_their_own_payslip()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var id = await CreateEmployeeAsync();

        Assert.Equal(15950m, (await PayslipsFor(FakeCurrentUser.Employee(id)).GetAsync(id, "2026-03", Ct)).NetPay);
    }

    [Fact]
    public async Task Employee_requesting_another_employees_payslip_is_forbidden_even_if_it_does_not_exist()
    {
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1));
        var own = await CreateEmployeeAsync();
        var other = (await _employees.CreateAsync(TestData.CreateRequest(email: "other@demo.local"), Ct)).Id;
        var payslips = PayslipsFor(FakeCurrentUser.Employee(own));

        await Assert.ThrowsAsync<ForbiddenException>(() => payslips.GetAsync(other, "2026-03", Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => payslips.GetAsync(999, "2026-03", Ct));
    }
}
