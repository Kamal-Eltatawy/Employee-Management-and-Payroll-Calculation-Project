using Payroll.Domain.Common;

namespace Payroll.Domain.Payroll;

/// <summary>Effective-dated tax brackets + social insurance settings of one tax scheme (D29/D30/D53). Append-only once in effect (D31).</summary>
public sealed class PayrollPolicy
{
    private readonly List<TaxBracket> _taxBrackets = [];

    public PayrollPolicy(TaxScheme taxScheme, DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
        IReadOnlyList<TaxBracketDefinition> taxBrackets, DateTime utcNow)
    {
        TaxSchemeId = taxScheme.Id;
        Apply(taxScheme, effectiveFrom, insuranceRatePercent, insuranceCap, taxBrackets);
        CreatedAtUtc = utcNow;
    }

    private PayrollPolicy()
    {
    }

    public int Id { get; private set; }

    public int TaxSchemeId { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public decimal InsuranceRatePercent { get; private set; }

    /// <summary>Null means no cap. A cap of 0 is a real cap (zero insurance).</summary>
    public decimal? InsuranceCap { get; private set; }

    public IReadOnlyList<TaxBracket> TaxBrackets => _taxBrackets.OrderBy(b => b.SortOrder).ToList();

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>The scheme is passed for its bracket rules; a policy never moves to another scheme.</summary>
    public void Replace(TaxScheme taxScheme, DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
        IReadOnlyList<TaxBracketDefinition> taxBrackets, PayrollMonth currentMonth)
    {
        if (taxScheme.Id != TaxSchemeId)
        {
            throw new DomainException("A payroll policy cannot move to another tax scheme.");
        }

        EnsureModifiable(currentMonth);
        Apply(taxScheme, effectiveFrom, insuranceRatePercent, insuranceCap, taxBrackets);
    }

    /// <summary>A policy already in effect (or in the past) must not change, so payslips stay reproducible (D31).</summary>
    public void EnsureModifiable(PayrollMonth currentMonth)
    {
        if (EffectiveFrom <= currentMonth.FirstDay)
        {
            throw new DomainException(
                $"The payroll policy effective from {EffectiveFrom:yyyy-MM-dd} is already in effect and cannot be changed.");
        }
    }

    private void Apply(TaxScheme taxScheme, DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
        IReadOnlyList<TaxBracketDefinition> taxBrackets)
    {
        if (effectiveFrom.Day != 1)
        {
            throw new DomainException("A payroll policy must take effect on the first day of a month.");
        }

        EnsurePercent(insuranceRatePercent, "Insurance rate");
        if (insuranceCap < 0)
        {
            throw new DomainException("Insurance cap cannot be negative.");
        }

        taxScheme.EnsureBracketsFit(taxBrackets);
        EnsureValidBrackets(taxBrackets);

        EffectiveFrom = effectiveFrom;
        InsuranceRatePercent = insuranceRatePercent;
        InsuranceCap = insuranceCap;
        ReplaceBrackets(taxBrackets);
    }

    private void ReplaceBrackets(IReadOnlyList<TaxBracketDefinition> definitions)
    {
        // Rows are updated in place by position, so the unique (PolicyId, SortOrder) index is never violated mid-save.
        var existing = _taxBrackets.OrderBy(b => b.SortOrder).ToList();
        for (var i = 0; i < definitions.Count; i++)
        {
            if (i < existing.Count)
            {
                existing[i].Change(definitions[i].UpperBound, definitions[i].RatePercent);
            }
            else
            {
                _taxBrackets.Add(new TaxBracket(i + 1, definitions[i].UpperBound, definitions[i].RatePercent));
            }
        }

        foreach (var extra in existing.Skip(definitions.Count))
        {
            _taxBrackets.Remove(extra);
        }
    }

    private static void EnsureValidBrackets(IReadOnlyList<TaxBracketDefinition> brackets)
    {
        decimal previousUpper = 0;
        for (var i = 0; i < brackets.Count; i++)
        {
            var bracket = brackets[i];
            EnsurePercent(bracket.RatePercent, "Tax rate");

            var isLast = i == brackets.Count - 1;
            if (isLast != (bracket.UpperBound is null))
            {
                throw new DomainException("Exactly the last tax bracket must be open-ended (no upper bound).");
            }

            if (bracket.UpperBound is { } upper)
            {
                if (upper <= previousUpper)
                {
                    throw new DomainException("Tax bracket upper bounds must be positive and strictly ascending.");
                }

                previousUpper = upper;
            }
        }
    }

    private static void EnsurePercent(decimal value, string field)
    {
        if (value is < 0 or > 100)
        {
            throw new DomainException($"{field} must be between 0 and 100.");
        }
    }
}
