using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.Employees;

namespace Payroll.Infrastructure.Persistence;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public const string EmailIndexName = "IX_Employees_Email";

    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FullName).HasMaxLength(Employee.FullNameMaxLength).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(Employee.EmailMaxLength).IsRequired();
        builder.Property(e => e.JobTitle).HasMaxLength(Employee.JobTitleMaxLength).IsRequired();
        builder.Property(e => e.BaseMonthlySalary).HasPrecision(18, 2);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Plain unique index over all rows: the email of a soft-deleted employee stays reserved (D6).
        builder.HasIndex(e => e.Email).IsUnique().HasDatabaseName(EmailIndexName);
        builder.HasIndex(e => e.FullName);
        builder.HasIndex(e => e.Status);

        builder.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.PayComponents)
            .WithOne()
            .HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.PayComponents).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(e => e.DeletedAtUtc == null);
    }
}
