using System.Text.Json;

using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ChurchProjection.Infrastructure.Caching;

/// <summary>
/// Caches passage reads. A use case cannot tell this apart from the EF
/// repository, which is the point: the cache is an implementation detail of
/// reading verses, not a thing the application knows about (NFR-REL-09).
///
/// Search is deliberately not cached. The query space is whatever the operator
/// types, so the hit rate would be near zero and every miss would pay for a
/// round trip it did not need.
/// </summary>
public sealed class CachedVerseRepository(
    IVerseRepository inner,
    IDistributedCache cache,
    CacheGeneration generation,
    ILogger<CachedVerseRepository> logger) : IVerseRepository
{
    private static readonly DistributedCacheEntryOptions Options = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
    };

    private static bool _reported;

    public async Task<Passage?> GetAsync(
        TranslationId translation, BibleReference reference, CancellationToken ct)
    {
        var key = $"passage:v{generation.Current}:{translation.Value}:" +
                  $"{reference.BookId}:{reference.Chapter}:{reference.VerseStart}:{reference.VerseEnd}";

        if (await TryReadAsync(key, ct) is { } cached)
        {
            return cached;
        }

        var passage = await inner.GetAsync(translation, reference, ct);

        if (passage is not null)
        {
            await TryWriteAsync(key, passage, ct);
        }

        return passage;
    }

    public Task<IReadOnlyList<VerseHit>> SearchAsync(
        TranslationId? translation, string query, int limit, CancellationToken ct) =>
        inner.SearchAsync(translation, query, limit, ct);

    public async Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct)
    {
        var written = await inner.ReplaceTranslationAsync(payload, ct);

        // After the write, so a failed import leaves the cache alone.
        generation.Bump();

        return written;
    }

    private async Task<Passage?> TryReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);

            return bytes is null ? null : JsonSerializer.Deserialize<Passage>(bytes);
        }
        catch (Exception ex)
        {
            ReportOnce(ex);

            return null;
        }
    }

    private async Task TryWriteAsync(string key, Passage passage, CancellationToken ct)
    {
        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(passage), Options, ct);
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private void ReportOnce(Exception ex)
    {
        if (_reported)
        {
            return;
        }

        _reported = true;
        logger.LogWarning(ex, "Cache unavailable; serving verses from the database.");
    }
}
