using Payroll.Domain.Common;
using Payroll.Domain.Departments;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;
using Payroll.Domain.UnitTests.Payroll;

namespace Payroll.Domain.UnitTests;

/// <summary>Rules added by the pre-submission review (D69): error codes, atomic updates, closed months, precision.</summary>
public sealed class ReviewRulesTests
{
    private static readonly DateOnly Today = new(2026, 3, 15);
    private static readonly DateTime UtcNow = new(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);

    private static Employee NewEmployee(decimal salary = 17000m, EmployeeStatus status = EmployeeStatus.Active,
        DateOnly? hireDate = null) =>
        new("Ali Hassan", "ali@demo.local", "Developer", 1, 1, hireDate ?? new DateOnly(2024, 6, 1), salary, status, Today, UtcNow);

    private static DomainException Fails(Action action) => Assert.Throws<DomainException>(action);

    // ---- Error codes carry the field as a translatable argument ----

    [Fact]
    public void Required_text_has_a_code_and_the_field_name()
    {
        var error = Fails(() => new Employee(" ", "a@b.c", "Dev", 1, 1, new DateOnly(2024, 1, 1), 1000m, EmployeeStatus.Active, Today, UtcNow));

        Assert.Equal(DomainErrorCodes.Required, error.Code);
        Assert.Equal(nameof(Employee.FullName), Assert.Single(error.Arguments));
    }

    [Fact]
    public void Too_long_text_has_the_field_and_the_limit()
    {
        var error = Fails(() => new Department(1, new string('x', Department.NameMaxLength + 1)));

        Assert.Equal(DomainErrorCodes.MaxLength, error.Code);
        Assert.Equal(new object[] { nameof(Department.Name), Department.NameMaxLength }, error.Arguments);
    }

    [Fact]
    public void Non_positive_salary_has_a_code() =>
        Assert.Equal(DomainErrorCodes.MustBePositive, Fails(() => NewEmployee(salary: 0m)).Code);

    [Fact]
    public void Hire_date_in_the_future_has_a_code() =>
        Assert.Equal(DomainErrorCodes.HireDateInFuture, Fails(() => NewEmployee(hireDate: Today.AddDays(1))).Code);

    [Fact]
    public void Duplicate_pay_component_type_has_a_code() =>
        Assert.Equal(DomainErrorCodes.DuplicatePayComponentType,
            Fails(() => NewEmployee().SetPayComponents([new(1, 10m), new(1, 20m)], UtcNow)).Code);

    [Fact]
    public void Taxable_deduction_has_a_code() =>
        Assert.Equal(DomainErrorCodes.DeductionNotTaxable,
            Fails(() => new PayComponentType("Loan", PayComponentKind.Deduction, true, UtcNow)).Code);

    // ---- Decimal precision matches the database (decimal(18,2) / decimal(5,2)) ----

    [Fact]
    public void Money_with_more_than_two_decimals_is_rejected()
    {
        var error = Fails(() => NewEmployee(salary: 1000.123m));

        Assert.Equal(DomainErrorCodes.MaxDecimals, error.Code);
        Assert.Equal(new object[] { nameof(Employee.BaseMonthlySalary), 2 }, error.Arguments);
        Assert.Equal(1000.12m, NewEmployee(salary: 1000.12m).BaseMonthlySalary);
        Assert.Equal(DomainErrorCodes.MaxDecimals, Fails(() => NewEmployee().SetPayComponents([new(1, 10.001m)], UtcNow)).Code);
    }

    [Fact]
    public void Rates_caps_and_bounds_with_more_than_two_decimals_are_rejected()
    {
        Assert.Equal(DomainErrorCodes.MaxDecimals, Fails(() => Policy(rate: 11.005m)).Code);
        Assert.Equal(DomainErrorCodes.MaxDecimals, Fails(() => Policy(cap: 1500.555m)).Code);
        Assert.Equal(DomainErrorCodes.MaxDecimals, Fails(() => Policy(brackets: [new(5000.001m, 0m), new(null, 10m)])).Code);
        Assert.Equal(DomainErrorCodes.MaxDecimals, Fails(() => Policy(brackets: [new(5000m, 0m), new(null, 10.125m)])).Code);
    }

    // ---- Employee updates are atomic and a deleted employee is read-only ----

