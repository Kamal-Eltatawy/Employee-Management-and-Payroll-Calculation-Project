using Payroll.Domain.Employees;

namespace Payroll.Application.Abstractions;

public enum EmployeeSortBy
{
    Name,
    HireDate,
    Salary,
}

public enum SortDirection
{
    Asc,
    Desc,
}

public sealed record EmployeeQuery(
    string? Search, int? DepartmentId, EmployeeStatus? Status,
    EmployeeSortBy SortBy, SortDirection SortDirection, int Page, int PageSize);
