namespace ChurchProjection.Domain.Library;

/// <summary>A resolved run of verses, ready to render. BookName is in the
/// translation's own language, which is why it travels with the passage
/// rather than being looked up by the client.</summary>
public sealed record Passage(
    TranslationId TranslationId,
    int BookId,
    string BookName,
    int Chapter,
    IReadOnlyList<Verse> Verses);
