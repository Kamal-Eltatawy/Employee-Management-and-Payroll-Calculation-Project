using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Payroll.Application.Common;
using Payroll.Application.Departments;
using Payroll.Application.Employees;

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

        return services;
    }
}