    [Fact]
    public void Failed_update_leaves_the_employee_unchanged()
    {
        var employee = NewEmployee();

        Fails(() => employee.UpdateDetails("New Name", "new@demo.local", "Lead", 2, 2, new DateOnly(2024, 6, 1), -1m,
            EmployeeStatus.Active, Today, UtcNow.AddDays(1)));

        Assert.Equal(("Ali Hassan", "ali@demo.local", "Developer", 1, 1, 17000m),
            (employee.FullName, employee.Email, employee.JobTitle, employee.DepartmentId, employee.TaxSchemeId, employee.BaseMonthlySalary));
        Assert.Equal(UtcNow, employee.UpdatedAtUtc);
    }

    [Fact]
    public void Deleted_employee_cannot_be_changed()
    {
        var employee = NewEmployee();
        employee.MarkDeleted(UtcNow);

        Assert.Equal(DomainErrorCodes.EmployeeDeleted, Fails(() => employee.UpdateDetails("Ali", "ali@demo.local", "Dev", 1, 1,
            new DateOnly(2024, 6, 1), 17000m, EmployeeStatus.Active, Today, UtcNow)).Code);
        Assert.Equal(DomainErrorCodes.EmployeeDeleted, Fails(() => employee.SetPayComponents([], UtcNow)).Code);
        Assert.Equal(DomainErrorCodes.EmployeeDeleted, Fails(() => employee.MarkDeleted(UtcNow)).Code);
    }

    [Fact]
    public void Eligibility_for_a_payslip()
    {
        var hired = NewEmployee(hireDate: new DateOnly(2026, 2, 10));

        hired.EnsureEligibleForPayslip(new PayrollMonth(2026, 2));
        var early = Fails(() => hired.EnsureEligibleForPayslip(new PayrollMonth(2026, 1)));
        Assert.Equal((DomainErrorCodes.HiredAfterMonth, "2026-02"), (early.Code, (string)Assert.Single(early.Arguments)));
        Assert.Equal(DomainErrorCodes.EmployeeInactive,
            Fails(() => NewEmployee(status: EmployeeStatus.Inactive).EnsureEligibleForPayslip(new PayrollMonth(2026, 3))).Code);
    }

    // ---- PayrollMonth ----

    [Fact]
    public void Default_month_is_not_a_valid_month()
    {
        var month = default(PayrollMonth);

        Assert.True(month.IsDefault);
        Assert.Equal(DomainErrorCodes.InvalidMonth, Fails(() => _ = month.FirstDay).Code);
        Assert.Equal(DomainErrorCodes.InvalidMonth, Fails(() => _ = month.DaysInMonth).Code);
        Assert.False(new PayrollMonth(2026, 3).IsDefault);
    }

    // ---- PayrollPolicy ----

    private static PayrollPolicy Policy(DateOnly? effectiveFrom = null, decimal rate = 11m, decimal? cap = 1500m,
        TaxBracketDefinition[]? brackets = null, PayrollMonth? currentMonth = null) =>
        new(PolicyBuilder.Standard, effectiveFrom ?? new DateOnly(2026, 6, 1), rate, cap,
            brackets ?? [new(5000m, 0m), new(null, 10m)], currentMonth ?? new PayrollMonth(2026, 3), UtcNow);

    [Fact]
    public void New_policy_cannot_start_in_a_closed_month()
    {
        Assert.Equal(DomainErrorCodes.PolicyClosedMonth,
            Fails(() => Policy(effectiveFrom: new DateOnly(2026, 2, 1), currentMonth: new PayrollMonth(2026, 3))).Code);
        Assert.Equal(new DateOnly(2026, 3, 1), Policy(effectiveFrom: new DateOnly(2026, 3, 1)).EffectiveFrom);
    }

    [Fact]
    public void Policy_rules_have_codes()
    {
        Assert.Equal(DomainErrorCodes.PolicyFirstDay, Fails(() => Policy(effectiveFrom: new DateOnly(2026, 6, 2))).Code);
        Assert.Equal(DomainErrorCodes.PercentRange, Fails(() => Policy(rate: 101m)).Code);
        Assert.Equal(DomainErrorCodes.InsuranceCapNegative, Fails(() => Policy(cap: -1m)).Code);
        Assert.Equal(DomainErrorCodes.BracketLastOpen, Fails(() => Policy(brackets: [new(5000m, 0m), new(9000m, 10m)])).Code);
        Assert.Equal(DomainErrorCodes.BracketOnlyLastOpen, Fails(() => Policy(brackets: [new(null, 0m), new(null, 10m)])).Code);
        Assert.Equal(DomainErrorCodes.BracketAscending, Fails(() => Policy(brackets: [new(9000m, 0m), new(5000m, 5m), new(null, 10m)])).Code);
        Assert.Equal("Policy.BracketShape.Progressive", Fails(() => Policy(brackets: [])).Code);

        var policy = Policy();
        Assert.Equal(DomainErrorCodes.PolicyCannotMoveScheme, Fails(() =>
            policy.Replace(PolicyBuilder.Flat, new DateOnly(2026, 6, 1), 11m, null, [new(null, 15m)], new PayrollMonth(2026, 3))).Code);
    }

