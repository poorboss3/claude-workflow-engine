using MediatR;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application.Commands.Submission;

public record PrepareSubmitCommand(
    string ProcessType,
    Dictionary<string, object> FormData,
    string SubmittedBy,
    string? OnBehalfOf) : IRequest<PrepareSubmitResult>;

public record PrepareSubmitResult(List<ResolvedStep> DefaultSteps, Dictionary<string, object>? Metadata);
