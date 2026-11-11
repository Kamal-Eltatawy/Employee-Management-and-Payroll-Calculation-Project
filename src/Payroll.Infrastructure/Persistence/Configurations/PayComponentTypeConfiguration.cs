using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.PayComponents;

namespace Payroll.Infrastructure.Persistence;

internal sealed class PayComponentTypeConfiguration : IEntityTypeConfiguration<PayComponentType>
{
    public const string NameIndexName = "IX_PayComponentTypes_Name";

    public void Configure(EntityTypeBuilder<PayComponentType> builder)
    {
        builder.ToTable("PayComponentTypes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(PayComponentType.NameMaxLength).IsRequired();
        builder.Property(t => t.NameAr).HasMaxLength(PayComponentType.NameMaxLength);
        builder.Property(t => t.Kind).HasConversion<int>();
        builder.HasIndex(t => t.Name).IsUnique().HasDatabaseName(NameIndexName);
        builder.HasData(ReferenceData.PayComponentTypeRows());
    }
}
