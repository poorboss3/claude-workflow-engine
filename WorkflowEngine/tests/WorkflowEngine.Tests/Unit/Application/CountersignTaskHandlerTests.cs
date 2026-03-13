using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Commands.Approval;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Tests.Helpers;

namespace WorkflowEngine.Tests.Unit.Application;

public class CountersignTaskHandlerTests
{
    [Fact]
    public async Task Countersign_ShouldInsertNewStepAfterCurrent()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (instance, step, task) = await TestDataBuilder.SeedSingleStepProcessAsync(ctx, "approver-1");
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act
        var newStepId = await mediator.Send(new CountersignTaskCommand(
            task.Id, "approver-1",
            [new ConfirmedAssignee("approver-extra")],
            "Need extra review"));

        // Assert
        var newStep = ctx.ApprovalSteps.FirstOrDefault(s => s.Id == newStepId);
        newStep.Should().NotBeNull();
        newStep!.StepIndex.Should().Be(2m); // 1 + 1 (no next step)
        newStep.Source.Should().Be(StepSource.Countersign);
        newStep.Assignees.Should().ContainSingle(a => a.UserId == "approver-extra");
    }

    [Fact]
    public async Task Countersign_BetweenTwoSteps_ShouldUseMidpointIndex()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var def = await TestDataBuilder.SeedDefinitionAsync(ctx);

        var instance = new Domain.Entities.ProcessInstance
        {
            Id = Guid.NewGuid(), DefinitionId = def.Id, DefinitionVersion = 1,
            BusinessKey = "BK-CS", FormDataSnapshotJson = "{}", SubmittedBy = "user-1",
            Status = ProcessStatus.Running, CurrentStepIndex = 1m,
        };
        ctx.ProcessInstances.Add(instance);

        var step1 = new Domain.Entities.ApprovalStep
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepIndex = 1m,
            Type = StepType.Approval, Assignees = [new Domain.Common.StepAssignee("approver-1")],
            Status = StepStatus.Active, Source = StepSource.Original,
        };
        var step2 = new Domain.Entities.ApprovalStep
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepIndex = 2m,
            Type = StepType.Approval, Assignees = [new Domain.Common.StepAssignee("approver-2")],
            Status = StepStatus.Pending, Source = StepSource.Original,
        };
        ctx.ApprovalSteps.AddRange(step1, step2);

        var task = new Domain.Entities.WorkflowTask
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepId = step1.Id,
            AssigneeId = "approver-1", Status = Domain.Enums.TaskStatus.Pending,
        };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();

        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act
        var newStepId = await mediator.Send(new CountersignTaskCommand(
            task.Id, "approver-1", [new ConfirmedAssignee("approver-mid")], null));

        // Assert
        var newStep = ctx.ApprovalSteps.FirstOrDefault(s => s.Id == newStepId);
        newStep.Should().NotBeNull();
        newStep!.StepIndex.Should().Be(1.5m); // midpoint of 1 and 2
    }
}
