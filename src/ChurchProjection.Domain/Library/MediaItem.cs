namespace ChurchProjection.Domain.Library;

public sealed class MediaItem
{
    public required MediaId Id { get; init; }

    /// <summary>The content type the file is served as, derived from its extension.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The name inside the media root, never a full path. The media root moves
    /// when a backup is restored onto another machine; the filename does not.
    /// </summary>
    public required string Filename { get; init; }

    public int? DurationMs { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}
