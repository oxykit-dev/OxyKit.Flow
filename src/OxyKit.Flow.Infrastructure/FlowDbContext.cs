using Microsoft.EntityFrameworkCore;

namespace OxyKit.Flow.Infrastructure;

public sealed class FlowDbContext(DbContextOptions<FlowDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobDelivery> JobDeliveries => Set<JobDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<Job>();
        job.ToTable("Jobs");
        job.HasKey(j => j.Id);
        job.Property(j => j.Name).HasMaxLength(200);
        job.Property(j => j.IdempotencyKey).HasMaxLength(128);
        job.Property(j => j.RenderEngine).HasMaxLength(64).IsRequired();
        job.Property(j => j.RenderInput).HasMaxLength(64).IsRequired();
        job.Property(j => j.RenderOutput).HasMaxLength(64).IsRequired();
        job.Property(j => j.RenderBodyJson).IsRequired();
        job.Property(j => j.OutputsJson).IsRequired();
        job.Property(j => j.LastError).HasMaxLength(4000);
        job.Property(j => j.Status).HasConversion<string>().HasMaxLength(32);

        // SQLite and PostgreSQL both allow multiple NULLs in a UNIQUE index.
        job.HasIndex(j => j.IdempotencyKey).IsUnique();

        job.HasIndex(j => j.Status);
        job.HasIndex(j => j.CreatedAt);

        job.HasMany<JobDelivery>()
            .WithOne(d => d.Job)
            .HasForeignKey(d => d.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        var delivery = modelBuilder.Entity<JobDelivery>();
        delivery.ToTable("JobDeliveries");
        delivery.HasKey(d => d.Id);
        delivery.Property(d => d.Provider).HasMaxLength(64).IsRequired();
        delivery.Property(d => d.Destination).HasMaxLength(2000).IsRequired();
        delivery.Property(d => d.ExternalId).HasMaxLength(128);
        delivery.Property(d => d.LastError).HasMaxLength(4000);
        delivery.Property(d => d.Status).HasConversion<string>().HasMaxLength(32);
        delivery.HasIndex(d => new { d.JobId, d.Ordinal }).IsUnique();
        delivery.HasIndex(d => d.Status);
    }
}
