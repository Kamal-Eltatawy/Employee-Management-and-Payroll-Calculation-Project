using Microsoft.EntityFrameworkCore;
using Payroll.Application.Common;
using Payroll.Domain.Employees;
using Payroll.Infrastructure.Persistence;
using static Payroll.Infrastructure.Persistence.ReferenceData;

namespace Payroll.Infrastructure.Seeding;

/// <summary>Demo employees for Development only (S10). Idempotent: does nothing once any employee exists.</summary>
public sealed class DevelopmentDataSeeder(AppDbContext db, TimeProvider clock)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var today = clock.Today();
        var utcNow = clock.UtcNow();

        Employee Create(string name, string email, string jobTitle, int departmentId, DateOnly hireDate, decimal salary,
            EmployeeStatus status, params PayComponentAssignment[] components) =>
            CreateInScheme(TaxSchemes.Standard, name, email, jobTitle, departmentId, hireDate, salary, status, components);

        Employee CreateInScheme(int taxSchemeId, string name, string email, string jobTitle, int departmentId, DateOnly hireDate,
            decimal salary, EmployeeStatus status, params PayComponentAssignment[] components)
        {
            var employee = new Employee(name, email, jobTitle, departmentId, taxSchemeId, hireDate, salary, status, today, utcNow);
            employee.SetPayComponents(components, utcNow);
            return employee;
        }

        db.Employees.AddRange(
            Create("Ali Hassan", "ali@demo.local", "Developer", Departments.Engineering, new DateOnly(2024, 6, 1), 17000m,
                EmployeeStatus.Active,
                new PayComponentAssignment(PayComponentTypes.Transport, 1000m), new PayComponentAssignment(PayComponentTypes.Housing, 2000m),
                new PayComponentAssignment(PayComponentTypes.LoanRepayment, 750m)),
            Create("Sara Ahmed", "sara@demo.local", "Accountant", Departments.Finance, new DateOnly(2023, 2, 15), 12000m,
                EmployeeStatus.Active,
                new PayComponentAssignment(PayComponentTypes.Meal, 500m), new PayComponentAssignment(PayComponentTypes.MedicalPlan, 300m)),
            CreateInScheme(TaxSchemes.Exempt, "Omar Khaled", "omar@demo.local", "HR Specialist", Departments.HumanResources, new DateOnly(2022, 9, 1), 9000m,
                EmployeeStatus.Active,
                new PayComponentAssignment(PayComponentTypes.Transport, 600m)),
            CreateInScheme(TaxSchemes.Flat, "Mona Ibrahim", "mona@demo.local", "Operations Lead", Departments.Operations, new DateOnly(2025, 1, 10), 15000m,
                EmployeeStatus.Active,
                new PayComponentAssignment(PayComponentTypes.Housing, 1500m), new PayComponentAssignment(PayComponentTypes.MedicalPlan, 400m)),
            Create("Youssef Adel", "youssef@demo.local", "Sales Manager", Departments.Sales, new DateOnly(2021, 11, 1), 22000m,
                EmployeeStatus.Inactive,
                new PayComponentAssignment(PayComponentTypes.Transport, 1000m), new PayComponentAssignment(PayComponentTypes.Meal, 800m)),
            Create("Nour Samir", "nour@demo.local", "QA Engineer", Departments.Engineering, new DateOnly(2025, 7, 1), 8000m,
                EmployeeStatus.Active),
            // Hired in the current month: shows hire-month proration.
            Create("Karim Mostafa", "karim@demo.local", "Junior Developer", Departments.Engineering, today, 14000m,
                EmployeeStatus.Active,
                new PayComponentAssignment(PayComponentTypes.Transport, 800m)),
            // Deductions exceed earnings: shows the negative-net warning.
            Create("Laila Farouk", "laila@demo.local", "Sales Associate", Departments.Sales, today, 7000m,
                EmployeeStatus.Active,
                new PayComponentAssignment(PayComponentTypes.LoanRepayment, 6000m), new PayComponentAssignment(PayComponentTypes.MedicalPlan, 1500m)));

        await db.SaveChangesAsync(ct);
    }
}
