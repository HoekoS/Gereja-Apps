namespace ChurchProjection.Domain.Library;

public sealed class Song
{
    public required SongId Id { get; set; }

    public required string Title { get; set; }

    public string? Author { get; set; }

    public string? Ccli { get; set; }

    public string? Language { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<SongPage> Pages { get; init; } = [];
}
