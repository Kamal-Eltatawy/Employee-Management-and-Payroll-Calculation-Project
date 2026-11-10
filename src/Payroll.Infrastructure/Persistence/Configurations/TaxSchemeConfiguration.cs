using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.Payroll;

namespace Payroll.Infrastructure.Persistence;

internal sealed class TaxSchemeConfiguration : IEntityTypeConfiguration<TaxScheme>
{
    public void Configure(EntityTypeBuilder<TaxScheme> builder)
    {
        builder.ToTable("TaxSchemes");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(TaxScheme.NameMaxLength).IsRequired();
        builder.Property(s => s.NameAr).HasMaxLength(TaxScheme.NameMaxLength);
        builder.Property(s => s.Method).HasConversion<int>();
        builder.HasIndex(s => s.Name).IsUnique();
        builder.HasData(ReferenceData.TaxSchemeRows());
    }
}
