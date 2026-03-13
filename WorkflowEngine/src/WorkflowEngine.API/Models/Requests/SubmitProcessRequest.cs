using WorkflowEngine.Application.Services;

namespace WorkflowEngine.API.Models.Requests;

public class PrepareSubmitRequest
{
    public string ProcessType { get; set; } = "";
    public Dictionary<string, object> FormData { get; set; } = [];
    public string? OnBehalfOf { get; set; }
}

public class SubmitProcessRequest
{
    public string ProcessType { get; set; } = "";
    public string BusinessKey { get; set; } = "";
    public Dictionary<string, object> FormData { get; set; } = [];
    public string? OnBehalfOf { get; set; }
    public List<ConfirmedStepRequest> ConfirmedSteps { get; set; } = [];
}

public class ConfirmedStepRequest
{
    public decimal StepIndex { get; set; }
    public string Type { get; set; } = "Approval";
    public List<ConfirmedAssigneeRequest> Assignees { get; set; } = [];
    public string? JointSignPolicy { get; set; }
}

public class ConfirmedAssigneeRequest
{
    public string UserId { get; set; } = "";
}
