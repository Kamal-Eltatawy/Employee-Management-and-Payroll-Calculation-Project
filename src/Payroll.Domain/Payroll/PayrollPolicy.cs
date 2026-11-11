using Payroll.Domain.Common;

namespace Payroll.Domain.Payroll;

/// <summary>
/// Effective-dated tax brackets + social insurance settings of one tax scheme (D29/D30/D53).
/// Past months are closed: a policy can change only while it starts in the current month or later (D31 as refined by D54).
/// </summary>
public sealed class PayrollPolicy
{
    private readonly List<TaxBracket> _taxBrackets = [];

    // Sorted view, built on first read. EF Core fills _taxBrackets in any order, so the order is fixed here, once.
    private IReadOnlyList<TaxBracket>? _sortedBrackets;

    /// <summary>
    /// A new policy can start only in the current month or later (D54): past months are closed.
    /// <paramref name="currentMonth"/> is "today" for the caller; historical data is created with the month it was entered in.
    /// </summary>
    public PayrollPolicy(TaxScheme taxScheme, DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
        IReadOnlyList<TaxBracketDefinition> taxBrackets, PayrollMonth currentMonth, DateTime utcNow)
    {
        EnsureNotInClosedMonth(effectiveFrom, currentMonth);
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

    public IReadOnlyList<TaxBracket> TaxBrackets =>
        _sortedBrackets is { } sorted && sorted.Count == _taxBrackets.Count
            ? sorted
            : _sortedBrackets = _taxBrackets.OrderBy(b => b.SortOrder).ToList().AsReadOnly();

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>The scheme is passed for its bracket rules; a policy never moves to another scheme.</summary>
    public void Replace(TaxScheme taxScheme, DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
        IReadOnlyList<TaxBracketDefinition> taxBrackets, PayrollMonth currentMonth)
    {
        if (taxScheme.Id != TaxSchemeId)
        {
            throw new DomainException(DomainErrorCodes.PolicyCannotMoveScheme, "A payroll policy cannot move to another tax scheme.");
        }

        EnsureModifiable(currentMonth);
        EnsureNotInClosedMonth(effectiveFrom, currentMonth);

        Apply(taxScheme, effectiveFrom, insuranceRatePercent, insuranceCap, taxBrackets);
    }

    /// <summary>
    /// The current month is still open, so a mistake can be corrected before payroll is paid at month end (D54).
    /// A policy that started in an earlier month has produced closed payslips and must not change (D31).
    /// </summary>
    public bool IsModifiable(PayrollMonth currentMonth) => EffectiveFrom >= currentMonth.FirstDay;

    public void EnsureModifiable(PayrollMonth currentMonth)
    {
        if (!IsModifiable(currentMonth))
        {
            throw new DomainException(DomainErrorCodes.PolicyCoversClosedMonths,
                $"The payroll policy effective from {EffectiveFrom:yyyy-MM-dd} covers closed months and cannot be changed.",
                EffectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static void EnsureNotInClosedMonth(DateOnly effectiveFrom, PayrollMonth currentMonth)
    {
        if (effectiveFrom < currentMonth.FirstDay)
        {
            throw new DomainException(DomainErrorCodes.PolicyClosedMonth,
                "A payroll policy can only take effect from the current month or later; past months are closed.");
        }
    }

    private void Apply(TaxScheme taxScheme, DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
        IReadOnlyList<TaxBracketDefinition> taxBrackets)
    {
        if (effectiveFrom.Day != 1)
        {
            throw new DomainException(DomainErrorCodes.PolicyFirstDay, "A payroll policy must take effect on the first day of a month.");
        }

        Guard.Percent(insuranceRatePercent, nameof(InsuranceRatePercent));
        if (insuranceCap is { } cap)
        {
            if (cap < 0)
            {
                throw new DomainException(DomainErrorCodes.InsuranceCapNegative, "Insurance cap cannot be negative.");
            }

            Guard.MaxDecimals(cap, nameof(InsuranceCap));
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
        _sortedBrackets = null;
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
            Guard.Percent(bracket.RatePercent, nameof(TaxBracket.RatePercent));

            var isLast = i == brackets.Count - 1;
            if (isLast && bracket.UpperBound is not null)
            {
                throw new DomainException(DomainErrorCodes.BracketLastOpen, "The last tax bracket must be open-ended (no upper bound).");
            }

            if (!isLast && bracket.UpperBound is null)
            {
                throw new DomainException(DomainErrorCodes.BracketOnlyLastOpen, "Only the last tax bracket can be open-ended.");
            }

            if (bracket.UpperBound is { } upper)
            {
                if (upper <= previousUpper)
                {
                    throw new DomainException(DomainErrorCodes.BracketAscending, "Tax bracket upper bounds must be positive and strictly ascending.");
                }

                Guard.MaxDecimals(upper, nameof(TaxBracket.UpperBound));
                previousUpper = upper;
            }
        }
    }
}
