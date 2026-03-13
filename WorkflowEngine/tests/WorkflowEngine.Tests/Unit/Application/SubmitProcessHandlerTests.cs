using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Commands.Submission;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Tests.Fakes;
using WorkflowEngine.Tests.Helpers;

namespace WorkflowEngine.Tests.Unit.Application;

public class SubmitProcessHandlerTests
{
    [Fact]
    public async Task Submit_ValidRequest_ShouldCreateInstanceAndActivateFirstStep()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        await TestDataBuilder.SeedDefinitionAsync(ctx, "expense_report");
        var notifications = new FakeNotificationPublisher();
        var sp = ServiceHelper.BuildTestServiceProvider(ctx, publisher: notifications);
        var mediator = sp.GetRequiredService<IMediator>();

        var confirmedSteps = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("approver-1")], null)
        };

        // Act
        var result = await mediator.Send(new SubmitProcessCommand(
            "expense_report", "EXP-001",
            new() { ["amount"] = 5000 },
            "user-1", null, confirmedSteps));

        // Assert
        result.Should().NotBeNull();
        result.BusinessKey.Should().Be("EXP-001");
        result.Status.Should().Be("Running");
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Status.Should().Be("Active");

        // Task created
        var task = ctx.Tasks.FirstOrDefault(t => t.AssigneeId == "approver-1");
        task.Should().NotBeNull();
        task!.Status.Should().Be(Domain.Enums.TaskStatus.Pending);

        // Notification published
        notifications.Published.Should().ContainSingle(n => n.EventType == "task_assigned" && n.RecipientId == "approver-1");
    }

    [Fact]
    public async Task Submit_WithProxyPermission_ShouldSucceed()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        await TestDataBuilder.SeedDefinitionAsync(ctx, "expense_report");

        // Setup proxy config: agent-1 can submit on behalf of user-2
        ctx.ProxyConfigs.Add(new Domain.Entities.ProxyConfig
        {
            Id = Guid.NewGuid(),
            AgentId = "agent-1",
            PrincipalId = "user-2",
            AllowedProcessTypes = [],
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(1),
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        var confirmedSteps = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("approver-1")], null)
        };

        // Act & Assert: should not throw
        var result = await mediator.Send(new SubmitProcessCommand(
            "expense_report", "EXP-002",
            new(), "agent-1", "user-2", confirmedSteps));

        result.SubmittedBy.Should().Be("agent-1");
        result.OnBehalfOf.Should().Be("user-2");
    }

    [Fact]
    public async Task Submit_WithoutProxyPermission_ShouldThrowPermissionDenied()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        await TestDataBuilder.SeedDefinitionAsync(ctx, "expense_report");
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        var confirmedSteps = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("approver-1")], null)
        };

        // Act & Assert
        await FluentActions.Awaiting(() =>
            mediator.Send(new SubmitProcessCommand(
                "expense_report", "EXP-003",
                new(), "agent-unauthorized", "user-2", confirmedSteps)))
            .Should().ThrowAsync<PermissionDeniedException>();
    }

    [Fact]
    public async Task Submit_WithPermissionValidationFail_ShouldThrow()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        await TestDataBuilder.SeedDefinitionAsync(ctx, "expense_report");
        var fakeValidator = new FakePermissionValidator
        {
            ShouldPass = false,
            FailItems = [new Domain.Exceptions.PermissionFailItem(1m, "approver-1", "Quota exceeded")],
            FailMessage = "Approver quota exceeded"
        };
        var sp = ServiceHelper.BuildTestServiceProvider(ctx, validator: fakeValidator);
        var mediator = sp.GetRequiredService<IMediator>();

        var confirmedSteps = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("approver-1")], null)
        };

        // Act & Assert
        var ex = await FluentActions.Awaiting(() =>
            mediator.Send(new SubmitProcessCommand(
                "expense_report", "EXP-004",
                new() { ["amount"] = 999999 }, "user-1", null, confirmedSteps)))
            .Should().ThrowAsync<PermissionValidationFailedException>();
        ex.Which.FailedItems.Should().ContainSingle(f => f.AssigneeId == "approver-1");
    }

    [Fact]
    public async Task Submit_WithDelegation_ShouldReplaceAssigneeTransparently()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        await TestDataBuilder.SeedDefinitionAsync(ctx, "expense_report");

        // approver-original has delegated to approver-delegate
        ctx.DelegationConfigs.Add(new Domain.Entities.DelegationConfig
        {
            Id = Guid.NewGuid(),
            DelegatorId = "approver-original",
            DelegateeId = "approver-delegate",
            AllowedProcessTypes = [],
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(1),
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        var confirmedSteps = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("approver-original")], null)
        };

        // Act
        await mediator.Send(new SubmitProcessCommand(
            "expense_report", "EXP-005", new(), "user-1", null, confirmedSteps));

        // Assert: task assigned to delegate, original preserved
        var task = ctx.Tasks.FirstOrDefault();
        task.Should().NotBeNull();
        task!.AssigneeId.Should().Be("approver-delegate");
        task.OriginalAssigneeId.Should().Be("approver-original");
        task.IsDelegated.Should().BeTrue();
    }
}
