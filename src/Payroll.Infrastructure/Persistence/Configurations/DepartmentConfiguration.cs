using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.Departments;

namespace Payroll.Infrastructure.Persistence;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(d => d.NameAr).HasMaxLength(Department.NameMaxLength);
        builder.HasIndex(d => d.Name).IsUnique();
        builder.HasData(ReferenceData.DepartmentRows());
    }
}
