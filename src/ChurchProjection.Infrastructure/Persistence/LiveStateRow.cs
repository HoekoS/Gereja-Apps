namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>
/// One row, id 1, rewritten on every command. A table rather than a file so it
/// shares the database's durability guarantees — this is what makes a restart
/// mid-service invisible to the congregation (FR-LIV-13).
/// </summary>
public sealed class LiveStateRow
{
    public int Id { get; init; } = 1;

    public string? ServiceId { get; set; }

    public string? LiveItemId { get; set; }

    public int LivePageIndex { get; set; }

    public bool LiveMediaAvailable { get; set; }

    public string? PreviewItemId { get; set; }

    public int PreviewPageIndex { get; set; }

    public bool PreviewMediaAvailable { get; set; }

    public bool Blackout { get; set; }

    public required string SkippedJson { get; set; }

    public DateTime UpdatedAt { get; set; }
}
