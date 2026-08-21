using ChurchProjection.Api.Access;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Api.Endpoints;

public static class BibleEndpoints
{
    private const int SearchCap = 100;

    public static void MapBible(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequirePair();

        group.MapGet("/translations", async (ITranslationRepository translations, CancellationToken ct) =>
        {
            var all = await translations.ListAsync(ct);

            return Results.Json(all.Select(t => new
            {
                id = t.Id.Value,
                abbrev = t.Abbrev,
                name = t.Name,
                language = t.Language,
            }));
        });

        group.MapGet("/bible/reference", (string? q) =>
        {
            // 404 rather than 400: the operator types into this field one
            // character at a time and most prefixes are not references yet.
            // A 400 storm in the log hides the failures that matter.
            if (BibleReference.TryParse(q ?? string.Empty) is not { } reference)
            {
                return ApiError.NotFound(
                    "UNPARSEABLE_REFERENCE", $"'{q}' is not a book, chapter and verse.");
            }

            return Results.Json(new
            {
                bookId = reference.BookId,
                chapter = reference.Chapter,
                verseStart = reference.VerseStart,
                verseEnd = reference.VerseEnd,
            });
        });

        group.MapGet("/bible/passage", async (
            string translationId,
            int bookId,
            int chapter,
            int? verseStart,
            int? verseEnd,
            IVerseRepository verses,
            CancellationToken ct) =>
        {
            var reference = new BibleReference(bookId, chapter, verseStart ?? 1, verseEnd);

            var passage = await verses.GetAsync(new TranslationId(translationId), reference, ct);

            if (passage is null)
            {
                return ApiError.NotFound(
                    "PASSAGE_NOT_FOUND", "That passage is not in this translation.");
            }

            return Results.Json(new
            {
                translationId = passage.TranslationId.Value,
                bookId = passage.BookId,
                bookName = passage.BookName,
                chapter = passage.Chapter,
                verses = passage.Verses.Select(v => new { verse = v.Number, text = v.Text }),
            });
        });

        group.MapGet("/bible/search", async (
            string? q, string? translationId, IVerseRepository verses, CancellationToken ct) =>
        {
            var translation = string.IsNullOrWhiteSpace(translationId)
                ? (TranslationId?)null
                : new TranslationId(translationId);

            var results = await verses.SearchAsync(translation, q ?? string.Empty, SearchCap, ct);

            return Results.Json(new { results });
        });
    }
}
