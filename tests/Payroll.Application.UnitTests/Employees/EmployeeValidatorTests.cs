using FluentValidation.Results;
using Payroll.Application.Employees;
using Payroll.Domain.Employees;

namespace Payroll.Application.UnitTests.Employees;

public sealed class EmployeeValidatorTests
{
    private readonly CreateEmployeeRequestValidator _create = new(TestData.Clock());
    private readonly UpdateEmployeeRequestValidator _update = new(TestData.Clock());
    private readonly EmployeeSearchRequestValidator _search = new();

    private static void AssertError(ValidationResult result, string property) =>
        Assert.Contains(result.Errors, error => error.PropertyName == property);

    [Fact]
    public void Valid_request_passes()
    {
        var result = _create.Validate(TestData.CreateRequest(components: [new(1, 1000m), new(2, 99.5m)]));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Full_name_of_100_characters_passes_and_101_fails()
    {
        Assert.True(_create.Validate(TestData.CreateRequest(fullName: new string('a', 100))).IsValid);
        AssertError(_create.Validate(TestData.CreateRequest(fullName: new string('a', 101))), "FullName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Full_name_is_required(string? fullName) =>
        AssertError(_create.Validate(TestData.CreateRequest(fullName: fullName!)), "FullName");

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData(null)]
    public void Email_must_be_present_and_valid(string? email) =>
        AssertError(_create.Validate(TestData.CreateRequest(email: email!)), "Email");

    [Fact]
    public void Email_longer_than_254_fails() =>
        AssertError(_create.Validate(TestData.CreateRequest(email: new string('a', 250) + "@x.io")), "Email");

    [Fact]
    public void Job_title_is_required() =>
        AssertError(_create.Validate(TestData.CreateRequest() with { JobTitle = "" }), "JobTitle");

    [Fact]
    public void Tax_scheme_is_required() =>
        AssertError(_create.Validate(TestData.CreateRequest(taxSchemeId: 0)), "TaxSchemeId");

    [Fact]
    public void Department_is_required() =>
        AssertError(_create.Validate(TestData.CreateRequest(departmentId: 0)), "DepartmentId");

    [Fact]
    public void Hire_date_today_passes_and_tomorrow_fails()
    {
        Assert.True(_create.Validate(TestData.CreateRequest(hireDate: TestData.Today)).IsValid);
        AssertError(_create.Validate(TestData.CreateRequest(hireDate: TestData.Today.AddDays(1))), "HireDate");
    }

    [Fact]
    public void Missing_hire_date_fails() =>
        AssertError(_create.Validate(TestData.CreateRequest(hireDate: default(DateOnly))), "HireDate");

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100.123")]
    public void Salary_must_be_positive_with_at_most_two_decimals(string salary) =>
        AssertError(_create.Validate(TestData.CreateRequest(salary: decimal.Parse(salary))), "BaseMonthlySalary");

    [Fact]
    public void Salary_with_trailing_zeros_passes() =>
        Assert.True(_create.Validate(TestData.CreateRequest(salary: 100.500m)).IsValid);

    [Fact]
    public void Undefined_status_fails() =>
        AssertError(_create.Validate(TestData.CreateRequest(status: (EmployeeStatus)0)), "Status");

    [Fact]
    public void Pay_component_amount_must_be_positive_with_two_decimals()
    {
        var result = _create.Validate(TestData.CreateRequest(components: [new(1, 0m), new(2, 1.005m)]));

        AssertError(result, "PayComponents[0].Amount");
        AssertError(result, "PayComponents[1].Amount");
    }

    [Fact]
    public void Pay_component_type_is_required() =>
        AssertError(_create.Validate(TestData.CreateRequest(components: [new(0, 10m)])), "PayComponents[0].PayComponentTypeId");

    [Fact]
    public void Duplicate_pay_component_type_is_reported_on_the_repeated_item() =>
        AssertError(_create.Validate(TestData.CreateRequest(components: [new(1, 10m), new(2, 5m), new(1, 20m)])),
            "PayComponents[2].PayComponentTypeId");

    [Fact]
    public void Null_pay_components_fail() =>
        AssertError(_create.Validate(TestData.CreateRequest() with { PayComponents = null! }), "PayComponents");

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    public void Update_requires_a_valid_row_version(string rowVersion)
    {
        var create = TestData.CreateRequest();
        var request = new UpdateEmployeeRequest(create.FullName, create.Email, create.JobTitle, create.DepartmentId,
            create.TaxSchemeId, create.HireDate, create.BaseMonthlySalary, create.Status, [], rowVersion);

        AssertError(_update.Validate(request), "RowVersion");
    }

    [Fact]
    public void Update_applies_the_shared_rules()
    {
        var request = new UpdateEmployeeRequest("", "ali@demo.local", "Dev", 1, 1, TestData.Today, 1m,
            EmployeeStatus.Active, [], "AAAAAAAAB9E=");

        AssertError(_update.Validate(request), "FullName");
    }

    [Fact]
    public void Default_search_request_passes() =>
        Assert.True(_search.Validate(new EmployeeSearchRequest(null, null, null, null, null)).IsValid);

    [Theory]
    [InlineData("name", "asc")]
    [InlineData("hireDate", "desc")]
    [InlineData("SALARY", "DESC")]
    public void Whitelisted_sort_passes(string sortBy, string sortDir) =>
        Assert.True(_search.Validate(new EmployeeSearchRequest(null, null, null, sortBy, sortDir)).IsValid);

    [Fact]
    public void Unknown_sort_field_and_direction_fail()
    {
        var result = _search.Validate(new EmployeeSearchRequest(null, null, null, "email", "up"));

        AssertError(result, "SortBy");
        AssertError(result, "SortDir");
    }

    [Theory]
    [InlineData(0, 10, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public void Paging_out_of_range_fails(int page, int pageSize, string property) =>
        AssertError(_search.Validate(new EmployeeSearchRequest(null, null, null, null, null, page, pageSize)), property);

    [Fact]
    public void Page_size_of_100_passes() =>
        Assert.True(_search.Validate(new EmployeeSearchRequest(null, null, null, null, null, 1, 100)).IsValid);
}
