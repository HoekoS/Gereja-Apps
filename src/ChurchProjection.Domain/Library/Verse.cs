namespace ChurchProjection.Domain.Library;

public sealed class Verse
{
    public long Id { get; init; }

    public required TranslationId TranslationId { get; init; }

    public required int BookId { get; init; }

    public required int Chapter { get; init; }

    public required int Number { get; init; }

    public required string Text { get; init; }
}
