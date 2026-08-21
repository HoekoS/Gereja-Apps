using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchProjection.Infrastructure.Persistence.Migrations;

/// <summary>
/// FTS5 virtual tables and the triggers that keep them in step. EF cannot model
/// a virtual table, so this migration is raw SQL and stays raw SQL. Nothing
/// outside VerseRepository.SearchAsync and SongRepository.SearchAsync may touch
/// these tables.
/// </summary>
public partial class Fts5Search : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE VIRTUAL TABLE verses_fts USING fts5(
                text,
                content='verses',
                content_rowid='id',
                tokenize='unicode61 remove_diacritics 2');
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER verses_fts_ai AFTER INSERT ON verses BEGIN
                INSERT INTO verses_fts(rowid, text) VALUES (new.id, new.text);
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER verses_fts_ad AFTER DELETE ON verses BEGIN
                INSERT INTO verses_fts(verses_fts, rowid, text) VALUES ('delete', old.id, old.text);
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER verses_fts_au AFTER UPDATE ON verses BEGIN
                INSERT INTO verses_fts(verses_fts, rowid, text) VALUES ('delete', old.id, old.text);
                INSERT INTO verses_fts(rowid, text) VALUES (new.id, new.text);
            END;
            """);

        // songs_fts is a plain (not external-content) table because one row has
        // to carry a title from one table and lyrics from another.
        migrationBuilder.Sql("""
            CREATE VIRTUAL TABLE songs_fts USING fts5(
                song_id UNINDEXED,
                title,
                text,
                tokenize='unicode61 remove_diacritics 2');
            """);

        foreach (var (name, table, timing, id) in new[]
        {
            ("songs_fts_ai", "songs", "AFTER INSERT", "new.id"),
            ("songs_fts_au", "songs", "AFTER UPDATE", "new.id"),
            ("song_pages_fts_ai", "song_pages", "AFTER INSERT", "new.song_id"),
            ("song_pages_fts_au", "song_pages", "AFTER UPDATE", "new.song_id"),
            ("song_pages_fts_ad", "song_pages", "AFTER DELETE", "old.song_id"),
        })
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER {name} {timing} ON {table} BEGIN
                    DELETE FROM songs_fts WHERE song_id = {id};
                    INSERT INTO songs_fts(song_id, title, text)
                        SELECT s.id,
                               s.title,
                               COALESCE((SELECT group_concat(p.text, ' ')
                                         FROM song_pages p WHERE p.song_id = s.id), '')
                        FROM songs s WHERE s.id = {id};
                END;
                """);
        }

        migrationBuilder.Sql("""
            CREATE TRIGGER songs_fts_ad AFTER DELETE ON songs BEGIN
                DELETE FROM songs_fts WHERE song_id = old.id;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var trigger in new[]
        {
            "verses_fts_ai", "verses_fts_ad", "verses_fts_au",
            "songs_fts_ai", "songs_fts_au", "songs_fts_ad",
            "song_pages_fts_ai", "song_pages_fts_au", "song_pages_fts_ad",
        })
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");
        }

        migrationBuilder.Sql("DROP TABLE IF EXISTS verses_fts;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS songs_fts;");
    }
}
