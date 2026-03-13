using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Infrastructure;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Tests.Fakes;

namespace WorkflowEngine.Tests.Helpers;

public static class ServiceHelper
{
    public static IServiceProvider BuildTestServiceProvider(
        WorkflowDbContext? ctx = null,
        FakeApproverResolver? resolver = null,
        FakePermissionValidator? validator = null,
        FakeNotificationPublisher? publisher = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        if (ctx != null)
        {
            services.AddSingleton(ctx);
            services.AddScoped<IUnitOfWork>(sp =>
                new UnitOfWork(sp.GetRequiredService<WorkflowDbContext>()));
        }
        else
        {
            services.AddWorkflowInfrastructure(
                new ConfigurationBuilder().Build(), useInMemory: true);
        }

        services.AddWorkflowApplication();

        // 始终注册 fake 扩展点（覆盖 Infrastructure 注册的真实实现）
        services.AddSingleton<IApproverResolver>(resolver ?? new FakeApproverResolver());
        services.AddSingleton<IPermissionValidator>(validator ?? new FakePermissionValidator());
        services.AddSingleton<IWorkflowNotificationPublisher>(publisher ?? new FakeNotificationPublisher());
        services.AddSingleton<IDistributedLockService>(new NoOpDistributedLockService());

        return services.BuildServiceProvider();
    }
}
