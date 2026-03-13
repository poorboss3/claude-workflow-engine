using System.Text.Json;
using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Events;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.Submission;

public class SubmitProcessHandler : IRequestHandler<SubmitProcessCommand, ProcessInstanceDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IApproverResolver _resolver;
    private readonly IPermissionValidator _validator;
    private readonly IMediator _mediator;

    public SubmitProcessHandler(IUnitOfWork uow, IApproverResolver resolver,
        IPermissionValidator validator, IMediator mediator)
    {
        _uow = uow;
        _resolver = resolver;
        _validator = validator;
        _mediator = mediator;
    }

    public async Task<ProcessInstanceDto> Handle(SubmitProcessCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        var definition = await _uow.ProcessDefinitions.GetActiveByProcessTypeAsync(cmd.ProcessType, ct)
            ?? throw new NotFoundException("ProcessDefinition", cmd.ProcessType);

        // 代提交权限校验
        if (cmd.OnBehalfOf != null && cmd.SubmittedBy != cmd.OnBehalfOf)
        {
            var proxyConfigs = await _uow.ProxyConfigs.FindActiveAsync(
                cmd.SubmittedBy, cmd.OnBehalfOf, DateTime.UtcNow, ct);
            var hasProxy = proxyConfigs.Any(c =>
                !c.AllowedProcessTypes.Any() || c.AllowedProcessTypes.Contains(cmd.ProcessType));
            if (!hasProxy)
                throw new PermissionDeniedException($"User '{cmd.SubmittedBy}' has no proxy permission for '{cmd.OnBehalfOf}'");
        }

        // 获取原始审批人列表（用于 diff 记录）
        var originalResult = await _resolver.ResolveAsync(new ResolveApproversContext(
            cmd.ProcessType, cmd.FormData, cmd.SubmittedBy, cmd.OnBehalfOf,
            definition.ApproverResolverUrl), ct);

        var diff = DiffCalculator.Calculate(originalResult.Steps, cmd.ConfirmedSteps);

        // 权限验证
        var validationResult = await _validator.ValidateAsync(new ValidatePermissionsContext(
            cmd.ProcessType, cmd.FormData, cmd.SubmittedBy,
            originalResult.Steps, cmd.ConfirmedSteps, diff.Any(),
            definition.PermissionValidatorUrl), ct);

        if (!validationResult.Passed)
            throw new PermissionValidationFailedException(validationResult.FailedItems, validationResult.Message);

        // 委托透明替换
        var stepsAfterDelegation = await ApplyDelegationAsync(cmd.ConfirmedSteps, cmd.ProcessType, ct);

        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            DefinitionVersion = definition.Version,
            BusinessKey = cmd.BusinessKey,
            FormDataSnapshotJson = JsonSerializer.Serialize(cmd.FormData),
            SubmittedBy = cmd.SubmittedBy,
            OnBehalfOf = cmd.OnBehalfOf,
            Status = ProcessStatus.Running,
        };
        _uow.ProcessInstances.Add(instance);

        var steps = stepsAfterDelegation
            .Select(s => new ApprovalStep
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                StepIndex = s.StepIndex,
                Type = Enum.Parse<StepType>(s.Type, true),
                Assignees = s.Assignees.Select(a => new StepAssignee(a.UserId, a.OriginalUserId ?? "") { IsDelegated = a.IsDelegated }).ToList(),
                JointSignPolicy = s.JointSignPolicy != null ? Enum.Parse<JointSignPolicy>(s.JointSignPolicy, true) : null,
                Status = StepStatus.Pending,
                Source = StepSource.Original,
            })
            .OrderBy(s => s.StepIndex)
            .ToList();

        foreach (var step in steps) _uow.ApprovalSteps.Add(step);

        // 保存修改记录
        _uow.ApproverModifications.Add(new ApproverListModification
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            ModifiedBy = cmd.SubmittedBy,
            OriginalStepsJson = JsonSerializer.Serialize(originalResult.Steps),
            FinalStepsJson = JsonSerializer.Serialize(cmd.ConfirmedSteps),
            DiffSummaryJson = JsonSerializer.Serialize(diff),
        });

        // 激活第一步
        var firstStep = steps.First();
        firstStep.Status = StepStatus.Active;
        instance.CurrentStepIndex = firstStep.StepIndex;

        var newTasks = CreateTasksForStep(firstStep, instance);
        foreach (var t in newTasks) _uow.Tasks.Add(t);

        await _uow.CommitAsync(ct);
        await _mediator.Publish(new ProcessSubmittedEvent(instance.Id, newTasks), ct);

        return MapToDto(instance, definition.Name, steps);
    }

    private List<WorkflowTask> CreateTasksForStep(ApprovalStep step, ProcessInstance instance)
        => step.Assignees.Select(a => new WorkflowTask
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepId = step.Id,
            AssigneeId = a.UserId,
            OriginalAssigneeId = a.IsDelegated ? a.OriginalUserId : null,
            IsDelegated = a.IsDelegated,
            Status = Domain.Enums.TaskStatus.Pending,
            IsUrgent = instance.IsUrgent,
        }).ToList();

    private async Task<List<ConfirmedStep>> ApplyDelegationAsync(
        List<ConfirmedStep> steps, string processType, CancellationToken ct)
    {
        var allUserIds = steps.SelectMany(s => s.Assignees.Select(a => a.UserId)).Distinct().ToList();
        var delegations = await _uow.DelegationConfigs.GetActiveForUsersAsync(allUserIds, processType, DateTime.UtcNow, ct);
        var delegationMap = delegations.GroupBy(d => d.DelegatorId).ToDictionary(g => g.Key, g => g.First());

        return steps.Select(step => step with
        {
            Assignees = step.Assignees.Select(a =>
            {
                if (delegationMap.TryGetValue(a.UserId, out var d))
                    return new ConfirmedAssignee(d.DelegateeId, a.UserId, true);
                return a;
            }).ToList()
        }).ToList();
    }

    private ProcessInstanceDto MapToDto(ProcessInstance instance, string processName, List<ApprovalStep> steps)
        => new()
        {
            Id = instance.Id,
            DefinitionId = instance.DefinitionId,
            ProcessName = processName,
            BusinessKey = instance.BusinessKey,
            SubmittedBy = instance.SubmittedBy,
            OnBehalfOf = instance.OnBehalfOf,
            Status = instance.Status.ToString(),
            IsUrgent = instance.IsUrgent,
            CurrentStepIndex = instance.CurrentStepIndex,
            CreatedAt = instance.CreatedAt,
            Steps = steps.Select(s => new ApprovalStepDto
            {
                Id = s.Id,
                StepIndex = s.StepIndex,
                Type = s.Type.ToString(),
                Status = s.Status.ToString(),
                Source = s.Source.ToString(),
                Assignees = s.Assignees.Select(a => new AssigneeDto { UserId = a.UserId, OriginalUserId = a.OriginalUserId, IsDelegated = a.IsDelegated }).ToList(),
                JointSignPolicy = s.JointSignPolicy?.ToString(),
            }).ToList()
        };
}
