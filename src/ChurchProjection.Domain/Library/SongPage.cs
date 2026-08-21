namespace ChurchProjection.Domain.Library;

/// <summary>One projected page. SectionLabel is free text because the church
/// writes "Reff", not "chorus".</summary>
public sealed class SongPage
{
    public required int Position { get; set; }

    public string? SectionLabel { get; set; }

    public required string Text { get; set; }
}
