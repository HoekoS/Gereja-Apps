using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class VerseRepository(ProjectionDbContext db) : IVerseRepository
{
    public async Task<Passage?> GetAsync(TranslationId translation, BibleReference reference, CancellationToken ct)
    {
        var verseEnd = reference.VerseEnd ?? int.MaxValue;

        var verses = await db.Verses.AsNoTracking()
            .Where(v => v.TranslationId == translation
                        && v.BookId == reference.BookId
                        && v.Chapter == reference.Chapter
                        && v.Number >= reference.VerseStart
                        && v.Number <= verseEnd)
            .OrderBy(v => v.Number)
            .ToListAsync(ct);

        if (verses.Count == 0)
        {
            return null;
        }

        var bookName = await db.BookNames.AsNoTracking()
            .Where(b => b.TranslationId == translation.Value && b.BookId == reference.BookId)
            .Select(b => b.Name)
            .SingleOrDefaultAsync(ct);

        return new Passage(
            translation,
            reference.BookId,
            bookName ?? BookNames.Name(reference.BookId) ?? $"Book {reference.BookId}",
            reference.Chapter,
            verses);
    }

    public async Task<IReadOnlyList<VerseHit>> SearchAsync(
        TranslationId? translation, string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        const string sql = """
            SELECT v.translation_id AS TranslationId,
                   v.book_id        AS BookId,
                   COALESCE(b.name, '') AS BookName,
                   v.chapter        AS Chapter,
                   v.verse          AS Verse,
                   v.text           AS Text
            FROM verses_fts f
            JOIN verses v ON v.id = f.rowid
            LEFT JOIN book_names b
                   ON b.translation_id = v.translation_id AND b.book_id = v.book_id
            WHERE verses_fts MATCH @query
              AND (@translation IS NULL OR v.translation_id = @translation)
            ORDER BY rank
            LIMIT @limit
            """;

        return await db.Database.SqlQueryRaw<VerseHit>(
                sql,
                new SqliteParameter("@query", AsPhrase(query)),
                new SqliteParameter("@translation", (object?)translation?.Value ?? DBNull.Value),
                new SqliteParameter("@limit", limit))
            .ToListAsync(ct);
    }

    public async Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct)
    {
        var translation = payload.Translation
            ?? throw new InvalidOperationException("A Bible payload must carry its translation.");

        // Replace, never merge. A partially overwritten translation is a Bible
        // with two editions of the same verse in it.
        await db.Verses.Where(v => v.TranslationId == new TranslationId(translation.Id)).ExecuteDeleteAsync(ct);
        await db.BookNames.Where(b => b.TranslationId == translation.Id).ExecuteDeleteAsync(ct);

        if (await db.Translations.FindAsync([new TranslationId(translation.Id)], ct) is null)
        {
            db.Translations.Add(new Translation
            {
                Id = translation.Id,
                Abbrev = translation.Abbrev,
                Name = translation.Name,
                Language = translation.Language,
            });
        }

        db.BookNames.AddRange(translation.Books.Select(book => new BookNameRow
        {
            TranslationId = translation.Id,
            BookId = book.BookId,
            Name = book.Name,
            Abbrev = book.Abbrev,
        }));

        db.Verses.AddRange(payload.Verses.Select(verse => new Verse
        {
            TranslationId = translation.Id,
            BookId = verse.BookId,
            Chapter = verse.Chapter,
            Number = verse.Verse,
            Text = verse.Text,
        }));

        await db.SaveChangesAsync(ct);

        return payload.Verses.Count;
    }

    /// <summary>
    /// Wraps the operator's words in an FTS5 phrase. The value is still a bound
    /// parameter — this is about FTS5's own query grammar, not about SQL: an
    /// apostrophe in "Allah's" is a syntax error to a bare MATCH.
    /// </summary>
    internal static string AsPhrase(string query) => $"\"{query.Trim().Replace("\"", "\"\"")}\"";
}
