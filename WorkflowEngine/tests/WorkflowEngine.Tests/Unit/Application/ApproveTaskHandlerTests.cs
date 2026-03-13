using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Commands.Approval;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Tests.Helpers;

namespace WorkflowEngine.Tests.Unit.Application;

public class ApproveTaskHandlerTests
{
    [Fact]
    public async Task Approve_SingleStepProcess_ShouldCompleteProcess()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (instance, step, task) = await TestDataBuilder.SeedSingleStepProcessAsync(ctx, "approver-1");
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new ApproveTaskCommand(task.Id, "approver-1", "Approved"));

        // Assert
        await ctx.Entry(instance).ReloadAsync();
        instance.Status.Should().Be(ProcessStatus.Completed);
        instance.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Approve_FirstOfTwoSteps_ShouldActivateNextStep()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var def = await TestDataBuilder.SeedDefinitionAsync(ctx);

        var instance = new Domain.Entities.ProcessInstance
        {
            Id = Guid.NewGuid(), DefinitionId = def.Id, DefinitionVersion = 1,
            BusinessKey = "BK-001", FormDataSnapshotJson = "{}", SubmittedBy = "user-1",
            Status = ProcessStatus.Running, CurrentStepIndex = 1m,
        };
        ctx.ProcessInstances.Add(instance);

        var step1 = new Domain.Entities.ApprovalStep
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepIndex = 1m,
            Type = StepType.Approval,
            Assignees = [new Domain.Common.StepAssignee("approver-1")],
            Status = StepStatus.Active, Source = StepSource.Original,
        };
        var step2 = new Domain.Entities.ApprovalStep
        {
            Id = Guid.NewGuid(), InstanceId = instance.Id, StepIndex = 2m,
            Type = StepType.Approval,
            Assignees = [new Domain.Common.StepAssignee("approver-2")],
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
        await mediator.Send(new ApproveTaskCommand(task.Id, "approver-1", null));

        // Assert - process still running
        await ctx.Entry(instance).ReloadAsync();
        instance.Status.Should().Be(ProcessStatus.Running);
        instance.CurrentStepIndex.Should().Be(2m);

        // Step2 should be active
        await ctx.Entry(step2).ReloadAsync();
        step2.Status.Should().Be(StepStatus.Active);

        // New task created for approver-2
        var newTask = ctx.Tasks.FirstOrDefault(t => t.AssigneeId == "approver-2");
        newTask.Should().NotBeNull();
    }

    [Fact]
    public async Task Approve_WrongUser_ShouldThrowPermissionDenied()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (_, _, task) = await TestDataBuilder.SeedSingleStepProcessAsync(ctx, "approver-1");
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act & Assert
        await FluentActions.Awaiting(() =>
            mediator.Send(new ApproveTaskCommand(task.Id, "wrong-user", null)))
            .Should().ThrowAsync<PermissionDeniedException>();
    }

    [Fact]
    public async Task JointSign_AllPass_FirstApproval_ShouldNotAdvance()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (instance, step, tasks) = await TestDataBuilder.SeedJointSignProcessAsync(ctx, JointSignPolicy.AllPass, 2);
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act: first approver approves
        await mediator.Send(new ApproveTaskCommand(tasks[0].Id, "approver-1", null));

        // Assert: still running
        await ctx.Entry(instance).ReloadAsync();
        instance.Status.Should().Be(ProcessStatus.Running);
        instance.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task JointSign_AllPass_BothApprove_ShouldComplete()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (instance, step, tasks) = await TestDataBuilder.SeedJointSignProcessAsync(ctx, JointSignPolicy.AllPass, 2);
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new ApproveTaskCommand(tasks[0].Id, "approver-1", null));
        await mediator.Send(new ApproveTaskCommand(tasks[1].Id, "approver-2", null));

        // Assert
        await ctx.Entry(instance).ReloadAsync();
        instance.Status.Should().Be(ProcessStatus.Completed);
    }

    [Fact]
    public async Task JointSign_AnyOne_FirstApproval_ShouldComplete()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (instance, _, tasks) = await TestDataBuilder.SeedJointSignProcessAsync(ctx, JointSignPolicy.AnyOne, 3);
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act: only first person approves
        await mediator.Send(new ApproveTaskCommand(tasks[0].Id, "approver-1", null));

        // Assert: process completed
        await ctx.Entry(instance).ReloadAsync();
        instance.Status.Should().Be(ProcessStatus.Completed);
    }
}
