namespace ChurchProjection.Application.Import;

public enum ImportKind
{
    Bible,
    Song,
}

/// <summary>
/// A parsed file that has not been written yet. Parsing either produces a
/// complete payload or throws; nothing yields records one at a time, because a
/// stream that fails halfway is exactly the half-written import FR-IMP-07
/// forbids.
/// </summary>
public sealed record ImportPayload(
    ImportKind Kind,
    IReadOnlyList<ImportedSong> Songs,
    IReadOnlyList<ImportedVerse> Verses,
    ImportedTranslation? Translation);

public sealed record ImportedSong(
    string Title,
    string? Author,
    string? Ccli,
    string? Language,
    IReadOnlyList<ImportedPage> Pages);

public sealed record ImportedPage(int Position, string? SectionLabel, string Text);

public sealed record ImportedVerse(int BookId, int Chapter, int Verse, string Text);

public sealed record ImportedTranslation(
    string Id,
    string Abbrev,
    string Name,
    string Language,
    IReadOnlyList<ImportedBookName> Books);

public sealed record ImportedBookName(int BookId, string Name, string? Abbrev);
