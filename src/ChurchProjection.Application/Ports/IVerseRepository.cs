using ChurchProjection.Application.Import;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

/// <summary>A search hit. Flat and already carrying its book name, because the
/// client renders the list without a second round trip.</summary>
public sealed record VerseHit(
    string TranslationId,
    int BookId,
    string BookName,
    int Chapter,
    int Verse,
    string Text);

public interface IVerseRepository
{
    Task<Passage?> GetAsync(TranslationId translation, BibleReference reference, CancellationToken ct);

    /// <summary>Full-text search. <paramref name="translation"/> null searches every translation.</summary>
    Task<IReadOnlyList<VerseHit>> SearchAsync(TranslationId? translation, string query, int limit, CancellationToken ct);

    /// <summary>Replaces a whole translation. Individual verses are never
    /// written or deleted; a Bible is imported or it is not.</summary>
    Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct);
}
