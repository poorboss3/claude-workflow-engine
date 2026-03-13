using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkflowEngine.Application;
using WorkflowEngine.Application.Services;
using WorkflowEngine.API.Middleware;
using WorkflowEngine.API.Services;
using WorkflowEngine.Infrastructure;
using FluentValidation.AspNetCore;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext());

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services.AddControllers();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "WorkflowEngine API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "JWT or X-User-Id header",
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer"
        });
    });

    var useInMemory = builder.Environment.IsEnvironment("Testing") ||
                      builder.Configuration.GetValue<bool>("UseInMemoryDb");

    builder.Services.AddWorkflowApplication();
    builder.Services.AddWorkflowInfrastructure(builder.Configuration, useInMemory);

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WorkflowEngine v1"));
    }

    app.UseAuthorization();
    app.MapControllers();

    // Apply migrations (non-testing environments)
    if (!useInMemory)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<WorkflowEngine.Infrastructure.Persistence.WorkflowDbContext>();
        await ctx.Database.MigrateAsync();
    }

    Log.Information("WorkflowEngine API starting on {Environment}", builder.Environment.EnvironmentName);
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
