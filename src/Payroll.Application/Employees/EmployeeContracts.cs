using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;

namespace Payroll.Application.Employees;

public sealed record PayComponentAssignmentDto(int PayComponentTypeId, decimal Amount);

/// <summary>The fields shared by the create and update requests, so both use the same validation rules.</summary>
public interface IEmployeeWriteRequest
{
    string FullName { get; }

    string Email { get; }

    string JobTitle { get; }

    int DepartmentId { get; }

    int TaxSchemeId { get; }

    DateOnly HireDate { get; }

    decimal BaseMonthlySalary { get; }

    EmployeeStatus Status { get; }

    IReadOnlyList<PayComponentAssignmentDto> PayComponents { get; }
}

public sealed record CreateEmployeeRequest(
    string FullName, string Email, string JobTitle, int DepartmentId, int TaxSchemeId, DateOnly HireDate,
    decimal BaseMonthlySalary, EmployeeStatus Status, IReadOnlyList<PayComponentAssignmentDto> PayComponents)
    : IEmployeeWriteRequest;

public sealed record UpdateEmployeeRequest(
    string FullName, string Email, string JobTitle, int DepartmentId, int TaxSchemeId, DateOnly HireDate,
    decimal BaseMonthlySalary, EmployeeStatus Status, IReadOnlyList<PayComponentAssignmentDto> PayComponents,
    string RowVersion)
    : IEmployeeWriteRequest;

public sealed record EmployeeSearchRequest(
    string? Search, int? DepartmentId, EmployeeStatus? Status,
    string? SortBy, string? SortDir, int Page = 1, int PageSize = 10);

public sealed record EmployeePayComponentResponse(
    int PayComponentTypeId, string Name, PayComponentKind Kind, bool IsTaxable, decimal Amount);

public sealed record EmployeeResponse(
    int Id, string FullName, string Email, string JobTitle, int DepartmentId, string DepartmentName,
    int TaxSchemeId, string TaxSchemeName, DateOnly HireDate, decimal BaseMonthlySalary, EmployeeStatus Status,
    IReadOnlyList<EmployeePayComponentResponse> PayComponents, string RowVersion);

public sealed record EmployeeListItem(
    int Id, string FullName, string Email, string JobTitle, string DepartmentName,
    DateOnly HireDate, decimal BaseMonthlySalary, EmployeeStatus Status);
