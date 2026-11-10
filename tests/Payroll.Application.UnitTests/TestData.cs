using Microsoft.Extensions.Time.Testing;
using Payroll.Application.Employees;
using Payroll.Domain.Employees;

namespace Payroll.Application.UnitTests;

internal static class TestData
{
    /// <summary>"Today" for all application tests: 2026-03-15.</summary>
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));

    public static readonly DateOnly Today = new(2026, 3, 15);

    public static CreateEmployeeRequest CreateRequest(
        string fullName = "Ali Hassan", string email = "ali@demo.local", int departmentId = 1,
        DateOnly? hireDate = null, decimal salary = 17000m, EmployeeStatus status = EmployeeStatus.Active,
        int taxSchemeId = 1, params PayComponentAssignmentDto[] components) =>
        new(fullName, email, "Developer", departmentId, taxSchemeId, hireDate ?? new DateOnly(2024, 6, 1), salary, status,
            components);

    public static UpdateEmployeeRequest UpdateRequest(EmployeeResponse current, string? email = null, string? rowVersion = null,
        params PayComponentAssignmentDto[] components) =>
        new(current.FullName, email ?? current.Email, current.JobTitle, current.DepartmentId, current.TaxSchemeId, current.HireDate,
            current.BaseMonthlySalary, current.Status, components, rowVersion ?? current.RowVersion);
}
