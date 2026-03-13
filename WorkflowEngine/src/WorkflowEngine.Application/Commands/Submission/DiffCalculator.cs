using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application.Commands.Submission;

public static class DiffCalculator
{
    public static List<DiffEntry> Calculate(List<ResolvedStep> original, List<ConfirmedStep> confirmed)
    {
        var result = new List<DiffEntry>();
        var originalByIndex = original.ToDictionary(s => s.StepIndex);
        var confirmedByIndex = confirmed.ToDictionary(s => s.StepIndex);

        foreach (var (index, step) in confirmedByIndex)
        {
            if (!originalByIndex.ContainsKey(index))
                foreach (var a in step.Assignees)
                    result.Add(new DiffEntry("added", index, a.UserId, null));
        }

        foreach (var (index, step) in originalByIndex)
        {
            if (!confirmedByIndex.ContainsKey(index))
                foreach (var a in step.Assignees)
                    result.Add(new DiffEntry("removed", index, null, a.UserId));
        }

        foreach (var (index, confirmedStep) in confirmedByIndex)
        {
            if (!originalByIndex.TryGetValue(index, out var originalStep)) continue;
            var origIds = originalStep.Assignees.Select(a => a.UserId).ToHashSet();
            var confIds = confirmedStep.Assignees.Select(a => a.UserId).ToHashSet();
            foreach (var id in confIds.Except(origIds))
                result.Add(new DiffEntry("added", index, id, null));
            foreach (var id in origIds.Except(confIds))
                result.Add(new DiffEntry("removed", index, null, id));
        }

        return result;
    }
}

public record DiffEntry(string Action, decimal StepIndex, string? AssigneeId, string? RemovedAssigneeId);
