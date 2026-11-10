using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payroll.Application.Abstractions;
using Payroll.Infrastructure.Persistence;
using Payroll.Infrastructure.Repositories;
using Payroll.Infrastructure.Seeding;

namespace Payroll.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. Copy .env.example to .env and set ConnectionStrings__Default.")));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IPayComponentTypeRepository, PayComponentTypeRepository>();
        services.AddScoped<DevelopmentDataSeeder>();

        return services;
    }

    /// <summary>Development startup: apply migrations, then seed demo data (D21).</summary>
    public static async Task MigrateAndSeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(ct);
    }
}
