using Payroll.Domain.Common;

namespace Payroll.Domain.Payroll;

/// <summary>A calendar month, written as "yyyy-MM".</summary>
public readonly record struct PayrollMonth : IComparable<PayrollMonth>
{
    public PayrollMonth(int year, int month)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            throw new DomainException($"{year:D4}-{month:D2} is not a valid month.");
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    /// <summary>Strict "yyyy-MM": four-digit year, two-digit month 01–12.</summary>
    public static bool TryParse(string? value, out PayrollMonth month)
    {
        month = default;
        if (value is not { Length: 7 } || value[4] != '-' ||
            !value.AsSpan(0, 4).ContainsOnlyDigits() || !value.AsSpan(5, 2).ContainsOnlyDigits())
        {
            return false;
        }

        var year = int.Parse(value.AsSpan(0, 4));
        var monthNumber = int.Parse(value.AsSpan(5, 2));
        if (year < 1 || monthNumber is < 1 or > 12)
        {
            return false;
        }

        month = new PayrollMonth(year, monthNumber);
        return true;
    }

    public static PayrollMonth From(DateOnly date) => new(date.Year, date.Month);

    public int CompareTo(PayrollMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    public override string ToString() => $"{Year:D4}-{Month:D2}";

    public static bool operator <(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) > 0;

    public static bool operator <=(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) <= 0;

    public static bool operator >=(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) >= 0;
}

internal static class SpanDigits
{
    public static bool ContainsOnlyDigits(this ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
