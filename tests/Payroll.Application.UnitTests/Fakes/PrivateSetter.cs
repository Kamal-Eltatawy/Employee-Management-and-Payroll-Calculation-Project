namespace Payroll.Application.UnitTests.Fakes;

/// <summary>Sets database-generated values (ids, row versions, loaded navigations) the way EF Core would.</summary>
internal static class PrivateSetter
{
    public static void Set(object entity, string property, object? value) =>
        entity.GetType().GetProperty(property)!.SetValue(entity, value);
}
