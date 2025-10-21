using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SADAB.Server.Models;

namespace SADAB.Server.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Agent> Agents { get; set; }
    public DbSet<AgentCertificate> AgentCertificates { get; set; }
    public DbSet<Deployment> Deployments { get; set; }
    public DbSet<DeploymentTarget> DeploymentTargets { get; set; }
    public DbSet<DeploymentResult> DeploymentResults { get; set; }
    public DbSet<InventoryData> InventoryData { get; set; }
    public DbSet<CommandExecution> CommandExecutions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Agent configuration
        builder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MachineId).IsUnique();
            entity.HasIndex(e => e.MachineName);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.RegisteredAt).HasDefaultValueSql("datetime('now')");
        });

        // AgentCertificate configuration
        builder.Entity<AgentCertificate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Thumbprint).IsUnique();
            entity.HasIndex(e => e.AgentId);
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Certificates)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Deployment configuration
        builder.Entity<Deployment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // DeploymentTarget configuration
        builder.Entity<DeploymentTarget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DeploymentId, e.AgentId }).IsUnique();
            entity.HasOne(e => e.Deployment)
                .WithMany(d => d.Targets)
                .HasForeignKey(e => e.DeploymentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Agent)
                .WithMany()
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DeploymentResult configuration
        builder.Entity<DeploymentResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DeploymentId, e.AgentId });
            entity.HasOne(e => e.Deployment)
                .WithMany(d => d.Results)
                .HasForeignKey(e => e.DeploymentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.DeploymentResults)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryData configuration
        builder.Entity<InventoryData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.CollectedAt);
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.InventoryData)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CommandExecution configuration
        builder.Entity<CommandExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.RequestedAt);
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.CommandExecutions)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
