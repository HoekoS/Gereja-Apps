namespace ChurchProjection.Domain.Library;

public sealed class Translation
{
    public required TranslationId Id { get; init; }

    public required string Abbrev { get; init; }

    public required string Name { get; init; }

    public required string Language { get; init; }
}
