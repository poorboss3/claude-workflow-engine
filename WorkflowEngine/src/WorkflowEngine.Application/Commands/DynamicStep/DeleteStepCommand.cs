using MediatR;

namespace WorkflowEngine.Application.Commands.DynamicStep;

public record DeleteStepCommand(Guid InstanceId, Guid StepId, string OperatorId) : IRequest;
