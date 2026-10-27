using Microsoft.EntityFrameworkCore;

namespace OxyKit.Flow.Infrastructure;

public sealed class FlowDbContext(DbContextOptions<FlowDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

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
    }
}
