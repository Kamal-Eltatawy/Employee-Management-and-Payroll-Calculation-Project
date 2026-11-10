using Payroll.Domain.PayComponents;

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
        new { Id = Departments.Engineering, Name = "Engineering" },
        new { Id = Departments.Finance, Name = "Finance" },
        new { Id = Departments.HumanResources, Name = "Human Resources" },
        new { Id = Departments.Operations, Name = "Operations" },
        new { Id = Departments.Sales, Name = "Sales" },
    ];

    internal static object[] PayComponentTypeRows() =>
    [
        PayComponentTypeRow(PayComponentTypes.Transport, "Transport", PayComponentKind.Allowance, isTaxable: false),
        PayComponentTypeRow(PayComponentTypes.Housing, "Housing", PayComponentKind.Allowance, isTaxable: true),
        PayComponentTypeRow(PayComponentTypes.Meal, "Meal", PayComponentKind.Allowance, isTaxable: true),
        PayComponentTypeRow(PayComponentTypes.LoanRepayment, "Loan Repayment", PayComponentKind.Deduction, isTaxable: false),
        PayComponentTypeRow(PayComponentTypes.MedicalPlan, "Medical Plan", PayComponentKind.Deduction, isTaxable: false),
    ];

    private static object PayComponentTypeRow(int id, string name, PayComponentKind kind, bool isTaxable) =>
        new { Id = id, Name = name, Kind = kind, IsTaxable = isTaxable, CreatedAtUtc = SeededAtUtc, UpdatedAtUtc = SeededAtUtc };
}
