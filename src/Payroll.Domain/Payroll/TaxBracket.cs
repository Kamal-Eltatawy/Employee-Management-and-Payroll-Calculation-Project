namespace Payroll.Domain.Payroll;

/// <summary>One band of the progressive tax. Its lower bound is the previous band's upper bound (D27).</summary>
public sealed class TaxBracket
{
    internal TaxBracket(int sortOrder, decimal? upperBound, decimal ratePercent)
    {
        SortOrder = sortOrder;
        UpperBound = upperBound;
        RatePercent = ratePercent;
    }

    private TaxBracket()
    {
    }

    public int Id { get; private set; }

    public int PayrollPolicyId { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Null only for the last, open-ended band.</summary>
    public decimal? UpperBound { get; private set; }

    public decimal RatePercent { get; private set; }

    internal void Change(decimal? upperBound, decimal ratePercent)
    {
        UpperBound = upperBound;
        RatePercent = ratePercent;
    }
}

public sealed record TaxBracketDefinition(decimal? UpperBound, decimal RatePercent);
