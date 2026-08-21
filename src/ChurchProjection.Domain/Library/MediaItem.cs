namespace ChurchProjection.Domain.Library;

public sealed class MediaItem
{
    public required MediaId Id { get; init; }

    public required string Kind { get; init; }          // image | video | audio

    public required string Filename { get; init; }

    public required string Path { get; init; }

    public int? DurationMs { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}
