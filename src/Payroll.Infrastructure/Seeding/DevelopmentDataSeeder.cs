using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Payroll.Application.Common;
using Payroll.Domain.Employees;
using Payroll.Infrastructure.Identity;
using Payroll.Infrastructure.Persistence;
using static Payroll.Infrastructure.Persistence.ReferenceData;

namespace Payroll.Infrastructure.Seeding;

/// <summary>
/// Demo data for Development only (S10), idempotent. Employees are added once; roles and users are added when missing.
/// Passwords come from configuration (Seed__HrPassword, Seed__EmployeePassword in .env), never from code.
/// </summary>
public sealed class DevelopmentDataSeeder(
    AppDbContext db,
    TimeProvider clock,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<int>> roles,
    IConfiguration configuration)
{
    public const string HrEmail = "hr@payroll.local";

    public async Task SeedAsync(CancellationToken ct)
    {
        await SeedEmployeesAsync(ct);
        await SeedUsersAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        foreach (var role in new[] { Roles.Hr, Roles.Employee })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole<int>(role)), $"create role {role}");
            }
        }

        var hrPassword = RequiredSetting("Seed:HrPassword");
        var employeePassword = RequiredSetting("Seed:EmployeePassword");

        await EnsureUserAsync(HrEmail, hrPassword, Roles.Hr, employeeId: null);

        // Ali is Active (can log in); Youssef is Inactive (shows the D25 rule: login refused).
        foreach (var email in new[] { "ali@demo.local", "youssef@demo.local" })
        {
            var employee = await db.Employees.IgnoreQueryFilters().SingleOrDefaultAsync(e => e.Email == email, ct);
            if (employee is not null)
            {
                await EnsureUserAsync(email, employeePassword, Roles.Employee, employee.Id);
            }
        }
    }

    private async Task EnsureUserAsync(string email, string password, string role, int? employeeId)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, EmployeeId = employeeId };
        EnsureSucceeded(await users.CreateAsync(user, password), $"create user {email}");
        EnsureSucceeded(await users.AddToRoleAsync(user, role), $"add {email} to {role}");
    }

    private string RequiredSetting(string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{key.Replace(":", "__", StringComparison.Ordinal)} is missing. Set it in .env (see .env.example).");

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }

    private async Task SeedEmployeesAsync(CancellationToken ct)
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
