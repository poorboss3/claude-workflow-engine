namespace WorkflowEngine.API.Models.Requests;

public class ApproveTaskRequest
{
    public string? Comment { get; set; }
}

public class RejectTaskRequest
{
    public string Comment { get; set; } = "";
}

public class ReturnTaskRequest
{
    public Guid? TargetStepId { get; set; }
    public string Comment { get; set; } = "";
}

public class CountersignRequest
{
    public List<CountersignAssigneeRequest> Assignees { get; set; } = [];
    public string? Comment { get; set; }
}

public class CountersignAssigneeRequest
{
    public string UserId { get; set; } = "";
}

public class InsertStepRequest
{
    public Guid AfterStepId { get; set; }
    public string Type { get; set; } = "Approval";
    public List<ConfirmedAssigneeRequest> Assignees { get; set; } = [];
    public string? JointSignPolicy { get; set; }
}

public class ReplaceAssigneesRequest
{
    public List<ConfirmedAssigneeRequest> Assignees { get; set; } = [];
}

public class ReorderStepsRequest
{
    public List<Guid> OrderedStepIds { get; set; } = [];
}

public class MarkUrgentRequest
{
    public string? Reason { get; set; }
}
