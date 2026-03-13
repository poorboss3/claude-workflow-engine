using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Commands.Approval;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Tests.Helpers;

namespace WorkflowEngine.Tests.Unit.Application;

public class ReturnTaskHandlerTests
{
    [Fact]
    public async Task Return_ToInitiator_ShouldReactivateFirstStep()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var def = await TestDataBuilder.SeedDefinitionAsync(ctx);

        var instance = new Domain.Entities.ProcessInstance
        {
            Id = Guid.NewGuid(), DefinitionId = def.Id, DefinitionVersion = 1,
            BusinessKey = "BK-RET-001", FormDataSnapshotJson = "{}", SubmittedBy = "user-1",
            Status = ProcessStatus.Running, CurrentStepIndex = 2m,
        };
        ctx.ProcessInstances.Add(instance);

        var step1 = new Domain.Entities.ApprovalStep
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepIndex = 1m,
            Type = StepType.Approval, Assignees = [new Domain.Common.StepAssignee("user-1")],
            Status = StepStatus.Completed, Source = StepSource.Original,
        };
        var step2 = new Domain.Entities.ApprovalStep
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepIndex = 2m,
            Type = StepType.Approval, Assignees = [new Domain.Common.StepAssignee("approver-1")],
            Status = StepStatus.Active, Source = StepSource.Original,
        };
        ctx.ApprovalSteps.AddRange(step1, step2);

        var task = new Domain.Entities.WorkflowTask
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepId = step2.Id,
            AssigneeId = "approver-1", Status = Domain.Enums.TaskStatus.Pending,
        };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();

        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act: return to initiator (no targetStepId = return to first step)
        await mediator.Send(new ReturnTaskCommand(task.Id, "approver-1", "Incomplete material"));

        // Assert
        await ctx.Entry(instance).ReloadAsync();
        await ctx.Entry(step1).ReloadAsync();
        instance.Status.Should().Be(ProcessStatus.Running);
        instance.CurrentStepIndex.Should().Be(1m);
        step1.Status.Should().Be(StepStatus.Active);
    }
}
