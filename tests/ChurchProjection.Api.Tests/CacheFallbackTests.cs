// tests/ChurchProjection.Api.Tests/CacheFallbackTests.cs
//
// INT-14: a configured but unreachable cache degrades to the database. Every
// cache call is wrapped, not just the read — a Redis that dies between the miss
// and the write-back would otherwise throw on the way out, after the work was
// already done.

using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ChurchProjection.Api.Tests;

public class CacheFallbackTests
{
    [Fact]
    public async Task INT_14_an_unreachable_cache_still_serves_the_passage()
    {
        var logger = new CollectingLogger();
        var repository = new CachedVerseRepository(
            new StubVerseRepository(), new BrokenCache(), new CacheGeneration(), logger);

        var reference = new BibleReference(43, 3, 16, 16);

        var first = await repository.GetAsync(
            new TranslationId("tb"), reference, TestContext.Current.CancellationToken);
        var second = await repository.GetAsync(
            new TranslationId("tb"), reference, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Karena begitu besar kasih Allah", first.Verses[0].Text);

        // One warning, not one per request: a dead Redis must not fill the disk
        // with log during a service.
        Assert.Equal(1, logger.Warnings);
    }

    private sealed class BrokenCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("cache is down");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");

        public void Refresh(string key) => throw new InvalidOperationException("cache is down");

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");

        public void Remove(string key) => throw new InvalidOperationException("cache is down");

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("cache is down");

        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");
    }

    private sealed class StubVerseRepository : IVerseRepository
    {
        public Task<Passage?> GetAsync(TranslationId translation, BibleReference reference, CancellationToken ct) =>
            Task.FromResult<Passage?>(new Passage(
                translation,
                reference.BookId,
                "Yohanes",
                reference.Chapter,
                [new Verse
                {
                    TranslationId = translation,
                    BookId = reference.BookId,
                    Chapter = reference.Chapter,
                    Number = reference.VerseStart,
                    Text = "Karena begitu besar kasih Allah",
                }]));

        public Task<IReadOnlyList<VerseHit>> SearchAsync(
            TranslationId? translation, string query, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<VerseHit>>([]);

        public Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct) =>
            Task.FromResult(0);
    }

    private sealed class CollectingLogger : ILogger<CachedVerseRepository>
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings++;
            }
        }
    }
}
