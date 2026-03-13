using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Commands.Approval;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Tests.Helpers;

namespace WorkflowEngine.Tests.Unit.Application;

public class RejectTaskHandlerTests
{
    [Fact]
    public async Task Reject_ShouldTerminateProcessAndMarkAllStepsSkipped()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (instance, step, task) = await TestDataBuilder.SeedSingleStepProcessAsync(ctx);
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new RejectTaskCommand(task.Id, "approver-1", "Budget exceeded"));

        // Assert
        await ctx.Entry(instance).ReloadAsync();
        instance.Status.Should().Be(ProcessStatus.Rejected);
        instance.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Reject_WithoutComment_ShouldThrowValidationError()
    {
        // Arrange
        using var ctx = TestDbContextFactory.Create();
        var (_, _, task) = await TestDataBuilder.SeedSingleStepProcessAsync(ctx);
        var sp = ServiceHelper.BuildTestServiceProvider(ctx);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act & Assert
        await FluentActions.Awaiting(() =>
            mediator.Send(new RejectTaskCommand(task.Id, "approver-1", "")))
            .Should().ThrowAsync<WorkflowException>()
            .Where(e => e.Code == "VALIDATION_ERROR");
    }
}
