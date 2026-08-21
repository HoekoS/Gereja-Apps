using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Import;

public sealed record ImportOutcome(string Kind, int Imported, int Updated);

/// <summary>
/// Parse, then write everything in one transaction. Parsing happens outside the
/// transaction on purpose: a file that cannot be read must not even open one.
/// </summary>
public sealed class ImportLibrary(
    IImportReader reader,
    IVerseRepository verses,
    ISongRepository songs,
    IUnitOfWork unitOfWork)
{
    public Task<ImportOutcome> ExecuteAsync(Stream file, string fileName, CancellationToken ct)
    {
        var payload = reader.Parse(file, fileName);

        return unitOfWork.InTransactionAsync(token => payload.Kind switch
        {
            ImportKind.Bible => StoreBibleAsync(payload, token),
            _ => StoreSongsAsync(payload, token),
        }, ct);
    }

    private async Task<ImportOutcome> StoreBibleAsync(ImportPayload payload, CancellationToken ct)
    {
        var written = await verses.ReplaceTranslationAsync(payload, ct);

        return new ImportOutcome("bible", written, 0);
    }

    private async Task<ImportOutcome> StoreSongsAsync(ImportPayload payload, CancellationToken ct)
    {
        var imported = 0;
        var updated = 0;

        foreach (var incoming in payload.Songs)
        {
            var existing = await songs.FindByTitleAsync(incoming.Title, ct);

            var song = existing ?? new Song
            {
                Id = Guid.NewGuid().ToString("n"),
                Title = incoming.Title,
            };

            song.Title = incoming.Title;
            song.Author = incoming.Author;
            song.Ccli = incoming.Ccli;
            song.Language = incoming.Language;
            song.UpdatedAt = DateTime.UtcNow;

            // Re-importing a song replaces its pages outright. That is the point:
            // the operator fixed a typo in the second verse and expects the fixed
            // verse on the screen, not a fifth copy of the song (FR-IMP-04).
            song.Pages.Clear();
            song.Pages.AddRange(incoming.Pages.Select(page => new SongPage
            {
                Position = page.Position,
                SectionLabel = page.SectionLabel,
                Text = page.Text,
            }));

            await songs.UpsertAsync(song, ct);

            if (existing is null)
            {
                imported++;
            }
            else
            {
                updated++;
            }
        }

        return new ImportOutcome("song", imported, updated);
    }
}
