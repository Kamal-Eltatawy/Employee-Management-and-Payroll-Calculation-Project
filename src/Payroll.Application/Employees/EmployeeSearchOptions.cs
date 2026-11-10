using Payroll.Application.Abstractions;

namespace Payroll.Application.Employees;

/// <summary>The sort whitelist (D22) and the conversion from the HTTP request to the repository query.</summary>
public static class EmployeeSearchOptions
{
    public const int MaxPageSize = 100;

    public static readonly IReadOnlyDictionary<string, EmployeeSortBy> SortFields =
        new Dictionary<string, EmployeeSortBy>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = EmployeeSortBy.Name,
            ["hireDate"] = EmployeeSortBy.HireDate,
            ["salary"] = EmployeeSortBy.Salary,
        };

    public static readonly IReadOnlyDictionary<string, SortDirection> SortDirections =
        new Dictionary<string, SortDirection>(StringComparer.OrdinalIgnoreCase)
        {
            ["asc"] = SortDirection.Asc,
            ["desc"] = SortDirection.Desc,
        };

    public static EmployeeQuery ToQuery(EmployeeSearchRequest request) => new(
        string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
        request.DepartmentId,
        request.Status,
        request.SortBy is null ? EmployeeSortBy.Name : SortFields[request.SortBy],
        request.SortDir is null ? SortDirection.Asc : SortDirections[request.SortDir],
        request.Page,
        request.PageSize);
}
