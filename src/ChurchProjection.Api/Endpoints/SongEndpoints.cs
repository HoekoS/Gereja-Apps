using ChurchProjection.Api.Access;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Api.Endpoints;

public static class SongEndpoints
{
    private const int SearchCap = 100;

    public static void MapSongs(this WebApplication app)
    {
        var group = app.MapGroup("/api/songs").RequirePair();

        group.MapGet("/", async (string? q, ISongRepository songs, CancellationToken ct) =>
        {
            var results = await songs.SearchAsync(q ?? string.Empty, SearchCap, ct);

            return Results.Json(new { results });
        });

        group.MapGet("/{id}", async (string id, ISongRepository songs, CancellationToken ct) =>
        {
            var song = await songs.FindAsync(new SongId(id), ct);

            if (song is null)
            {
                return ApiError.NotFound("SONG_NOT_FOUND", "That song is not in the library.");
            }

            return Results.Json(new
            {
                id = song.Id.Value,
                title = song.Title,
                author = song.Author,
                ccli = song.Ccli,
                language = song.Language,
                pages = song.Pages
                    .OrderBy(page => page.Position)
                    .Select(page => new { position = page.Position, sectionLabel = page.SectionLabel, text = page.Text }),
            });
        });
    }
}
