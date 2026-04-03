using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SAMA.Data.Entities;

namespace SAMA.Data.Configuration;

public class CheckResultConfiguration : IEntityTypeConfiguration<CheckResult>
{
    public void Configure(EntityTypeBuilder<CheckResult> builder)
    {
        builder.ToTable("CheckResults");

        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(cr => cr.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(cr => cr.CheckedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(cr => cr.Check)
            .WithMany(c => c.CheckResults)
            .HasForeignKey(cr => cr.CheckId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(cr => new { cr.CheckId, cr.CheckedAt })
            .HasDatabaseName("IX_CheckResults_CheckId_CheckedAt_Covering")
            .IsDescending(false, true)
            .IncludeProperties(cr => new { cr.Status, cr.ResponseTimeMs, cr.ErrorMessage });

        builder.HasIndex(cr => cr.CheckedAt)
            .HasDatabaseName("IX_CheckResults_CheckedAt");
    }
}
