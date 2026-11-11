using FluentValidation;
using FluentValidation.Results;
using Mapster;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.PayComponents;

namespace Payroll.Application.PayComponents;

/// <summary>
/// <c>Name</c> is in the request language (D58); <c>NameEn</c> / <c>NameAr</c> are the stored values, for editing.
/// <c>IsInUse</c>: assigned to at least one employee (deleted ones too), so it cannot be deleted and its kind cannot change (D71).
/// </summary>
public sealed record PayComponentTypeResponse(int Id, string Name, PayComponentKind Kind, bool IsTaxable, string NameEn, string? NameAr)
{
    public bool IsInUse { get; init; }
}

/// <summary>Kind is set once on create (D2/S5).</summary>
public sealed record CreatePayComponentTypeRequest(string Name, PayComponentKind Kind, bool IsTaxable, string? NameAr = null);

/// <summary><c>Kind</c> is optional: null keeps it; a new kind is accepted only while no employee uses the type (D71).</summary>
public sealed record UpdatePayComponentTypeRequest(string Name, bool IsTaxable, string? NameAr = null, PayComponentKind? Kind = null);

public sealed class CreatePayComponentTypeRequestValidator : AbstractValidator<CreatePayComponentTypeRequest>
{
    public CreatePayComponentTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PayComponentType.NameMaxLength);
        RuleFor(x => x.NameAr).MaximumLength(PayComponentType.NameMaxLength);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.IsTaxable).Equal(false).When(x => x.Kind == PayComponentKind.Deduction)
            .WithMessage(PayComponentTypeService.DeductionNotTaxable).WithErrorCode(ErrorCodes.DeductionNotTaxable);
    }
}

public sealed class UpdatePayComponentTypeRequestValidator : AbstractValidator<UpdatePayComponentTypeRequest>
{
    public UpdatePayComponentTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PayComponentType.NameMaxLength);
        RuleFor(x => x.NameAr).MaximumLength(PayComponentType.NameMaxLength);
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.IsTaxable).Equal(false).When(x => x.Kind == PayComponentKind.Deduction)
            .WithMessage(PayComponentTypeService.DeductionNotTaxable).WithErrorCode(ErrorCodes.DeductionNotTaxable);
    }
}

public interface IPayComponentTypeService
{
    Task<IReadOnlyList<PayComponentTypeResponse>> ListAsync(CancellationToken ct);

    Task<PayComponentTypeResponse> GetAsync(int id, CancellationToken ct);

    Task<PayComponentTypeResponse> CreateAsync(CreatePayComponentTypeRequest request, CancellationToken ct);

    Task<PayComponentTypeResponse> UpdateAsync(int id, UpdatePayComponentTypeRequest request, CancellationToken ct);

    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class PayComponentTypeService(
    IPayComponentTypeRepository types,
    IUnitOfWork unitOfWork,
    IValidator<CreatePayComponentTypeRequest> createValidator,
    IValidator<UpdatePayComponentTypeRequest> updateValidator,
    TimeProvider clock,
    TypeAdapterConfig mapping) : IPayComponentTypeService
{
    public const string DeductionNotTaxable = "A deduction cannot be taxable.";

    public async Task<IReadOnlyList<PayComponentTypeResponse>> ListAsync(CancellationToken ct)
    {
        var used = await types.GetUsedIdsAsync(ct);
        return (await types.ListAsync(ct))
            .Select(t => t.Adapt<PayComponentTypeResponse>(mapping) with { IsInUse = used.Contains(t.Id) })
            .ToList();
    }

    public async Task<PayComponentTypeResponse> GetAsync(int id, CancellationToken ct)
    {
        var type = await LoadAsync(id, ct);
        return type.Adapt<PayComponentTypeResponse>(mapping) with { IsInUse = await types.IsInUseAsync(id, ct) };
    }

    public async Task<PayComponentTypeResponse> CreateAsync(CreatePayComponentTypeRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await EnsureNameAvailableAsync(request.Name, excludeId: null, ct);

        var type = new PayComponentType(request.Name, request.Kind, request.IsTaxable, clock.UtcNow(), request.NameAr);
        types.Add(type);
        await unitOfWork.SaveChangesAsync(ct);

        return type.Adapt<PayComponentTypeResponse>(mapping);
    }

    public async Task<PayComponentTypeResponse> UpdateAsync(int id, UpdatePayComponentTypeRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var type = await LoadAsync(id, ct);

        // Without a new kind in the request, the rule is checked against the stored kind.
        var kind = request.Kind ?? type.Kind;
        if (kind == PayComponentKind.Deduction && request.IsTaxable)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.IsTaxable), DeductionNotTaxable) { ErrorCode = ErrorCodes.DeductionNotTaxable }]);
        }

        // Payslips are recalculated from current data, not stored: a used type keeps its kind so past payslips stay the same (D71).
        var isInUse = await types.IsInUseAsync(id, ct);
        if (kind != type.Kind && isInUse)
        {
            throw ConflictException.Coded(ErrorCodes.PayComponentTypeKindInUse,
                $"Pay component type '{type.Name}' is assigned to at least one employee (including deleted employees), so its kind cannot change. Create a new pay item instead.",
                "kind", type.Name);
        }

        await EnsureNameAvailableAsync(request.Name, excludeId: id, ct);
        type.Update(request.Name, request.IsTaxable, clock.UtcNow(), request.NameAr, kind);
        await unitOfWork.SaveChangesAsync(ct);

        return type.Adapt<PayComponentTypeResponse>(mapping) with { IsInUse = isInUse };
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var type = await LoadAsync(id, ct);
        if (await types.IsInUseAsync(id, ct))
        {
            throw ConflictException.Coded(ErrorCodes.PayComponentTypeInUse,
                $"Pay component type '{type.Name}' is assigned to at least one employee (including deleted employees) and cannot be deleted.",
                field: null, type.Name);
        }

        types.Remove(type);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<PayComponentType> LoadAsync(int id, CancellationToken ct) =>
        await types.GetByIdAsync(id, ct) ?? throw new NotFoundException("Pay component type", id);

    private async Task EnsureNameAvailableAsync(string name, int? excludeId, CancellationToken ct)
    {
        if (await types.NameExistsAsync(name.Trim(), excludeId, ct))
        {
            throw ConflictException.Coded(ErrorCodes.PayComponentTypeNameTaken,
                $"A pay component type named '{name.Trim()}' already exists.", "name", name.Trim());
        }
    }
}
