using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Tests.Helpers;

public static class TestDbContextFactory
{
    public static WorkflowDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(dbName ?? $"TestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new WorkflowDbContext(options);
    }
}
