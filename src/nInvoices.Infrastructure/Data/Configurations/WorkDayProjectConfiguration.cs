using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class WorkDayProjectConfiguration : IEntityTypeConfiguration<WorkDayProject>
{
    public void Configure(EntityTypeBuilder<WorkDayProject> builder)
    {
        builder.ToTable("WorkDayProjects");

        builder.HasKey(wdp => wdp.Id);

        builder.Property(wdp => wdp.Hours)
            .HasPrecision(6, 2)
            .IsRequired();

        builder.Property(wdp => wdp.CreatedAt).IsRequired();
        builder.Property(wdp => wdp.UpdatedAt);

        builder.HasOne(wdp => wdp.WorkDay)
            .WithMany(wd => wd.Projects)
            .HasForeignKey(wdp => wdp.WorkDayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wdp => wdp.Project)
            .WithMany()
            .HasForeignKey(wdp => wdp.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // One allocation per project per work day
        builder.HasIndex(wdp => new { wdp.WorkDayId, wdp.ProjectId })
            .IsUnique();
    }
}
