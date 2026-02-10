using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SAMA.Data.Entities;

namespace SAMA.Data.Configuration;

public class CheckConfiguration : IEntityTypeConfiguration<Check>
{
    public void Configure(EntityTypeBuilder<Check> builder)
    {
        builder.ToTable("Checks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.CheckType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.ConfigurationJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.Schedule)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.TimeoutSeconds)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(c => c.Enabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(c => c.Workspace)
            .WithMany(w => w.Checks)
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.WorkspaceId)
            .HasDatabaseName("IX_Checks_WorkspaceId");

        builder.HasIndex(c => c.Enabled)
            .HasDatabaseName("IX_Checks_Enabled");
    }
}
