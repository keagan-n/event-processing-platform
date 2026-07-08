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
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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

            entity.Property(job => job.StartedUtc)
                .HasColumnName("started_utc");

            entity.Property(job => job.CompletedUtc)
                .HasColumnName("completed_utc");

            entity.Property(job => job.FailedUtc)
                .HasColumnName("failed_utc");

            entity.Property(job => job.FailureReason)
                .HasColumnName("failure_reason");

            entity.Property(job => job.Result)
                .HasColumnName("result");

            entity.Property(job => job.AttemptCount)
                .HasColumnName("attempt_count")
                .HasDefaultValue(0)
                .IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");

            entity.HasKey(message => message.Id);

            entity.Property(message => message.Id)
                .HasColumnName("id")
                .IsRequired();

            entity.Property(message => message.OccurredUtc)
                .HasColumnName("occurred_utc")
                .IsRequired();

            entity.Property(message => message.Type)
                .HasColumnName("type")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(message => message.Payload)
                .HasColumnName("payload")
                .IsRequired();

            entity.Property(message => message.ProcessedUtc)
                .HasColumnName("processed_utc");

            entity.Property(message => message.Error)
                .HasColumnName("error");

            entity.HasIndex(message => message.ProcessedUtc)
                .HasDatabaseName("ix_outbox_messages_processed_utc");
        });
    }
}