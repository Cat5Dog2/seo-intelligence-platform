using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace SeoIntelligence.Web.Services;

public sealed class GuestDemoSessionStore(TimeProvider timeProvider) : IDisposable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private readonly MemoryCache _sessions = new(new MemoryCacheOptions { SizeLimit = 200 });
    private readonly Lock _gate = new();

    public string Create()
    {
        lock (_gate)
        {
            if (_sessions.Count >= 200) _sessions.Compact(0.1);
            var id = Guid.NewGuid().ToString("N");
            var session = new GuestDemoSession(timeProvider.GetUtcNow().Add(Lifetime));
            _sessions.Set(id, session, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Lifetime,
                Size = 1
            });
            return id;
        }
    }

    public GuestDemoSession? Find(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is not null
            && _sessions.TryGetValue<GuestDemoSession>(id, out var session)
            && session!.ExpiresAt > timeProvider.GetUtcNow()
                ? session
                : null;
    }

    public void Remove(ClaimsPrincipal principal)
    {
        if (principal.FindFirstValue(ClaimTypes.NameIdentifier) is { } id)
        {
            _sessions.Remove(id);
        }
    }

    public void Dispose() => _sessions.Dispose();
}
