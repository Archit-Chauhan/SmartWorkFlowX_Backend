using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Infrastructure.Data
{
    public class SmartWorkflowXDbContext : DbContext
    {
        public SmartWorkflowXDbContext(DbContextOptions<SmartWorkflowXDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Workflow> Workflows { get; set; }
        public DbSet<WorkflowStep> WorkflowSteps { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TaskStepHistory> TaskStepHistories { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. Roles & Users
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<User>(entity => {
                entity.ToTable("Users");
                entity.HasKey(e => e.UserId);
                entity.HasOne(d => d.Role).WithMany().HasForeignKey(d => d.RoleId);
            });

            // 2. Workflows & Steps
            modelBuilder.Entity<Workflow>(entity => {
                entity.ToTable("Workflows");
                entity.HasKey(w => w.WorkflowId);
                entity.Property(w => w.Status).HasDefaultValue("Draft");

                entity.HasOne(w => w.Creator)
                      .WithMany()
                      .HasForeignKey(w => w.CreatedBy)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(w => w.Steps)
                      .WithOne()
                      .HasForeignKey(s => s.WorkflowId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WorkflowStep>(entity => {
                entity.ToTable("WorkflowSteps");
                entity.HasKey(s => s.StepId);
                entity.Property(s => s.OnRejectAction).HasDefaultValue("Cancel");

                entity.HasOne(s => s.ApproverRole)
                      .WithMany()
                      .HasForeignKey(s => s.ApproverRoleId);
            });

            // 3. Tasks
            modelBuilder.Entity<TaskItem>(entity => {
                entity.ToTable("Tasks");
                entity.HasKey(t => t.TaskId);
                entity.Property(t => t.Priority).HasDefaultValue("Medium");
                entity.Property(t => t.Status).HasDefaultValue("Pending");

                entity.HasOne(t => t.Assignee)
                      .WithMany()
                      .HasForeignKey(t => t.AssignedTo)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(t => t.StepHistories)
                      .WithOne(h => h.Task)
                      .HasForeignKey(h => h.TaskId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 4. Task Step History
            modelBuilder.Entity<TaskStepHistory>(entity => {
                entity.ToTable("TaskStepHistories");
                entity.HasKey(h => h.Id);

                entity.HasOne(h => h.ActedByUser)
                      .WithMany()
                      .HasForeignKey(h => h.ActedByUserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // 5. Audit Logs
            modelBuilder.Entity<AuditLog>(entity => {
                entity.ToTable("AuditLogs");
                entity.HasKey(a => a.LogId);
            });

            // 6. Notifications
            modelBuilder.Entity<Notification>(entity => {
                entity.ToTable("Notifications");
                entity.HasKey(n => n.NotificationId);
            });
        }
    }
}