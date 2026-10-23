using Microsoft.EntityFrameworkCore;
using OcrWebApp.Models.Entities;

namespace OcrWebApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<OcrJob> Jobs => Set<OcrJob>();
    public DbSet<OcrJobPage> JobPages => Set<OcrJobPage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OcrJob>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SessionToken)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(e => e.StoredFileName)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Language)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            // Indexes for fast lookup by user session, status filtering, and cleanup queries
            entity.HasIndex(e => e.SessionToken);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAtUtc);

            entity.HasMany(e => e.Pages)
                .WithOne(p => p.Job)
                .HasForeignKey(p => p.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OcrJobPage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PageImagePath)
                .HasMaxLength(260);

            entity.HasIndex(e => new { e.JobId, e.PageNumber }).IsUnique();
        });
    }
}
