using Payroll.Domain.Common;
using Payroll.Domain.Departments;
using Payroll.Domain.PayComponents;

namespace Payroll.Domain.UnitTests.PayComponents;

public sealed class PayComponentTypeTests
{
    private static readonly DateTime UtcNow = new(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Taxable_deduction_is_rejected() =>
        Assert.Throws<DomainException>(() => new PayComponentType("Loan", PayComponentKind.Deduction, true, UtcNow));

    [Fact]
    public void Taxable_allowance_is_accepted() =>
        Assert.True(new PayComponentType(" Housing ", PayComponentKind.Allowance, true, UtcNow) is
            { Name: "Housing", IsTaxable: true });

    [Fact]
    public void Update_cannot_make_a_deduction_taxable()
    {
        var type = new PayComponentType("Loan", PayComponentKind.Deduction, false, UtcNow);

        Assert.Throws<DomainException>(() => type.Update("Loan", true, UtcNow));
    }

    [Fact]
    public void Update_renames_and_keeps_kind()
    {
        var type = new PayComponentType("Meal", PayComponentKind.Allowance, false, UtcNow);

        type.Update("Meals", true, UtcNow.AddDays(1));

        Assert.Equal("Meals", type.Name);
        Assert.True(type.IsTaxable);
        Assert.Equal(PayComponentKind.Allowance, type.Kind);
        Assert.Equal(UtcNow.AddDays(1), type.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string name) =>
        Assert.Throws<DomainException>(() => new PayComponentType(name, PayComponentKind.Allowance, false, UtcNow));

    [Fact]
    public void Undefined_kind_is_rejected() =>
        Assert.Throws<DomainException>(() => new PayComponentType("X", (PayComponentKind)9, false, UtcNow));

    [Fact]
    public void Department_name_is_required() =>
        Assert.Throws<DomainException>(() => new Department(1, " "));
}
