using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Domain.UnitTests.Payroll;

internal static class PolicyBuilder
{
    public static readonly DateTime UtcNow = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly TaxScheme Standard = new(1, "Standard", TaxMethod.Progressive);
    public static readonly TaxScheme Flat = new(2, "Flat Rate", TaxMethod.Flat);
    public static readonly TaxScheme Exempt = new(3, "Tax Exempt", TaxMethod.Exempt);

    /// <summary>Spec §11.5 policy v1: 0% to 5,000; 10% to 15,000; 20% above. Insurance 11% capped at 1,500.</summary>
    public static PayrollPolicy V1(decimal? cap = 1500m, decimal insuranceRate = 11m) =>
        new(Standard, new DateOnly(2025, 1, 1), insuranceRate, cap,
            [new(5000m, 0m), new(15000m, 10m), new(null, 20m)], UtcNow);

    public static IReadOnlyList<TaxBracket> Brackets(params TaxBracketDefinition[] definitions) =>
        new PayrollPolicy(Standard, new DateOnly(2025, 1, 1), 0m, null, definitions, UtcNow).TaxBrackets;

    /// <summary>Sets what EF Core would load: the tax scheme and the pay component type navigations.</summary>
    public static Employee WithTypes(Employee employee, params PayComponentType[] types)
    {
        var scheme = new[] { Standard, Flat, Exempt }.Single(s => s.Id == employee.TaxSchemeId);
        typeof(Employee).GetProperty(nameof(Employee.TaxScheme))!.SetValue(employee, scheme);

        foreach (var component in employee.PayComponents)
        {
            var type = types.Single(t => t.Id == component.PayComponentTypeId);
            typeof(EmployeePayComponent).GetProperty(nameof(EmployeePayComponent.PayComponentType))!.SetValue(component, type);
        }

        return employee;
    }

    public static PayComponentType Type(int id, string name, PayComponentKind kind, bool isTaxable = false)
    {
        var type = new PayComponentType(name, kind, isTaxable, UtcNow);
        typeof(PayComponentType).GetProperty(nameof(PayComponentType.Id))!.SetValue(type, id);
        return type;
    }
}
