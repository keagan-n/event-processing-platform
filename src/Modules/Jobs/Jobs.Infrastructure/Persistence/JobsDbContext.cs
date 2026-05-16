using Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence;

public sealed class JobsDbContext : DbContext
{
    public JobsDbContext(DbContextOptions<JobsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("jobs");

            entity.HasKey(job => job.Id);

            entity.Property(job => job.Id)
                .HasColumnName("id")
                .IsRequired();

            entity.Property(job => job.TenantId)
                .HasColumnName("tenant_id")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(job => job.JobType)
                .HasColumnName("job_type")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(job => job.Payload)
                .HasColumnName("payload")
                .IsRequired();

            entity.Property(job => job.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(job => job.CreatedUtc)
                .HasColumnName("created_utc")
                .IsRequired();

            entity.HasIndex(job => job.Status)
                .HasDatabaseName("ix_jobs_status");

            entity.HasIndex(job => job.CreatedUtc)
                .HasDatabaseName("ix_jobs_created_utc");
        });
    }
}