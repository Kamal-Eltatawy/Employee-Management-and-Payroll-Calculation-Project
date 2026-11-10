using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Infrastructure.Persistence;

/// <summary>Reference data seeded through the migrations (S10). Ids are fixed so the demo seeder can use them.</summary>
public static class ReferenceData
{
    public static readonly DateTime SeededAtUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static class Departments
    {
        public const int Engineering = 1;
        public const int Finance = 2;
        public const int HumanResources = 3;
        public const int Operations = 4;
        public const int Sales = 5;
    }

    public static class TaxSchemes
    {
        public const int Standard = 1;
        public const int Flat = 2;
        public const int Exempt = 3;
    }

    public static class PayComponentTypes
    {
        public const int Transport = 1;
        public const int Housing = 2;
        public const int Meal = 3;
        public const int LoanRepayment = 4;
        public const int MedicalPlan = 5;
    }

    internal static object[] DepartmentRows() =>
    [
        new { Id = Departments.Engineering, Name = "Engineering", NameAr = "الهندسة" },
        new { Id = Departments.Finance, Name = "Finance", NameAr = "المالية" },
        new { Id = Departments.HumanResources, Name = "Human Resources", NameAr = "الموارد البشرية" },
        new { Id = Departments.Operations, Name = "Operations", NameAr = "العمليات" },
        new { Id = Departments.Sales, Name = "Sales", NameAr = "المبيعات" },
    ];

    internal static object[] PayComponentTypeRows() =>
    [
        PayComponentTypeRow(PayComponentTypes.Transport, "Transport", "بدل انتقال", PayComponentKind.Allowance, isTaxable: false),
        PayComponentTypeRow(PayComponentTypes.Housing, "Housing", "بدل سكن", PayComponentKind.Allowance, isTaxable: true),
        PayComponentTypeRow(PayComponentTypes.Meal, "Meal", "بدل وجبات", PayComponentKind.Allowance, isTaxable: true),
        PayComponentTypeRow(PayComponentTypes.LoanRepayment, "Loan Repayment", "سداد قرض", PayComponentKind.Deduction, isTaxable: false),
        PayComponentTypeRow(PayComponentTypes.MedicalPlan, "Medical Plan", "التأمين الطبي", PayComponentKind.Deduction, isTaxable: false),
    ];

    internal static object[] TaxSchemeRows() =>
    [
        new { Id = TaxSchemes.Standard, Name = "Standard", NameAr = "النظام القياسي", Method = TaxMethod.Progressive },
        new { Id = TaxSchemes.Flat, Name = "Flat Rate", NameAr = "النسبة الثابتة", Method = TaxMethod.Flat },
        new { Id = TaxSchemes.Exempt, Name = "Tax Exempt", NameAr = "معفى من الضريبة", Method = TaxMethod.Exempt },
    ];

    /// <summary>
    /// Standard: v1 in force since 2025-01; v2 starts 2027-01 to demonstrate effective dating and previews (D29/D34).
    /// Flat Rate: 15% on taxable income. Tax Exempt: no brackets, insurance only (D53).
    /// </summary>
    internal static object[] PayrollPolicyRows() =>
    [
        PolicyRow(1, TaxSchemes.Standard, new DateOnly(2025, 1, 1), 11m, 1500m),
        PolicyRow(2, TaxSchemes.Standard, new DateOnly(2027, 1, 1), 12m, 1800m),
        PolicyRow(3, TaxSchemes.Flat, new DateOnly(2025, 1, 1), 11m, 1500m),
        PolicyRow(4, TaxSchemes.Exempt, new DateOnly(2025, 1, 1), 11m, 1500m),
    ];

    private static object PolicyRow(int id, int taxSchemeId, DateOnly effectiveFrom, decimal rate, decimal? cap) =>
        new { Id = id, TaxSchemeId = taxSchemeId, EffectiveFrom = effectiveFrom, InsuranceRatePercent = rate, InsuranceCap = cap, CreatedAtUtc = SeededAtUtc };

    internal static object[] TaxBracketRows() =>
    [
        new { Id = 1, PayrollPolicyId = 1, SortOrder = 1, UpperBound = (decimal?)5000m, RatePercent = 0m },
        new { Id = 2, PayrollPolicyId = 1, SortOrder = 2, UpperBound = (decimal?)15000m, RatePercent = 10m },
        new { Id = 3, PayrollPolicyId = 1, SortOrder = 3, UpperBound = (decimal?)null, RatePercent = 20m },
        new { Id = 4, PayrollPolicyId = 2, SortOrder = 1, UpperBound = (decimal?)6000m, RatePercent = 0m },
        new { Id = 5, PayrollPolicyId = 2, SortOrder = 2, UpperBound = (decimal?)18000m, RatePercent = 10m },
        new { Id = 6, PayrollPolicyId = 2, SortOrder = 3, UpperBound = (decimal?)null, RatePercent = 22m },
        new { Id = 7, PayrollPolicyId = 3, SortOrder = 1, UpperBound = (decimal?)null, RatePercent = 15m },
    ];

    private static object PayComponentTypeRow(int id, string name, string nameAr, PayComponentKind kind, bool isTaxable) =>
        new { Id = id, Name = name, NameAr = nameAr, Kind = kind, IsTaxable = isTaxable, CreatedAtUtc = SeededAtUtc, UpdatedAtUtc = SeededAtUtc };
}
