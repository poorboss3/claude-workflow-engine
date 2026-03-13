using StackExchange.Redis;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Infrastructure.Caching;

public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisDistributedLockService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"workflow:lock:{key}";
        var lockValue = Guid.NewGuid().ToString("N");
        var acquired = await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);
        return acquired ? new RedisLock(db, lockKey, lockValue) : null;
    }

    private sealed class RedisLock : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;

        public RedisLock(IDatabase db, string key, string value) { _db = db; _key = key; _value = value; }

        public async ValueTask DisposeAsync()
        {
            const string script = "if redis.call('get',KEYS[1])==ARGV[1] then return redis.call('del',KEYS[1]) else return 0 end";
            await _db.ScriptEvaluateAsync(script, new RedisKey[] { _key }, new RedisValue[] { _value });
        }
    }
}
