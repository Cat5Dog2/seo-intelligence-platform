namespace SeoIntelligence.Application.Redis;

public interface IRedisCoordinator
{
    Task<TimeSpan> PingAsync(CancellationToken cancellationToken = default);

    Task SetStringAsync(RedisKey key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    Task<string?> GetStringAsync(RedisKey key, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(RedisKey key, CancellationToken cancellationToken = default);

    Task<IRedisLease?> TryAcquireLockAsync(
        RedisKey key,
        string owner,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}

public interface IRedisLease : IAsyncDisposable
{
    RedisKey Key { get; }

    string Owner { get; }
}

public readonly record struct RedisKey
{
    public RedisKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Redis key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
