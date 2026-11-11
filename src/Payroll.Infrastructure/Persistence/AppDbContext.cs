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
            throw ConflictException.Coded(ErrorCodes.Concurrency, ConflictMessages.Concurrency);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            throw await ToUniqueConflictAsync(sql.Message, exception, ct);
        }
    }

    /// <summary>
    /// A race between two requests can pass the service's "already exists" check and hit the unique index instead.
    /// The user still gets the same coded message on the same field as from the service check (D42, D69).
    /// </summary>
    private async Task<ConflictException> ToUniqueConflictAsync(string sqlMessage, DbUpdateException exception, CancellationToken ct)
    {
        bool Violates(string indexName) => sqlMessage.Contains(indexName, StringComparison.OrdinalIgnoreCase);
        var entity = exception.Entries.Select(entry => entry.Entity).FirstOrDefault();

        if (Violates(EmployeeConfiguration.EmailIndexName))
        {
            return ConflictException.Coded(ErrorCodes.EmailInUse, ConflictMessages.EmailInUse, "email");
        }

        if (Violates(PayComponentTypeConfiguration.NameIndexName) && entity is PayComponentType type)
        {
            return ConflictException.Coded(ErrorCodes.PayComponentTypeNameTaken,
                $"A pay component type named '{type.Name}' already exists.", "name", type.Name);
        }

        if (Violates(PayrollPolicyConfiguration.SchemeDateIndexName) && entity is PayrollPolicy policy)
        {
            var scheme = await TaxSchemes.AsNoTracking().Where(s => s.Id == policy.TaxSchemeId).Select(s => s.Name).FirstOrDefaultAsync(ct)
                ?? policy.TaxSchemeId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var date = policy.EffectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return ConflictException.Coded(ErrorCodes.PolicyDateTaken,
                $"The {scheme} scheme already has a policy effective from {date}.", "effectiveFrom", scheme, date);
        }

        return ConflictException.Coded(ErrorCodes.UniqueConflict, "The record conflicts with an existing record.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tables first
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
