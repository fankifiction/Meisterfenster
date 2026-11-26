using Microsoft.EntityFrameworkCore;
using Wochenplanung.Domain.Entities;

namespace Wochenplanung.Infrastructure.Data;

public class ScheduleDbContext : DbContext
{
    public ScheduleDbContext(DbContextOptions<ScheduleDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; }
    public DbSet<WorkTask> Tasks { get; set; }
    public DbSet<ScheduleTemplate> ScheduleTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeNumber).HasMaxLength(30);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(100);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Version).IsConcurrencyToken().HasDefaultValue(0);
            entity.Property(e => e.LastModifiedBy).HasMaxLength(200);
            entity.HasMany(e => e.Tasks)
                  .WithOne(t => t.Employee)
                  .HasForeignKey(t => t.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkTask>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Description).IsRequired().HasMaxLength(500);
            entity.Property(t => t.Location).HasMaxLength(200);
            entity.Property(t => t.Project).HasMaxLength(200);
            entity.Property(t => t.Notes).HasMaxLength(1000);
            entity.Property(t => t.Version).IsConcurrencyToken().HasDefaultValue(0);
            entity.Property(t => t.LastModifiedBy).HasMaxLength(200);
            entity.HasIndex(t => new { t.EmployeeId, t.StartDate });
            entity.HasIndex(t => t.RecurrenceGroupId);
        });

        modelBuilder.Entity<ScheduleTemplate>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Description).IsRequired().HasMaxLength(500);
            entity.Property(t => t.DisplayOrder).HasDefaultValue(0);
        });
    }
}
