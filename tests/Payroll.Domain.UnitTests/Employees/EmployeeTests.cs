using Payroll.Domain.Common;
using Payroll.Domain.Employees;

namespace Payroll.Domain.UnitTests.Employees;

public sealed class EmployeeTests
{
    private static readonly DateOnly Today = new(2026, 3, 15);
    private static readonly DateTime UtcNow = new(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);

    private static Employee NewEmployee(
        string fullName = "Ali Hassan", string email = "ali@demo.local", string jobTitle = "Developer",
        int departmentId = 1, DateOnly? hireDate = null, decimal salary = 17000m,
        EmployeeStatus status = EmployeeStatus.Active, int taxSchemeId = 1) =>
        new(fullName, email, jobTitle, departmentId, taxSchemeId, hireDate ?? new DateOnly(2024, 6, 1), salary, status, Today, UtcNow);

    [Fact]
    public void Constructor_trims_text_and_normalises_email()
    {
        var employee = NewEmployee(fullName: "  Ali Hassan ", email: "  Ali.Hassan@Demo.LOCAL ", jobTitle: " Developer ");

        Assert.Equal("Ali Hassan", employee.FullName);
        Assert.Equal("ali.hassan@demo.local", employee.Email);
        Assert.Equal("Developer", employee.JobTitle);
        Assert.Equal(UtcNow, employee.CreatedAtUtc);
        Assert.Null(employee.DeletedAtUtc);
    }

    [Fact]
    public void NormalizeEmail_trims_and_lower_cases() =>
        Assert.Equal("a@b.com", Employee.NormalizeEmail("  A@B.Com  "));

    [Fact]
    public void Full_name_of_exactly_max_length_is_accepted() =>
        Assert.Equal(100, NewEmployee(fullName: new string('a', 100)).FullName.Length);

    [Theory]
    [InlineData("blank name")]
    [InlineData("long name")]
    [InlineData("blank email")]
    [InlineData("long email")]
    [InlineData("blank job title")]
    [InlineData("long job title")]
    [InlineData("no department")]
    [InlineData("no tax scheme")]
    [InlineData("future hire date")]
    [InlineData("zero salary")]
    [InlineData("negative salary")]
    [InlineData("undefined status")]
    public void Constructor_guards_throw_domain_exception(string violation) =>
        Assert.Throws<DomainException>(() => violation switch
        {
            "blank name" => NewEmployee(fullName: " "),
            "long name" => NewEmployee(fullName: new string('a', 101)),
            "blank email" => NewEmployee(email: ""),
            "long email" => NewEmployee(email: new string('a', 250) + "@x.io"),
            "blank job title" => NewEmployee(jobTitle: ""),
            "long job title" => NewEmployee(jobTitle: new string('a', 101)),
            "no department" => NewEmployee(departmentId: 0),
            "no tax scheme" => NewEmployee(taxSchemeId: 0),
            "future hire date" => NewEmployee(hireDate: Today.AddDays(1)),
            "zero salary" => NewEmployee(salary: 0m),
            "negative salary" => NewEmployee(salary: -1m),
            "undefined status" => NewEmployee(status: (EmployeeStatus)99),
            _ => throw new ArgumentOutOfRangeException(nameof(violation)),
        });

    [Fact]
    public void Hire_date_today_is_accepted() =>
        Assert.Equal(Today, NewEmployee(hireDate: Today).HireDate);

    [Fact]
    public void UpdateDetails_applies_the_same_guards()
    {
        var employee = NewEmployee();

        Assert.Throws<DomainException>(() => employee.UpdateDetails(
            "Ali", "ali@demo.local", "Dev", 1, 1, Today.AddDays(1), 1000m, EmployeeStatus.Active, Today, UtcNow));
    }

    [Fact]
    public void UpdateDetails_changes_values_and_timestamp()
    {
        var employee = NewEmployee();
        var later = UtcNow.AddHours(1);

        employee.UpdateDetails("Ali H", "NEW@demo.local", "Lead", 2, 3, new DateOnly(2024, 1, 1), 20000m,
            EmployeeStatus.Inactive, Today, later);

        Assert.Equal("new@demo.local", employee.Email);
        Assert.Equal(2, employee.DepartmentId);
        Assert.Equal(3, employee.TaxSchemeId);
        Assert.Equal(EmployeeStatus.Inactive, employee.Status);
        Assert.Equal(later, employee.UpdatedAtUtc);
    }

    [Fact]
    public void SetPayComponents_rejects_duplicate_types()
    {
        var employee = NewEmployee();

        Assert.Throws<DomainException>(() => employee.SetPayComponents([new(1, 100m), new(1, 200m)], UtcNow));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public void SetPayComponents_rejects_invalid_assignments(int typeId, decimal amount)
    {
        var employee = NewEmployee();

        Assert.Throws<DomainException>(() => employee.SetPayComponents([new(typeId, amount)], UtcNow));
    }

    [Fact]
    public void SetPayComponents_replaces_the_set_and_keeps_existing_rows()
    {
        var employee = NewEmployee();
        employee.SetPayComponents([new(1, 100m), new(2, 200m)], UtcNow);
        var kept = employee.PayComponents.Single(c => c.PayComponentTypeId == 1);

        employee.SetPayComponents([new(1, 150m), new(3, 300m)], UtcNow);

        Assert.Equal(new[] { 1, 3 }, employee.PayComponents.Select(c => c.PayComponentTypeId).Order().ToArray());
        Assert.Same(kept, employee.PayComponents.Single(c => c.PayComponentTypeId == 1));
        Assert.Equal(150m, kept.Amount);
    }

    [Fact]
    public void SetPayComponents_with_an_invalid_item_leaves_the_set_unchanged()
    {
        var employee = NewEmployee();
        employee.SetPayComponents([new(1, 100m)], UtcNow);

        Assert.Throws<DomainException>(() => employee.SetPayComponents([new(2, 50m), new(3, 0m)], UtcNow));

        Assert.Equal(1, Assert.Single(employee.PayComponents).PayComponentTypeId);
    }

    [Fact]
    public void MarkDeleted_sets_deleted_timestamp_once()
    {
        var employee = NewEmployee();

        employee.MarkDeleted(UtcNow);

        Assert.Equal(UtcNow, employee.DeletedAtUtc);
        Assert.Throws<DomainException>(() => employee.MarkDeleted(UtcNow));
    }
}
