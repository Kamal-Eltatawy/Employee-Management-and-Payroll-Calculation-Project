using Payroll.Domain.Payroll;

namespace Payroll.Application.Common;

public static class ClockExtensions
{
    /// <summary>"Today" is the server's local date (D15).</summary>
    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public static PayrollMonth CurrentMonth(this TimeProvider clock) => PayrollMonth.From(clock.Today());

    public static DateTime UtcNow(this TimeProvider clock) => clock.GetUtcNow().UtcDateTime;
}
