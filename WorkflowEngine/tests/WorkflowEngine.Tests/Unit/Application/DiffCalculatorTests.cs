using FluentAssertions;
using WorkflowEngine.Application.Commands.Submission;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Tests.Unit.Application;

public class DiffCalculatorTests
{
    [Fact]
    public void Calculate_NoChanges_ShouldReturnEmptyDiff()
    {
        var original = new List<ResolvedStep>
        {
            new(1m, "Approval", [new ResolvedAssignee("user-1")], null)
        };
        var confirmed = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("user-1")], null)
        };

        var diff = DiffCalculator.Calculate(original, confirmed);
        diff.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_AddedAssignee_ShouldDetectAdded()
    {
        var original = new List<ResolvedStep>
        {
            new(1m, "Approval", [new ResolvedAssignee("user-1")], null)
        };
        var confirmed = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("user-1"), new ConfirmedAssignee("user-2")], null)
        };

        var diff = DiffCalculator.Calculate(original, confirmed);
        diff.Should().ContainSingle(d => d.Action == "added" && d.AssigneeId == "user-2");
    }

    [Fact]
    public void Calculate_RemovedAssignee_ShouldDetectRemoved()
    {
        var original = new List<ResolvedStep>
        {
            new(1m, "Approval", [new ResolvedAssignee("user-1"), new ResolvedAssignee("user-2")], null)
        };
        var confirmed = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("user-1")], null)
        };

        var diff = DiffCalculator.Calculate(original, confirmed);
        diff.Should().ContainSingle(d => d.Action == "removed" && d.RemovedAssigneeId == "user-2");
    }

    [Fact]
    public void Calculate_NewStepAdded_ShouldDetectAllAssigneesAsAdded()
    {
        var original = new List<ResolvedStep>
        {
            new(1m, "Approval", [new ResolvedAssignee("user-1")], null)
        };
        var confirmed = new List<ConfirmedStep>
        {
            new(1m, "Approval", [new ConfirmedAssignee("user-1")], null),
            new(1.5m, "Approval", [new ConfirmedAssignee("user-extra")], null),
        };

        var diff = DiffCalculator.Calculate(original, confirmed);
        diff.Should().ContainSingle(d => d.Action == "added" && d.StepIndex == 1.5m && d.AssigneeId == "user-extra");
    }
}
