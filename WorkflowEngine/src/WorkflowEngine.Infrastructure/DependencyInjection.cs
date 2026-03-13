using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Infrastructure.Caching;
using WorkflowEngine.Infrastructure.ExternalServices;
using WorkflowEngine.Infrastructure.Messaging;
using WorkflowEngine.Infrastructure.Messaging.Consumers;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowInfrastructure(
        this IServiceCollection services,
        IConfiguration config,
        bool useInMemory = false)
    {
        if (useInMemory)
        {
            services.AddDbContext<WorkflowDbContext>(opt =>
                opt.UseInMemoryDatabase($"WorkflowTestDb_{Guid.NewGuid()}"));
        }
        else
        {
            services.AddDbContext<WorkflowDbContext>(opt =>
                opt.UseNpgsql(config.GetConnectionString("WorkflowDb"),
                    npgsql => npgsql.MigrationsAssembly("WorkflowEngine.Infrastructure")));
        }

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        // Individual repo registrations not needed because UnitOfWork creates them internally
        // But exposing them for direct injection if needed:
        services.AddScoped<IProcessDefinitionRepository>(sp => (IProcessDefinitionRepository)sp.GetRequiredService<IUnitOfWork>().ProcessDefinitions);
        services.AddScoped<IProcessInstanceRepository>(sp => (IProcessInstanceRepository)sp.GetRequiredService<IUnitOfWork>().ProcessInstances);
        services.AddScoped<ITaskRepository>(sp => (ITaskRepository)sp.GetRequiredService<IUnitOfWork>().Tasks);

        // Extension point services
        services.AddScoped<IApproverResolver, HttpApproverResolver>();
        services.AddScoped<IPermissionValidator, HttpPermissionValidator>();

        // HTTP client with resilience (.NET 8 built-in)
        services.AddHttpClient("WorkflowExtension")
                .AddStandardResilienceHandler(options =>
                {
                    options.Retry.MaxRetryAttempts = 2;
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                });

        // Redis
        if (!useInMemory)
        {
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379"));
            services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();
        }
        else
        {
            services.AddSingleton<IDistributedLockService, NoOpDistributedLockService>();
        }

        // MassTransit + RabbitMQ
        services.AddMassTransit(x =>
        {
            x.AddConsumer<NotificationConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10))));

            if (useInMemory)
            {
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            }
            else
            {
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Host(config.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost/");
                    cfg.ReceiveEndpoint("workflow-notifications", e =>
                    {
                        e.ConfigureConsumer<NotificationConsumer>(ctx);
                    });
                });
            }
        });
        services.AddScoped<IWorkflowNotificationPublisher, RabbitMqNotificationPublisher>();

        return services;
    }
}

// No-op lock for testing (always grants lock)
public class NoOpDistributedLockService : IDistributedLockService
{
    public Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => Task.FromResult<IAsyncDisposable?>(new NoOpLock());

    private class NoOpLock : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
