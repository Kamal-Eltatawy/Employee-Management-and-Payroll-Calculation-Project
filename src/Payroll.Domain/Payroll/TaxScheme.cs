using Payroll.Domain.Common;

namespace Payroll.Domain.Payroll;

/// <summary>How income tax is computed for a scheme (D53). Each value has an <see cref="ITaxCalculator"/> implementation.</summary>
public enum TaxMethod
{
    /// <summary>Monthly progressive brackets.</summary>
    Progressive = 1,

    /// <summary>One rate on the whole taxable income (a single open-ended bracket holds the rate).</summary>
    Flat = 2,

    /// <summary>No income tax. Social insurance still applies.</summary>
    Exempt = 3,
}

/// <summary>A tax regime an employee belongs to (D53). Read-only master data; each scheme has its own effective-dated policies.</summary>
public sealed class TaxScheme
{
    public const int NameMaxLength = 100;

    public TaxScheme(int id, string name, TaxMethod method, string? nameAr = null)
    {
        NameAr = Guard.OptionalText(nameAr, NameMaxLength, nameof(NameAr));
        Id = Guard.PositiveId(id, nameof(Id));
        Name = Guard.RequiredText(name, NameMaxLength, nameof(Name));
        Method = Guard.Defined(method, nameof(Method));
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Optional Arabic name; the API falls back to <see cref="Name"/> (D58).</summary>
    public string? NameAr { get; private set; }

    public TaxMethod Method { get; private set; }

    /// <summary>The bracket shape each method needs, or null when it fits. Generic bracket rules live in <see cref="PayrollPolicy"/>.</summary>
    public string? DescribeBracketShapeProblem(int bracketCount) => Method switch
    {
        TaxMethod.Progressive when bracketCount == 0 => "A progressive tax needs at least one tax bracket.",
        TaxMethod.Flat when bracketCount != 1 => "A flat tax needs exactly one open-ended tax bracket holding its rate.",
        TaxMethod.Exempt when bracketCount != 0 => "A tax-exempt scheme has no tax brackets.",
        _ => null,
    };

    public void EnsureBracketsFit(IReadOnlyList<TaxBracketDefinition> brackets)
    {
        if (DescribeBracketShapeProblem(brackets.Count) is { } problem)
        {
            throw new DomainException($"{DomainErrorCodes.BracketShape}.{Method}", problem);
        }
    }
}
