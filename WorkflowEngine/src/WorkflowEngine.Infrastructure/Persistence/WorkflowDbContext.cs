using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence;

public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    public DbSet<ProcessDefinition> ProcessDefinitions { get; set; }
    public DbSet<ProcessInstance> ProcessInstances { get; set; }
    public DbSet<ApprovalStep> ApprovalSteps { get; set; }
    public DbSet<WorkflowTask> Tasks { get; set; }
    public DbSet<ApprovalRule> ApprovalRules { get; set; }
    public DbSet<ApproverListModification> ApproverModifications { get; set; }
    public DbSet<ProxyConfig> ProxyConfigs { get; set; }
    public DbSet<DelegationConfig> DelegationConfigs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowDbContext).Assembly);
    }
}