    [Fact]
    public void Tax_brackets_are_sorted_once_and_refreshed_after_a_replace()
    {
        var policy = Policy(brackets: [new(5000m, 0m), new(15000m, 10m), new(null, 20m)]);

        Assert.Same(policy.TaxBrackets, policy.TaxBrackets);

        policy.Replace(PolicyBuilder.Standard, new DateOnly(2026, 7, 1), 11m, null, [new(null, 5m)], new PayrollMonth(2026, 3));
        Assert.Equal(5m, Assert.Single(policy.TaxBrackets).RatePercent);
    }

    // ---- TaxScheme and Arabic names ----

    [Fact]
    public void Tax_scheme_guards()
    {
        Assert.Equal(DomainErrorCodes.Required, Fails(() => new TaxScheme(1, " ", TaxMethod.Flat)).Code);
        Assert.Equal(DomainErrorCodes.Required, Fails(() => new TaxScheme(0, "Flat", TaxMethod.Flat)).Code);
        Assert.Equal(DomainErrorCodes.InvalidValue, Fails(() => new TaxScheme(1, "Odd", (TaxMethod)99)).Code);
        Assert.Null(PolicyBuilder.Standard.DescribeBracketShapeProblem(3));
        Assert.NotNull(PolicyBuilder.Exempt.DescribeBracketShapeProblem(1));
        Assert.Equal("Policy.BracketShape.Flat", Fails(() => PolicyBuilder.Flat.EnsureBracketsFit([])).Code);
    }

    [Fact]
    public void Arabic_names_are_optional_and_trimmed()
    {
        Assert.Null(new Department(1, "IT", "  ").NameAr);
        Assert.Equal("تقنية", new Department(1, "IT", " تقنية ").NameAr);
        Assert.Equal("ثابت", new TaxScheme(2, "Flat", TaxMethod.Flat, "ثابت").NameAr);
        Assert.Equal("مواصلات", new PayComponentType("Transport", PayComponentKind.Allowance, false, UtcNow, "مواصلات").NameAr);
        Assert.Equal(DomainErrorCodes.MaxLength, Fails(() => new Department(1, "IT", new string('ع', Department.NameMaxLength + 1))).Code);
        Assert.Equal(DomainErrorCodes.Required, Fails(() => new Department(0, "IT")).Code);
    }

    // ---- Calculator cases that were not covered ----

    [Fact]
    public void Progressive_tax_with_a_non_zero_first_bracket()
    {
        var brackets = PolicyBuilder.Brackets(new TaxBracketDefinition(5000m, 5m), new TaxBracketDefinition(null, 10m));

        // 5,000 × 5% + 3,000 × 10% = 250 + 300
        Assert.Equal(550m, new ProgressiveTaxCalculator().Calculate(8000m, brackets));
    }

    [Fact]
    public void Mid_month_hire_below_the_prorated_cap_pays_the_plain_rate()
    {
        var calculator = new PayrollCalculator([new ProgressiveTaxCalculator(), new FlatTaxCalculator(), new ExemptTaxCalculator()],
            new SocialInsuranceCalculator(), new CalendarDayProrationCalculator());
        var employee = PolicyBuilder.WithTypes(new Employee("Nour", "nour@demo.local", "QA", 1, 1, new DateOnly(2026, 3, 17), 6000m,
            EmployeeStatus.Active, new DateOnly(2026, 3, 31), UtcNow));

        var payslip = calculator.Calculate(employee, PolicyBuilder.V1(), new PayrollMonth(2026, 3));

        var factor = 15m / 31m;
        Assert.Equal((15, 31), (payslip.Proration.DaysPaid, payslip.Proration.DaysInMonth));
        Assert.Equal(6000m * factor, payslip.GrossPay);
        Assert.True(payslip.SocialInsurance < 1500m * factor);
        Assert.Equal(payslip.GrossPay * 11m / 100m, payslip.SocialInsurance);
    }
}
