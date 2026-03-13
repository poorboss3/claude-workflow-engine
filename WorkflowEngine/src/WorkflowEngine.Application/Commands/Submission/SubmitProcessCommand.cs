using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application.Commands.Submission;

public record SubmitProcessCommand(
    string ProcessType,
    string BusinessKey,
    Dictionary<string, object> FormData,
    string SubmittedBy,
    string? OnBehalfOf,
    List<ConfirmedStep> ConfirmedSteps) : IRequest<ProcessInstanceDto>;
