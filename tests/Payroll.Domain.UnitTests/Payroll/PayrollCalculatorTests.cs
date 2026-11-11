using Payroll.Domain.Common;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Domain.UnitTests.Payroll;

public sealed class PayrollCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 3, 31);
    private static readonly PayComponentType Transport = PolicyBuilder.Type(1, "Transport", PayComponentKind.Allowance);
    private static readonly PayComponentType Housing = PolicyBuilder.Type(2, "Housing", PayComponentKind.Allowance, isTaxable: true);
    private static readonly PayComponentType Loan = PolicyBuilder.Type(3, "Loan Repayment", PayComponentKind.Deduction);

    private readonly PayrollCalculator _calculator =
        new([new ProgressiveTaxCalculator(), new FlatTaxCalculator(), new ExemptTaxCalculator()],
            new SocialInsuranceCalculator(), new CalendarDayProrationCalculator());

    private static Employee SpecEmployee(DateOnly hireDate, EmployeeStatus status = EmployeeStatus.Active, int taxSchemeId = 1)
    {
        var employee = new Employee("Ali Hassan", "ali@demo.local", "Developer", 1, taxSchemeId, hireDate, 17000m, status, Today,
            PolicyBuilder.UtcNow);
        employee.SetPayComponents([new(1, 1000m), new(2, 2000m), new(3, 750m)], PolicyBuilder.UtcNow);
        return PolicyBuilder.WithTypes(employee, Transport, Housing, Loan);
    }

    // Decimal keeps 28–29 significant digits; the spec values come from arbitrary precision.
    private static void AssertClose(decimal expected, decimal actual) =>
        Assert.True(Math.Abs(expected - actual) < 0.000000000000000001m, $"Expected {expected}, got {actual}.");

    [Fact]
    public void Spec_example_full_month()
    {
        var payslip = _calculator.Calculate(SpecEmployee(new DateOnly(2024, 6, 1)), PolicyBuilder.V1(), new PayrollMonth(2026, 3));

        Assert.Equal(1m, payslip.Proration.Factor);
        Assert.Equal(17000m, payslip.BaseSalary);
        Assert.Equal(20000m, payslip.GrossPay);
        Assert.Equal(19000m, payslip.TaxableIncome);
        Assert.Equal(1800m, payslip.IncomeTax);
        Assert.Equal(1500m, payslip.SocialInsurance);
        Assert.Equal(750m, payslip.OtherDeductionsTotal);
        Assert.Equal(15950m, payslip.NetPay);
        Assert.False(payslip.IsNetNegative);
        Assert.Equal(new[] { "Transport", "Housing" }, payslip.Allowances.Select(a => a.Name).ToArray());
        Assert.Equal("Loan Repayment", Assert.Single(payslip.Deductions).Name);
    }

    [Fact]
    public void Spec_example_hired_on_march_20()
    {
        var payslip = _calculator.Calculate(SpecEmployee(new DateOnly(2026, 3, 20)), PolicyBuilder.V1(), new PayrollMonth(2026, 3));

        Assert.Equal((12, 31), (payslip.Proration.DaysPaid, payslip.Proration.DaysInMonth));
        AssertClose(6580.645161290322580645161290m, payslip.BaseSalary);
        AssertClose(7741.935483870967741935483871m, payslip.GrossPay);
        AssertClose(7354.838709677419354838709677m, payslip.TaxableIncome);
        AssertClose(235.4838709677419354838709677m, payslip.IncomeTax);
        AssertClose(580.6451612903225806451612903m, payslip.SocialInsurance);
        Assert.Equal(750m, payslip.OtherDeductionsTotal);
        AssertClose(6175.806451612903225806451613m, payslip.NetPay);
        Assert.Equal(6175.81m, Math.Round(payslip.NetPay, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Zero_allowances_tax_the_base_only()
    {
        var employee = new Employee("Nour", "nour@demo.local", "QA", 1, 1, new DateOnly(2025, 7, 1), 8000m,
            EmployeeStatus.Active, Today, PolicyBuilder.UtcNow);

        var payslip = _calculator.Calculate(PolicyBuilder.WithTypes(employee), PolicyBuilder.V1(), new PayrollMonth(2026, 3));

        Assert.Empty(payslip.Allowances);
        Assert.Equal(8000m, payslip.GrossPay);
        Assert.Equal(300m, payslip.IncomeTax);
        Assert.Equal(880m, payslip.SocialInsurance);
        Assert.Equal(6820m, payslip.NetPay);
    }

    [Fact]
    public void Exempt_allowance_is_in_gross_but_not_in_taxable_income()
    {
        var employee = new Employee("A", "a@demo.local", "Dev", 1, 1, new DateOnly(2025, 1, 1), 10000m,
            EmployeeStatus.Active, Today, PolicyBuilder.UtcNow);
        employee.SetPayComponents([new(1, 1000m), new(2, 1000m)], PolicyBuilder.UtcNow);
        PolicyBuilder.WithTypes(employee, Transport, Housing);

        var payslip = _calculator.Calculate(employee, PolicyBuilder.V1(), new PayrollMonth(2026, 3));

        Assert.Equal(12000m, payslip.GrossPay);
        Assert.Equal(11000m, payslip.TaxableIncome);
    }

    [Fact]
    public void Deductions_above_earnings_give_a_negative_net_flag()
    {
        var employee = new Employee("L", "l@demo.local", "Sales", 1, 1, new DateOnly(2026, 3, 20), 7000m,
            EmployeeStatus.Active, Today, PolicyBuilder.UtcNow);
        employee.SetPayComponents([new(3, 6000m)], PolicyBuilder.UtcNow);
        PolicyBuilder.WithTypes(employee, Loan);

        var payslip = _calculator.Calculate(employee, PolicyBuilder.V1(), new PayrollMonth(2026, 3));

        Assert.True(payslip.NetPay < 0);
        Assert.True(payslip.IsNetNegative);
    }

    [Fact]
    public void Inactive_employee_is_not_eligible() =>
        Assert.Equal("Employee is inactive.", Assert.Throws<DomainException>(() =>
            _calculator.Calculate(SpecEmployee(new DateOnly(2024, 6, 1), EmployeeStatus.Inactive), PolicyBuilder.V1(),
                new PayrollMonth(2026, 3))).Message);

    [Fact]
    public void Month_before_hire_month_is_not_eligible() =>
        Assert.Equal("Employee was hired in 2026-03.", Assert.Throws<DomainException>(() =>
            _calculator.Calculate(SpecEmployee(new DateOnly(2026, 3, 20)), PolicyBuilder.V1(), new PayrollMonth(2026, 2))).Message);

    [Fact]
    public void Flat_scheme_taxes_the_whole_taxable_income_at_one_rate()
    {
        var policy = new PayrollPolicy(PolicyBuilder.Flat, new DateOnly(2025, 1, 1), 11m, 1500m, [new(null, 15m)], PolicyBuilder.Entered, PolicyBuilder.UtcNow);

        var payslip = _calculator.Calculate(SpecEmployee(new DateOnly(2024, 6, 1), taxSchemeId: 2), policy, new PayrollMonth(2026, 3));

        Assert.Equal(("Flat Rate", TaxMethod.Flat), (payslip.TaxSchemeName, payslip.TaxMethod));
        Assert.Equal(2850m, payslip.IncomeTax);   // 19,000 × 15%
        Assert.Equal(14900m, payslip.NetPay);     // 20,000 − 2,850 − 1,500 − 750
    }

    [Fact]
    public void Exempt_scheme_pays_no_income_tax_but_still_insurance()
    {
        var policy = new PayrollPolicy(PolicyBuilder.Exempt, new DateOnly(2025, 1, 1), 11m, 1500m, [], PolicyBuilder.Entered, PolicyBuilder.UtcNow);

        var payslip = _calculator.Calculate(SpecEmployee(new DateOnly(2024, 6, 1), taxSchemeId: 3), policy, new PayrollMonth(2026, 3));

        Assert.Equal(0m, payslip.IncomeTax);
        Assert.Equal(1500m, payslip.SocialInsurance);
        Assert.Equal(17750m, payslip.NetPay);
    }

    [Fact]
    public void Policy_of_another_scheme_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() =>
            _calculator.Calculate(SpecEmployee(new DateOnly(2024, 6, 1), taxSchemeId: 2), PolicyBuilder.V1(), new PayrollMonth(2026, 3)));

    [Fact]
    public void Missing_calculator_for_a_method_is_reported()
    {
        var progressiveOnly = new PayrollCalculator([new ProgressiveTaxCalculator()], new SocialInsuranceCalculator(),
            new CalendarDayProrationCalculator());
        var policy = new PayrollPolicy(PolicyBuilder.Exempt, new DateOnly(2025, 1, 1), 11m, null, [], PolicyBuilder.Entered, PolicyBuilder.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            progressiveOnly.Calculate(SpecEmployee(new DateOnly(2024, 6, 1), taxSchemeId: 3), policy, new PayrollMonth(2026, 3)));
    }
}
