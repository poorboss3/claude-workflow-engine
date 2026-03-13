using FluentValidation;
using WorkflowEngine.Application.Commands.Submission;

namespace WorkflowEngine.Application.Validators;

public class SubmitProcessValidator : AbstractValidator<SubmitProcessCommand>
{
    public SubmitProcessValidator()
    {
        RuleFor(x => x.ProcessType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BusinessKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SubmittedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ConfirmedSteps).NotEmpty().WithMessage("审批步骤列表不能为空");
        RuleForEach(x => x.ConfirmedSteps).ChildRules(step =>
        {
            step.RuleFor(s => s.Assignees).NotEmpty().WithMessage("每个审批步骤至少需要一个审批人");
        });
    }
}
