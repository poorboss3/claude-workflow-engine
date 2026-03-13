using System.Net;
using System.Text.Json;
using WorkflowEngine.API.Models.Responses;
using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        var (statusCode, code, message, details) = ex switch
        {
            NotFoundException e => (HttpStatusCode.NotFound, e.Code, e.Message, (object?)null),
            PermissionDeniedException e => (HttpStatusCode.Forbidden, e.Code, e.Message, null),
            PermissionValidationFailedException e => (HttpStatusCode.UnprocessableEntity, e.Code, e.Message, (object?)e.FailedItems),
            StepNotModifiableException e => (HttpStatusCode.Conflict, e.Code, e.Message, null),
            ConcurrentConflictException e => (HttpStatusCode.Conflict, e.Code, e.Message, null),
            WorkflowException e => (HttpStatusCode.BadRequest, e.Code, e.Message, null),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred", null),
        };

        context.Response.StatusCode = (int)statusCode;
        var response = ApiResponse<object>.Fail(code, message, details);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
