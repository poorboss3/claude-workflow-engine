using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.Infrastructure.ExternalServices;

public class HttpPermissionValidator : IPermissionValidator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpPermissionValidator> _logger;

    public HttpPermissionValidator(IHttpClientFactory httpClientFactory, ILogger<HttpPermissionValidator> logger)
    { _httpClientFactory = httpClientFactory; _logger = logger; }

    public async Task<ValidatePermissionsResult> ValidateAsync(ValidatePermissionsContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ctx.CallbackUrl))
            return new ValidatePermissionsResult(true, [], "");

        var client = _httpClientFactory.CreateClient("WorkflowExtension");
        var request = new
        {
            processType = ctx.ProcessType,
            formData = ctx.FormData,
            submittedBy = ctx.SubmittedBy,
            originalSteps = ctx.OriginalSteps,
            finalSteps = ctx.FinalSteps,
            isModified = ctx.IsModified,
            requestId = Guid.NewGuid().ToString("N"),
        };

        try
        {
            var response = await client.PostAsJsonAsync(ctx.CallbackUrl, request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ExternalValidationResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("扩展点返回数据格式错误");
            return new ValidatePermissionsResult(
                result.Passed,
                result.FailedItems.Select(f => new PermissionFailItem(f.StepIndex, f.AssigneeId, f.Reason)).ToList(),
                result.Message ?? "");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "PermissionValidator call failed. Url={Url}", ctx.CallbackUrl);
            // Fallback: pass validation on timeout
            return new ValidatePermissionsResult(true, [], "");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private record ExternalValidationResponse(bool Passed, List<ExternalFailItem> FailedItems, string? Message);
    private record ExternalFailItem(decimal StepIndex, string AssigneeId, string Reason);
}
