using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payroll.Application.Abstractions;
using Payroll.Infrastructure.Caching;
using Payroll.Infrastructure.Identity;
using Payroll.Infrastructure.Maintenance;
using Payroll.Infrastructure.Persistence;
using Payroll.Infrastructure.Repositories;
using Payroll.Infrastructure.Seeding;

namespace Payroll.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Pooled contexts: fewer allocations per request under load (D62). AppDbContext has only the options constructor.
        // Retries transient SQL errors (e.g. the database container still starting). Safe: the code opens no manual transactions.
        services.AddDbContextPool<AppDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. Copy .env.example to .env and set ConnectionStrings__Default."),
            sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddMemoryCache();
        services.AddScoped<DepartmentRepository>();
        services.AddScoped<IDepartmentRepository>(provider =>
            new CachedDepartmentRepository(provider.GetRequiredService<DepartmentRepository>(), provider.GetRequiredService<IMemoryCache>()));
        services.AddScoped<IPayComponentTypeRepository, PayComponentTypeRepository>();
        services.AddScoped<PayrollPolicyRepository>();
        services.AddScoped<IPayrollPolicyRepository>(provider => new CachedPayrollPolicyRepository(
            provider.GetRequiredService<PayrollPolicyRepository>(), provider.GetRequiredService<IMemoryCache>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<TaxSchemeRepository>();
        services.AddScoped<ITaxSchemeRepository>(provider =>
            new CachedTaxSchemeRepository(provider.GetRequiredService<TaxSchemeRepository>(), provider.GetRequiredService<IMemoryCache>()));
        services.AddHostedService<RefreshTokenCleanupService>();
        services.AddScoped<DevelopmentDataSeeder>();

        // Identity (D24): users, roles and password hashing. No cookies: the API authenticates with JWT only.
        // Lockout (D66): 10 failed passwords lock the account for 5 minutes. The response stays the same 401 (D25).
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IUserCredentialStore, IdentityCredentialStore>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

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
