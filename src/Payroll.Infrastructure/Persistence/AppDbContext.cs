using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Departments;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;
using Payroll.Infrastructure.Identity;

namespace Payroll.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options), IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmployeePayComponent> EmployeePayComponents => Set<EmployeePayComponent>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<PayComponentType> PayComponentTypes => Set<PayComponentType>();

    public DbSet<PayrollPolicy> PayrollPolicies => Set<PayrollPolicy>();

    public DbSet<TaxScheme> TaxSchemes => Set<TaxScheme>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>The single place where database failures become application exceptions (S6).</summary>
    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ConflictMessages.Concurrency);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            throw sql.Message.Contains(EmployeeConfiguration.EmailIndexName, StringComparison.OrdinalIgnoreCase)
                ? ConflictException.ForField("email", ConflictMessages.EmailInUse)
                : new ConflictException("The record conflicts with an existing record.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tables first
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
