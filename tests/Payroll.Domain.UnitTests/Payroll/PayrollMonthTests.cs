using Payroll.Domain.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Domain.UnitTests.Payroll;

public sealed class PayrollMonthTests
{
    [Fact]
    public void Parses_a_strict_year_month()
    {
        Assert.True(PayrollMonth.TryParse("2026-03", out var month));
        Assert.Equal(new PayrollMonth(2026, 3), month);
        Assert.Equal("2026-03", month.ToString());
        Assert.Equal(new DateOnly(2026, 3, 1), month.FirstDay);
        Assert.Equal(31, month.DaysInMonth);
    }

    [Theory]
    [InlineData("2026-3")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("2026/03")]
    [InlineData(" 2026-03")]
    [InlineData("0000-01")]
    public void Rejects_anything_else(string? value) => Assert.False(PayrollMonth.TryParse(value, out _));

    [Fact]
    public void Compares_chronologically()
    {
        Assert.True(new PayrollMonth(2025, 12) < new PayrollMonth(2026, 1));
        Assert.True(new PayrollMonth(2026, 2) >= new PayrollMonth(2026, 2));
        Assert.Equal(new PayrollMonth(2024, 2), PayrollMonth.From(new DateOnly(2024, 2, 29)));
    }

    [Fact]
    public void Leap_year_february_has_29_days() => Assert.Equal(29, new PayrollMonth(2024, 2).DaysInMonth);

    [Fact]
    public void Invalid_month_number_cannot_be_constructed() =>
        Assert.Throws<DomainException>(() => new PayrollMonth(2026, 13));
}
