using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Payroll.Application.Common;
using Payroll.Application.Departments;
using Payroll.Application.Employees;
using Payroll.Application.PayComponents;
using Payroll.Application.Payroll;
using Payroll.Domain.Payroll;

namespace Payroll.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var mapping = MappingConfig.Create();
        mapping.Compile();
        services.AddSingleton(mapping);

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPayslipService, PayslipService>();
        services.AddScoped<IPayrollPolicyService, PayrollPolicyService>();
        services.AddScoped<ITaxSchemeService, TaxSchemeService>();
        services.AddScoped<IPayComponentTypeService, PayComponentTypeService>();

        // Domain calculators: pure and stateless.
        services.AddSingleton<IProrationCalculator, CalendarDayProrationCalculator>();
        // Every tax method is registered; PayrollCalculator receives all of them and picks by the scheme's method (D53).
        services.AddSingleton<ITaxCalculator, ProgressiveTaxCalculator>();
        services.AddSingleton<ITaxCalculator, FlatTaxCalculator>();
        services.AddSingleton<ITaxCalculator, ExemptTaxCalculator>();
        services.AddSingleton<ISocialInsuranceCalculator, SocialInsuranceCalculator>();
        services.AddSingleton<IPayrollCalculator, PayrollCalculator>();

        return services;
    }
}
