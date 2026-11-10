using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.PayComponents;

namespace Payroll.Infrastructure.Persistence;

internal sealed class PayComponentTypeConfiguration : IEntityTypeConfiguration<PayComponentType>
{
    public void Configure(EntityTypeBuilder<PayComponentType> builder)
    {
        builder.ToTable("PayComponentTypes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(PayComponentType.NameMaxLength).IsRequired();
        builder.Property(t => t.Kind).HasConversion<int>();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasData(ReferenceData.PayComponentTypeRows());
    }
}
