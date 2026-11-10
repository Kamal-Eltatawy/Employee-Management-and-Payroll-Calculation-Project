using FluentValidation;
using FluentValidation.Results;
using Mapster;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.PayComponents;

namespace Payroll.Application.PayComponents;

/// <summary><c>Name</c> is in the request language (D58); <c>NameEn</c> / <c>NameAr</c> are the stored values, for editing.</summary>
public sealed record PayComponentTypeResponse(int Id, string Name, PayComponentKind Kind, bool IsTaxable, string NameEn, string? NameAr);

/// <summary>Kind is set once on create (D2/S5).</summary>
public sealed record CreatePayComponentTypeRequest(string Name, PayComponentKind Kind, bool IsTaxable, string? NameAr = null);

/// <summary>No Kind field: the kind cannot change (S5).</summary>
public sealed record UpdatePayComponentTypeRequest(string Name, bool IsTaxable, string? NameAr = null);

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

    public async Task<IReadOnlyList<PayComponentTypeResponse>> ListAsync(CancellationToken ct) =>
        (await types.ListAsync(ct)).Select(t => t.Adapt<PayComponentTypeResponse>(mapping)).ToList();

    public async Task<PayComponentTypeResponse> GetAsync(int id, CancellationToken ct) =>
        (await LoadAsync(id, ct)).Adapt<PayComponentTypeResponse>(mapping);

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

        // The kind is stored, not sent, so this rule can only be checked against the loaded type.
        if (type.Kind == PayComponentKind.Deduction && request.IsTaxable)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.IsTaxable), DeductionNotTaxable) { ErrorCode = ErrorCodes.DeductionNotTaxable }]);
        }

        await EnsureNameAvailableAsync(request.Name, excludeId: id, ct);
        type.Update(request.Name, request.IsTaxable, clock.UtcNow(), request.NameAr);
        await unitOfWork.SaveChangesAsync(ct);

        return type.Adapt<PayComponentTypeResponse>(mapping);
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
