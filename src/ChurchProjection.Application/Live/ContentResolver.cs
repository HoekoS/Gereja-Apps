// src/ChurchProjection.Application/Live/ContentResolver.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Live;

/// <summary>
/// Turns an item and a page number into the words the output view paints. This
/// travels inside the state so the projector never has to ask a second question
/// — one round trip, and no window where the state says one thing and the
/// screen shows another (FR-LIV-11).
/// </summary>
public sealed class ContentResolver(
    ISongRepository songs, IVerseRepository verses, IMediaRepository media)
{
    public async Task<int> PageCountAsync(ServiceItem item, CancellationToken ct) => item.Kind switch
    {
        "song" when item.Ref.SongId is { } id =>
            await songs.FindAsync(new SongId(id), ct) is { } song ? Math.Max(song.Pages.Count, 1) : 1,
        _ => 1,
    };

    public async Task<object?> ResolveAsync(ServiceItem item, int pageIndex, CancellationToken ct)
    {
        switch (item.Kind)
        {
            case "song" when item.Ref.SongId is { } songId:
            {
                var song = await songs.FindAsync(new SongId(songId), ct);
                var page = song?.Pages.OrderBy(p => p.Position).ElementAtOrDefault(pageIndex);

                return page is null
                    ? null
                    : new { kind = "song", title = song!.Title, sectionLabel = page.SectionLabel, text = page.Text };
            }

            case "bible" when item.Ref.TranslationId is { } translationId
                              && item.Ref.BookId is { } bookId
                              && item.Ref.Chapter is { } chapter:
            {
                var reference = new BibleReference(
                    bookId, chapter, item.Ref.VerseStart ?? 1, item.Ref.VerseEnd);
                var passage = await verses.GetAsync(new TranslationId(translationId), reference, ct);

                return passage is null
                    ? null
                    : new
                    {
                        kind = "bible",
                        reference = $"{passage.BookName} {passage.Chapter}:{item.Ref.VerseStart}",
                        translationId = passage.TranslationId.Value,
                        verses = passage.Verses.Select(v => new { verse = v.Number, text = v.Text }),
                    };
            }

            case "slide":
                return new { kind = "slide", text = item.Ref.Text ?? string.Empty };

            case "media" when item.Ref.MediaId is { } mediaId:
            {
                var found = await media.FindAsync(new MediaId(mediaId), ct);

                return found is null
                    ? null
                    : new
                    {
                        kind = "media",
                        mediaKind = found.Kind,
                        url = $"/api/media/{found.Id.Value}/stream",
                        durationMs = found.DurationMs,
                    };
            }

            case "countdown":
                return new { kind = "countdown", targetTime = item.Ref.TargetTime };

            default:
                return null;
        }
    }

    public async Task<IReadOnlySet<string>> UnavailableAsync(ServicePlan plan, CancellationToken ct)
    {
        var unavailable = new HashSet<string>();

        foreach (var item in plan.Items.Where(i => i.Kind == "media" && i.Ref.MediaId is not null))
        {
            if (!await media.IsAvailableAsync(new MediaId(item.Ref.MediaId!), ct))
            {
                unavailable.Add(item.Id);
            }
        }

        return unavailable;
    }
}
