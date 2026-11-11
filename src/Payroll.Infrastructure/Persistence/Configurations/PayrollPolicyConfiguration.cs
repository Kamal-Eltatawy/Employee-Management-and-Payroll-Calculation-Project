using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payroll.Domain.Payroll;

namespace Payroll.Infrastructure.Persistence;

internal sealed class PayrollPolicyConfiguration : IEntityTypeConfiguration<PayrollPolicy>
{
    public const string SchemeDateIndexName = "IX_PayrollPolicies_TaxSchemeId_EffectiveFrom";

    public void Configure(EntityTypeBuilder<PayrollPolicy> builder)
    {
        builder.ToTable("PayrollPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.InsuranceRatePercent).HasPrecision(5, 2);
        builder.Property(p => p.InsuranceCap).HasPrecision(18, 2);
        builder.HasIndex(p => new { p.TaxSchemeId, p.EffectiveFrom }).IsUnique().HasDatabaseName(SchemeDateIndexName);
        builder.HasOne<TaxScheme>()
            .WithMany()
            .HasForeignKey(p => p.TaxSchemeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.TaxBrackets)
            .WithOne()
            .HasForeignKey(b => b.PayrollPolicyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.TaxBrackets).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(ReferenceData.PayrollPolicyRows());
    }
}

internal sealed class TaxBracketConfiguration : IEntityTypeConfiguration<TaxBracket>
{
    public void Configure(EntityTypeBuilder<TaxBracket> builder)
    {
        builder.ToTable("TaxBrackets");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.UpperBound).HasPrecision(18, 2);
        builder.Property(b => b.RatePercent).HasPrecision(5, 2);
        builder.HasIndex(b => new { b.PayrollPolicyId, b.SortOrder }).IsUnique();

        builder.HasData(ReferenceData.TaxBracketRows());
    }
}
