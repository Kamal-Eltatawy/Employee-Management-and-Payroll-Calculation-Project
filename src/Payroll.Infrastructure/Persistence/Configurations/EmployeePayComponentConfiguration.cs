using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.Employees;

namespace Payroll.Infrastructure.Persistence;

internal sealed class EmployeePayComponentConfiguration : IEntityTypeConfiguration<EmployeePayComponent>
{
    public void Configure(EntityTypeBuilder<EmployeePayComponent> builder)
    {
        builder.ToTable("EmployeePayComponents");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.HasIndex(c => new { c.EmployeeId, c.PayComponentTypeId }).IsUnique();

        builder.HasOne(c => c.PayComponentType)
            .WithMany()
            .HasForeignKey(c => c.PayComponentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
