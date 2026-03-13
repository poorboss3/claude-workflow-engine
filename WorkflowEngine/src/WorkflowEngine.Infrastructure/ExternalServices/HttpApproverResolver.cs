using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Infrastructure.ExternalServices;

public class HttpApproverResolver : IApproverResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpApproverResolver> _logger;

    public HttpApproverResolver(IHttpClientFactory httpClientFactory, ILogger<HttpApproverResolver> logger)
    { _httpClientFactory = httpClientFactory; _logger = logger; }

    public async Task<ResolveApproversResult> ResolveAsync(ResolveApproversContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ctx.CallbackUrl))
            return new ResolveApproversResult([], null);

        var client = _httpClientFactory.CreateClient("WorkflowExtension");
        var request = new
        {
            processType = ctx.ProcessType,
            formData = ctx.FormData,
            submittedBy = ctx.SubmittedBy,
            onBehalfOf = ctx.OnBehalfOf,
            requestId = Guid.NewGuid().ToString("N"),
        };

        try
        {
            var response = await client.PostAsJsonAsync(ctx.CallbackUrl, request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ExternalResolveResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("扩展点返回数据格式错误");
            return new ResolveApproversResult(
                result.Steps.Select(s => new ResolvedStep(
                    s.StepIndex,
                    s.Type,
                    s.Assignees.Select(a => new ResolvedAssignee(a.UserId)).ToList(),
                    s.JointSignPolicy)).ToList(),
                result.Metadata);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "ApproverResolver call failed or timed out. Url={Url}", ctx.CallbackUrl);
            return new ResolveApproversResult([], null);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private record ExternalResolveResponse(List<ExternalStep> Steps, Dictionary<string, object>? Metadata);
    private record ExternalStep(decimal StepIndex, string Type, List<ExternalAssignee> Assignees, string? JointSignPolicy);
    private record ExternalAssignee(string UserId);
}
