namespace ChurchProjection.Infrastructure.Caching;

/// <summary>
/// Bumped when a translation is re-imported, and folded into every cache key so
/// the old entries are unreachable rather than deleted — IDistributedCache has
/// no way to drop a prefix.
///
/// ponytail: in-process counter, correct because the booth runs exactly one
/// server. If a second process ever shares the Redis, move this to a counter
/// stored in the cache itself.
/// </summary>
public sealed class CacheGeneration
{
    private int _current;

    public int Current => Volatile.Read(ref _current);

    public void Bump() => Interlocked.Increment(ref _current);
}
