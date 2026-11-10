using FluentValidation;
using Payroll.Application.Common;
using Payroll.Domain.Employees;

namespace Payroll.Application.Employees;

/// <summary>Input rules shared by create and update. Existence checks (department, pay component types) run in the service.</summary>
public abstract class EmployeeWriteRequestValidator<T> : AbstractValidator<T>
    where T : IEmployeeWriteRequest
{
    protected EmployeeWriteRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(Employee.FullNameMaxLength);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(Employee.EmailMaxLength).EmailAddress();
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(Employee.JobTitleMaxLength);
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Department is required.");
        RuleFor(x => x.TaxSchemeId).GreaterThan(0).WithMessage("Tax scheme is required.");
        RuleFor(x => x.HireDate)
            .NotEmpty()
            .Must(hireDate => hireDate <= clock.Today()).WithMessage("Hire date cannot be in the future.");
        RuleFor(x => x.BaseMonthlySalary).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Status).IsInEnum();

        RuleFor(x => x.PayComponents).NotNull();
        RuleForEach(x => x.PayComponents).NotNull().ChildRules(component =>
        {
            component.RuleFor(c => c.PayComponentTypeId).GreaterThan(0).WithMessage("Pay component type is required.");
            component.RuleFor(c => c.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        });
        RuleFor(x => x.PayComponents).Custom((components, context) =>
        {
            if (components is null)
            {
                return;
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < components.Count; i++)
            {
                if (components[i] is { } component && !seen.Add(component.PayComponentTypeId))
                {
                    context.AddFailure(
                        $"{nameof(IEmployeeWriteRequest.PayComponents)}[{i}].{nameof(PayComponentAssignmentDto.PayComponentTypeId)}",
                        "This pay component type is already assigned.");
                }
            }
        });
    }
}

public sealed class CreateEmployeeRequestValidator(TimeProvider clock)
    : EmployeeWriteRequestValidator<CreateEmployeeRequest>(clock);

public sealed class UpdateEmployeeRequestValidator : EmployeeWriteRequestValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator(TimeProvider clock)
        : base(clock)
    {
        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .Must(BeBase64).WithMessage("Row version is not valid.");
    }

    private static bool BeBase64(string? value) =>
        value is not null && Convert.TryFromBase64String(value, new byte[value.Length], out _);
}

public sealed class EmployeeSearchRequestValidator : AbstractValidator<EmployeeSearchRequest>
{
    public EmployeeSearchRequestValidator()
    {
        RuleFor(x => x.Search).MaximumLength(Employee.EmailMaxLength);
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.SortBy)
            .Must(value => value is null || EmployeeSearchOptions.SortFields.ContainsKey(value))
            .WithMessage("Sort by must be one of: name, hireDate, salary.");
        RuleFor(x => x.SortDir)
            .Must(value => value is null || EmployeeSearchOptions.SortDirections.ContainsKey(value))
            .WithMessage("Sort direction must be asc or desc.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, EmployeeSearchOptions.MaxPageSize);
    }
}
