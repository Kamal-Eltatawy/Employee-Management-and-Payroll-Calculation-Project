using FluentValidation;
using FluentValidation.Results;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;

namespace Payroll.Application.Employees;

/// <summary>Checks that the department, tax scheme and pay component types of an employee request exist (SRP, D62).</summary>
public interface IEmployeeReferenceChecker
{
    /// <summary>Throws a ValidationException with one field error per unknown reference (400, not a conflict).</summary>
    Task EnsureExistAsync(IEmployeeWriteRequest request, CancellationToken ct);
}

public sealed class EmployeeReferenceChecker(
    IDepartmentRepository departments,
    ITaxSchemeRepository taxSchemes,
    IPayComponentTypeRepository payComponentTypes) : IEmployeeReferenceChecker
{
    public async Task EnsureExistAsync(IEmployeeWriteRequest request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        if (!await departments.ExistsAsync(request.DepartmentId, ct))
        {
            failures.Add(new ValidationFailure(nameof(request.DepartmentId), "Department does not exist.") { ErrorCode = ErrorCodes.DepartmentNotFound });
        }

        if (!await taxSchemes.ExistsAsync(request.TaxSchemeId, ct))
        {
            failures.Add(new ValidationFailure(nameof(request.TaxSchemeId), "Tax scheme does not exist.") { ErrorCode = ErrorCodes.TaxSchemeNotFound });
        }

        var requestedIds = request.PayComponents.Select(c => c.PayComponentTypeId).Distinct().ToList();
        var knownIds = (await payComponentTypes.GetByIdsAsync(requestedIds, ct)).Select(t => t.Id).ToHashSet();
        for (var i = 0; i < request.PayComponents.Count; i++)
        {
            if (!knownIds.Contains(request.PayComponents[i].PayComponentTypeId))
            {
                failures.Add(new ValidationFailure(
                    $"{nameof(request.PayComponents)}[{i}].{nameof(PayComponentAssignmentDto.PayComponentTypeId)}",
                    "Pay component type does not exist.") { ErrorCode = ErrorCodes.PayComponentTypeNotFound });
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

}
