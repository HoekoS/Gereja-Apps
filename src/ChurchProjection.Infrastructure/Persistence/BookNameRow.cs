namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>A book's name in one translation's own language (FR-LIB-04).</summary>
public sealed class BookNameRow
{
    public required string TranslationId { get; init; }

    public required int BookId { get; init; }

    public required string Name { get; init; }

    public string? Abbrev { get; init; }
}
