using Payroll.Domain.Common;

namespace Payroll.Domain.Payroll;

public readonly record struct ProrationResult(decimal Factor, int DaysPaid, int DaysInMonth);

public interface IProrationCalculator
{
    ProrationResult Calculate(DateOnly hireDate, PayrollMonth month);
}

/// <summary>Hire-month proration by calendar days; the hire day counts (D35). Full precision, no rounding.</summary>
public sealed class CalendarDayProrationCalculator : IProrationCalculator
{
    public ProrationResult Calculate(DateOnly hireDate, PayrollMonth month)
    {
        var hireMonth = PayrollMonth.From(hireDate);
        if (month < hireMonth)
        {
            throw new DomainException(DomainErrorCodes.HiredAfterMonth, $"Employee was hired in {hireMonth}.", hireMonth.ToString());
        }

        if (month > hireMonth)
        {
            return new ProrationResult(1m, month.DaysInMonth, month.DaysInMonth);
        }

        var daysPaid = month.DaysInMonth - hireDate.Day + 1;
        return new ProrationResult((decimal)daysPaid / month.DaysInMonth, daysPaid, month.DaysInMonth);
    }
}
