using Microsoft.EntityFrameworkCore;
using Tasked.Models;

namespace Tasked.Data;

public class TaskedDbContext : DbContext
{
    public TaskedDbContext(DbContextOptions<TaskedDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> Tasks { get; set; }
    public DbSet<TaskProgress> TaskProgress { get; set; }
    public DbSet<Repository> Repositories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TaskItem configuration
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JiraKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Priority).HasMaxLength(20);
            entity.Property(e => e.Assignee).HasMaxLength(100);
            entity.Property(e => e.Reporter).HasMaxLength(100);
            entity.Property(e => e.RepositoryUrl).HasMaxLength(500);
            entity.Property(e => e.BranchName).HasMaxLength(200);

            entity.HasIndex(e => e.JiraKey).IsUnique();
            entity.HasIndex(e => e.LocalStatus);
        });

        // TaskProgress configuration
        modelBuilder.Entity<TaskProgress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);

            entity.HasOne(e => e.TaskItem)
                  .WithMany(e => e.ProgressHistory)
                  .HasForeignKey(e => e.TaskItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TaskItemId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Status);
        });

        // Repository configuration
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.DefaultBranch).HasMaxLength(100);
            entity.Property(e => e.ProjectKey).HasMaxLength(100);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Provider);
            entity.HasIndex(e => e.IsActive);
        });

        base.OnModelCreating(modelBuilder);
    }
}
