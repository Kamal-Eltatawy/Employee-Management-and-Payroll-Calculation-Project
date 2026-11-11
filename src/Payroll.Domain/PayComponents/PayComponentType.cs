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

    /// <summary>
    /// Can change only while no employee uses the type (D71): payslips are not stored but recalculated from current data,
    /// so changing the kind of a used type would silently change past payslips. The caller checks the usage.
    /// </summary>
    public PayComponentKind Kind { get; private set; }

    /// <summary>Only an allowance can be taxable (D26).</summary>
    public bool IsTaxable { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary><paramref name="kind"/> null keeps the current kind.</summary>
    public void Update(string name, bool isTaxable, DateTime utcNow, string? nameAr = null, PayComponentKind? kind = null)
    {
        var previousKind = Kind;
        Kind = kind is { } newKind ? Guard.Defined(newKind, nameof(Kind)) : Kind;
        try
        {
            Apply(name, isTaxable, nameAr);
        }
        catch (DomainException)
        {
            Kind = previousKind; // a failed update leaves the type unchanged (D69)
            throw;
        }

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
