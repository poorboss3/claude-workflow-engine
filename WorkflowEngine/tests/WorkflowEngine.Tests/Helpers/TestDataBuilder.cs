using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Tests.Helpers;

public static class TestDataBuilder
{
    public static async Task<ProcessDefinition> SeedDefinitionAsync(WorkflowDbContext ctx, string processType = "test_process")
    {
        var def = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Process",
            ProcessType = processType,
            Version = 1,
            Status = DefinitionStatus.Active,
            CreatedBy = "admin",
        };
        ctx.ProcessDefinitions.Add(def);
        await ctx.SaveChangesAsync();
        return def;
    }

    public static async Task<(ProcessInstance instance, ApprovalStep step, WorkflowTask task)> SeedSingleStepProcessAsync(
        WorkflowDbContext ctx, string assigneeId = "approver-1")
    {
        var def = await SeedDefinitionAsync(ctx);

        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = def.Id,
            DefinitionVersion = 1,
            BusinessKey = $"BK-{Guid.NewGuid():N}",
            FormDataSnapshotJson = "{}",
            SubmittedBy = "user-1",
            Status = ProcessStatus.Running,
            CurrentStepIndex = 1m,
        };
        ctx.ProcessInstances.Add(instance);

        var step = new ApprovalStep
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepIndex = 1m,
            Type = StepType.Approval,
            Assignees = [new StepAssignee(assigneeId)],
            Status = StepStatus.Active,
            Source = StepSource.Original,
        };
        ctx.ApprovalSteps.Add(step);

        var task = new WorkflowTask
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepId = step.Id,
            AssigneeId = assigneeId,
            Status = Domain.Enums.TaskStatus.Pending,
        };
        ctx.Tasks.Add(task);

        await ctx.SaveChangesAsync();
        return (instance, step, task);
    }

    public static async Task<(ProcessInstance instance, ApprovalStep step, List<WorkflowTask> tasks)> SeedJointSignProcessAsync(
        WorkflowDbContext ctx, JointSignPolicy policy, int assigneeCount)
    {
        var def = await SeedDefinitionAsync(ctx);
        var assignees = Enumerable.Range(1, assigneeCount)
            .Select(i => new StepAssignee($"approver-{i}")).ToList();

        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = def.Id,
            DefinitionVersion = 1,
            BusinessKey = $"BK-{Guid.NewGuid():N}",
            FormDataSnapshotJson = "{}",
            SubmittedBy = "user-1",
            Status = ProcessStatus.Running,
            CurrentStepIndex = 1m,
        };
        ctx.ProcessInstances.Add(instance);

        var step = new ApprovalStep
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepIndex = 1m,
            Type = StepType.JointSign,
            Assignees = assignees,
            JointSignPolicy = policy,
            Status = StepStatus.Active,
            Source = StepSource.Original,
        };
        ctx.ApprovalSteps.Add(step);

        var tasks = assignees.Select(a => new WorkflowTask
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepId = step.Id,
            AssigneeId = a.UserId,
            Status = Domain.Enums.TaskStatus.Pending,
        }).ToList();
        ctx.Tasks.AddRange(tasks);

        await ctx.SaveChangesAsync();
        return (instance, step, tasks);
    }
}
