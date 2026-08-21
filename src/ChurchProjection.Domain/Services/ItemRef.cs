namespace ChurchProjection.Domain.Services;

/// <summary>
/// The kind-specific payload of a service item. One nullable-heavy record
/// rather than a class hierarchy: it is stored as a single JSON column and
/// every consumer switches on Kind anyway.
/// </summary>
public sealed record ItemRef
{
    public string? TranslationId { get; init; }

    public int? BookId { get; init; }

    public int? Chapter { get; init; }

    public int? VerseStart { get; init; }

    public int? VerseEnd { get; init; }

    public string? SongId { get; init; }

    public string? MediaId { get; init; }

    public string? Text { get; init; }

    public string? TargetTime { get; init; }
}
