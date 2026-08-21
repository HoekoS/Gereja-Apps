using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

public sealed record SongHit(string Id, string Title, string? Author, string? Language);

public interface ISongRepository
{
    Task<Song?> FindAsync(SongId id, CancellationToken ct);

    Task<Song?> FindByTitleAsync(string title, CancellationToken ct);

    /// <summary>Title and lyric search. An empty query lists everything.</summary>
    Task<IReadOnlyList<SongHit>> SearchAsync(string query, int limit, CancellationToken ct);

    Task<SongId> UpsertAsync(Song song, CancellationToken ct);
}
