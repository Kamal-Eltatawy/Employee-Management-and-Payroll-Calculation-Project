using FluentValidation;
using FluentValidation.Results;
using Payroll.Application.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Payroll;

internal static class MonthParameter
{
    /// <summary>Parses the "month" query value. Missing → the current month; malformed → 400 on "month".</summary>
    public static PayrollMonth ParseOrCurrent(string? month, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return clock.CurrentMonth();
        }

        return PayrollMonth.TryParse(month, out var parsed)
            ? parsed
            : throw new ValidationException([new ValidationFailure("month", "Month must be in the format yyyy-MM.") { ErrorCode = ErrorCodes.MonthFormat }]);
    }
}
