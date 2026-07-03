using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Redis;
using StackExchange.Redis;
using ApplicationRedisKey = SeoIntelligence.Application.Redis.RedisKey;

namespace SeoIntelligence.Infrastructure.Redis;

internal sealed class StackExchangeRedisCoordinator : IRedisCoordinator, IAsyncDisposable
{
    private readonly ConfigurationOptions configurationOptions;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private ConnectionMultiplexer? connection;

    public StackExchangeRedisCoordinator(IOptions<RedisOptions> options)
    {
        var errors = options.Value.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        configurationOptions = ConfigurationOptions.Parse(options.Value.ConnectionString!);
        configurationOptions.AbortOnConnectFail = false;
        configurationOptions.ClientName ??= "SeoIntelligence";
    }

    public async Task<TimeSpan> PingAsync(CancellationToken cancellationToken = default)
    {
        var database = await GetDatabaseAsync(cancellationToken);
        return await database.PingAsync();
    }

    public async Task SetStringAsync(
        ApplicationRedisKey key,
        string value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabaseAsync(cancellationToken);
        await database.StringSetAsync(key.Value, value, expiry, When.Always);
    }

    public async Task<string?> GetStringAsync(ApplicationRedisKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabaseAsync(cancellationToken);
        var value = await database.StringGetAsync(key.Value);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<bool> RemoveAsync(ApplicationRedisKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabaseAsync(cancellationToken);
        return await database.KeyDeleteAsync(key.Value);
    }

    public async Task<IRedisLease?> TryAcquireLockAsync(
        ApplicationRedisKey key,
        string owner,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("Redis lock owner is required.", nameof(owner));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Redis lock ttl must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var database = await GetDatabaseAsync(cancellationToken);
        var acquired = await database.LockTakeAsync(key.Value, owner, ttl);

        return acquired
            ? new RedisLease(this, key, owner)
            : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.CloseAsync();
            connection.Dispose();
        }

        connectionLock.Dispose();
    }

    private async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        var multiplexer = await GetConnectionAsync(cancellationToken);
        return multiplexer.GetDatabase();
    }

    private async Task<ConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken)
    {
        // AbortOnConnectFail=false のため、切断からの復旧はConnectionMultiplexerの
        // 内部再接続に任せる。共有中の接続を破棄して作り直すと、他スレッドの
        // 実行中操作を巻き込むため行わない。
        if (connection is not null)
        {
            return connection;
        }

        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            connection ??= await ConnectionMultiplexer.ConnectAsync(configurationOptions).WaitAsync(cancellationToken);
            return connection;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private async ValueTask ReleaseLockAsync(ApplicationRedisKey key, string owner)
    {
        var database = await GetDatabaseAsync(CancellationToken.None);
        await database.LockReleaseAsync(key.Value, owner);
    }

    private sealed class RedisLease(
        StackExchangeRedisCoordinator coordinator,
        ApplicationRedisKey key,
        string owner)
        : IRedisLease
    {
        public ApplicationRedisKey Key { get; } = key;

        public string Owner { get; } = owner;

        public async ValueTask DisposeAsync()
        {
            await coordinator.ReleaseLockAsync(Key, Owner);
        }
    }
}
