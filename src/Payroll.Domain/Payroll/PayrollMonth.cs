using Payroll.Domain.Common;

namespace Payroll.Domain.Payroll;

/// <summary>A calendar month, written as "yyyy-MM".</summary>
public readonly record struct PayrollMonth : IComparable<PayrollMonth>
{
    public PayrollMonth(int year, int month)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            throw InvalidMonth($"{year:D4}-{month:D2}");
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    /// <summary><c>default(PayrollMonth)</c> skips the constructor (year 0, month 0); it is not a valid month.</summary>
    public bool IsDefault => Month == 0;

    public DateOnly FirstDay => IsDefault ? throw InvalidMonth(ToString()) : new DateOnly(Year, Month, 1);

    public int DaysInMonth => IsDefault ? throw InvalidMonth(ToString()) : DateTime.DaysInMonth(Year, Month);

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

    private static DomainException InvalidMonth(string value) =>
        new(DomainErrorCodes.InvalidMonth, $"{value} is not a valid month.", value);

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
