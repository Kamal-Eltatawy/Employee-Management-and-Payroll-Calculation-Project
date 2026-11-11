using Payroll.Domain.Common;

namespace Payroll.Domain.PayComponents;

public sealed class PayComponentType
{
    public const int NameMaxLength = 100;

    public PayComponentType(string name, PayComponentKind kind, bool isTaxable, DateTime utcNow, string? nameAr = null)
    {
        Kind = Guard.Defined(kind, nameof(Kind));
        Name = string.Empty;
        Apply(name, isTaxable, nameAr);
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    private PayComponentType()
    {
        Name = string.Empty;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Optional Arabic name; the API falls back to <see cref="Name"/> (D58).</summary>
    public string? NameAr { get; private set; }

    /// <summary>Immutable after creation (D2).</summary>
    public PayComponentKind Kind { get; private set; }

    /// <summary>Only an allowance can be taxable (D26).</summary>
    public bool IsTaxable { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string name, bool isTaxable, DateTime utcNow, string? nameAr = null)
    {
        Apply(name, isTaxable, nameAr);
        UpdatedAtUtc = utcNow;
    }

    private void Apply(string name, bool isTaxable, string? nameAr)
    {
        if (Kind == PayComponentKind.Deduction && isTaxable)
        {
            throw new DomainException(DomainErrorCodes.DeductionNotTaxable, "A deduction cannot be taxable.");
        }

        Name = Guard.RequiredText(name, NameMaxLength, nameof(Name));
        IsTaxable = isTaxable;
        NameAr = Guard.OptionalText(nameAr, NameMaxLength, nameof(NameAr));
    }
}
