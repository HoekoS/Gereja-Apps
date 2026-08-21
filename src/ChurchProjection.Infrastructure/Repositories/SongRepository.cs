using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class SongRepository(ProjectionDbContext db) : ISongRepository
{
    public async Task<Song?> FindAsync(SongId id, CancellationToken ct)
    {
        var song = await db.Songs.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);

        song?.Pages.Sort((left, right) => left.Position.CompareTo(right.Position));

        return song;
    }

    public Task<Song?> FindByTitleAsync(string title, CancellationToken ct) =>
        db.Songs.SingleOrDefaultAsync(s => s.Title.ToLower() == title.ToLower(), ct);

    public async Task<IReadOnlyList<SongHit>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            // An empty search lists the library. The operator opening the song
            // panel has not typed anything yet and still wants to see songs.
            return await db.Songs.AsNoTracking()
                .OrderBy(s => s.Title)
                .Take(limit)
                .Select(s => new SongHit(s.Id.Value, s.Title, s.Author, s.Language))
                .ToListAsync(ct);
        }

        const string sql = """
            SELECT s.id       AS Id,
                   s.title    AS Title,
                   s.author   AS Author,
                   s.language AS Language
            FROM songs_fts f
            JOIN songs s ON s.id = f.song_id
            WHERE songs_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;

        return await db.Database.SqlQueryRaw<SongHit>(
                sql,
                new SqliteParameter("@query", VerseRepository.AsPhrase(query)),
                new SqliteParameter("@limit", limit))
            .ToListAsync(ct);
    }

    public async Task<SongId> UpsertAsync(Song song, CancellationToken ct)
    {
        if (db.Entry(song).State == EntityState.Detached)
        {
            db.Songs.Add(song);
        }

        await db.SaveChangesAsync(ct);

        return song.Id;
    }
}
