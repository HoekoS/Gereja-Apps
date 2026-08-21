// tests/ChurchProjection.Api.Tests/PersistenceTests.cs
//
// INT-15: the schema applies and the FTS5 index stays in step with the tables
// it indexes. A search index that silently stops updating is invisible until a
// Sunday when the song the operator searched for is not there.

using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Api.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"projection-{Guid.NewGuid():n}.db");

    private ProjectionDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ProjectionDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options);

    [Fact]
    public async Task INT_15_the_migrations_create_the_full_text_indexes()
    {
        await using var db = CreateContext();
        await db.ApplyMigrationsAsync(TestContext.Current.CancellationToken);

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("verses_fts", tables);
        Assert.Contains("songs_fts", tables);
    }

    [Fact]
    public async Task INT_15_inserting_a_verse_makes_it_findable()
    {
        await using var db = CreateContext();
        await db.ApplyMigrationsAsync(TestContext.Current.CancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO translations(id, abbrev, name, language) VALUES ('tb', 'TB', 'Terjemahan Baru', 'id')",
            TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO verses(translation_id, book_id, chapter, verse, text) " +
            "VALUES ('tb', 43, 3, 16, 'Karena begitu besar kasih Allah akan dunia ini')",
            TestContext.Current.CancellationToken);

        var hits = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM verses_fts WHERE verses_fts MATCH 'kasih'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, hits[0]);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
        GC.SuppressFinalize(this);
    }
}
