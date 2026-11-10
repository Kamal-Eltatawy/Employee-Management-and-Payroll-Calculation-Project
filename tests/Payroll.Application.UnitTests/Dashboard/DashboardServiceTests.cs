using System.Globalization;
using Payroll.Application.Common;
using Payroll.Application.Dashboard;
using Payroll.Application.Employees;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Application.UnitTests.Dashboard;

public sealed class DashboardServiceTests
{
    private readonly FakeDatabase _db = new();
    private readonly EmployeeService _employees;
    private readonly DashboardService _dashboard;
    private readonly int _transportId;
    private readonly int _housingId;
    private readonly int _loanId;

    public DashboardServiceTests()
    {
        _db.Departments.Seed(1, "Engineering");
        _db.Departments.Seed(2, "Finance");
        var standard = _db.TaxSchemes.Seed(1, "Standard", TaxMethod.Progressive);
        _db.TaxSchemes.Seed(3, "Tax Exempt", TaxMethod.Exempt);
        _transportId = _db.PayComponentTypes.Seed("Transport", PayComponentKind.Allowance).Id;
        _housingId = _db.PayComponentTypes.Seed("Housing", PayComponentKind.Allowance, isTaxable: true).Id;
        _loanId = _db.PayComponentTypes.Seed("Loan Repayment", PayComponentKind.Deduction).Id;
        _db.PayrollPolicies.Seed(new DateOnly(2025, 1, 1), scheme: standard);

        var clock = TestData.Clock(); // 2026-03-15
        _employees = TestData.EmployeeService(_db, clock);
        _dashboard = new DashboardService(_db.Employees, _db.PayrollPolicies,
            new PayrollCalculator([new ProgressiveTaxCalculator(), new FlatTaxCalculator(), new ExemptTaxCalculator()],
                new SocialInsuranceCalculator(), new CalendarDayProrationCalculator()),
            clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<EmployeeResponse> CreateAsync(string email, int departmentId = 1, int taxSchemeId = 1,
        EmployeeStatus status = EmployeeStatus.Active, DateOnly? hireDate = null) =>
        _employees.CreateAsync(TestData.CreateRequest(email: email, departmentId: departmentId, taxSchemeId: taxSchemeId,
            status: status, hireDate: hireDate,
            components: [new(_transportId, 1000m), new(_housingId, 2000m), new(_loanId, 750m)]), Ct);

    [Fact]
    public async Task Counts_employees_and_totals_the_current_month_payroll()
    {
        await CreateAsync("a@demo.local");                                        // spec example: net 15,950
        await CreateAsync("b@demo.local", departmentId: 2);                       // same pay: net 15,950
        await CreateAsync("c@demo.local", status: EmployeeStatus.Inactive);       // not paid
        await CreateAsync("d@demo.local", taxSchemeId: 3);                        // Exempt scheme has no policy
        await CreateAsync("e@demo.local", hireDate: new DateOnly(2026, 3, 1));    // hired this month, full month
        var deleted = await CreateAsync("f@demo.local");
        await _employees.DeleteAsync(deleted.Id, Ct);

        var dashboard = await _dashboard.GetAsync(Ct);

        Assert.Equal(("2026-03", 5, 4, 1, 1),
            (dashboard.Month, dashboard.TotalEmployees, dashboard.ActiveEmployees, dashboard.InactiveEmployees, dashboard.HiredThisMonth));
        Assert.Equal(new CountItem("Engineering", 3), dashboard.ByDepartment[0]);
        Assert.Equal(new CountItem("Finance", 1), dashboard.ByDepartment[1]);
        Assert.Equal(3, dashboard.Payroll.EmployeesPaid);
        Assert.Equal(1, dashboard.Payroll.WithoutPolicy);
        Assert.Equal(60000m, dashboard.Payroll.GrossPay);
        Assert.Equal(3 * 15950m, dashboard.Payroll.NetPay);
        Assert.Equal(0, dashboard.Payroll.NegativeNetCount);
    }

    [Fact]
    public async Task Empty_company_has_zero_totals()
    {
        var dashboard = await _dashboard.GetAsync(Ct);

        Assert.Equal((0, 0, 0m), (dashboard.TotalEmployees, dashboard.Payroll.EmployeesPaid, dashboard.Payroll.NetPay));
        Assert.Empty(dashboard.ByDepartment);
    }
}

public sealed class LocalizedNameTests
{
    [Fact]
    public void Arabic_requests_get_the_arabic_name_when_there_is_one()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            Assert.Equal("الهندسة", LocalizedName.Pick("Engineering", "الهندسة"));
            Assert.Equal("Engineering", LocalizedName.Pick("Engineering", null));

            CultureInfo.CurrentUICulture = new CultureInfo("en");
            Assert.Equal("Engineering", LocalizedName.Pick("Engineering", "الهندسة"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
