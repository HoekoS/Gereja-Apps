using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>
/// The fixed content the API suite asserts against. Runs only in the Testing
/// environment and only into an empty database, so re-running the suite does
/// not stack duplicates.
/// </summary>
public static class DevSeed
{
    public static async Task ApplyAsync(ProjectionDbContext db, CancellationToken ct)
    {
        if (await db.Translations.AnyAsync(ct))
        {
            return;
        }

        db.Translations.AddRange(
            new Translation { Id = "tb", Abbrev = "TB", Name = "Terjemahan Baru", Language = "id" },
            new Translation { Id = "tl", Abbrev = "TL", Name = "Terjemahan Lama", Language = "id" });

        db.BookNames.AddRange(
            new BookNameRow { TranslationId = "tb", BookId = 1, Name = "Kejadian", Abbrev = "Kej" },
            new BookNameRow { TranslationId = "tl", BookId = 1, Name = "Kejadian", Abbrev = "Kej" },
            new BookNameRow { TranslationId = "tb", BookId = 43, Name = "Yohanes", Abbrev = "Yoh" },
            new BookNameRow { TranslationId = "tl", BookId = 43, Name = "Yahya", Abbrev = "Yah" });

        // Genesis 1:1-3 in both translations, because SYS-BIB-02 and SYS-BIB-04
        // ask for exactly that reference and then require the words to differ.
        // The word "terang" is here because SYS-BIB-05 searches for it.
        db.Verses.AddRange(
            new Verse { TranslationId = "tb", BookId = 1, Chapter = 1, Number = 1, Text = "Pada mulanya Allah menciptakan langit dan bumi" },
            new Verse { TranslationId = "tb", BookId = 1, Chapter = 1, Number = 2, Text = "Bumi belum berbentuk dan kosong, gelap gulita menutupi samudera raya" },
            new Verse { TranslationId = "tb", BookId = 1, Chapter = 1, Number = 3, Text = "Berfirmanlah Allah: Jadilah terang. Lalu terang itu jadi" },
            new Verse { TranslationId = "tl", BookId = 1, Chapter = 1, Number = 1, Text = "Bahwa pada mula pertama dijadikan Allah akan langit dan bumi" },
            new Verse { TranslationId = "tl", BookId = 1, Chapter = 1, Number = 2, Text = "Maka bumi itu lagi campur baur adanya, sunyi senyap" },
            new Verse { TranslationId = "tl", BookId = 1, Chapter = 1, Number = 3, Text = "Maka firman Allah: Hendaklah ada terang, lalu terang itu pun jadilah" },
            new Verse { TranslationId = "tb", BookId = 43, Chapter = 3, Number = 16, Text = "Karena begitu besar kasih Allah akan dunia ini, sehingga Ia telah mengaruniakan Anak-Nya yang tunggal" },
            new Verse { TranslationId = "tb", BookId = 43, Chapter = 3, Number = 17, Text = "Sebab Allah mengutus Anak-Nya ke dalam dunia bukan untuk menghakimi dunia" },
            new Verse { TranslationId = "tl", BookId = 43, Chapter = 3, Number = 16, Text = "Karena demikianlah Allah mengasihi isi dunia ini" });

        // SYS-SNG-01 searches the title for "Kasih"; SYS-SNG-02 searches for
        // "berkesudahan", which appears in the lyrics and in no title — that is
        // what proves the index covers more than titles. SYS-SNG-03 wants a
        // page labelled "Reff" and an author and CCLI number on the song.
        var song = new Song
        {
            Id = "song_seed",
            Title = "Kasih Setia-Mu",
            Author = "Tim Pujian",
            Ccli = "1234567",
            Language = "id",
        };
        song.Pages.Add(new SongPage { Position = 0, SectionLabel = null, Text = "Kasih setia-Mu tak pernah berubah" });
        song.Pages.Add(new SongPage { Position = 1, SectionLabel = "Reff", Text = "Rahmat-Nya tidak berkesudahan, selalu baru setiap pagi" });
        song.Pages.Add(new SongPage { Position = 2, SectionLabel = "Bait 2", Text = "Setiap pagi baru kurasakan" });
        db.Songs.Add(song);
        // SYS-MED-00 through SYS-MED-03 need both halves of the media failure:
        // a file that is there, and a row whose file was moved. The present file
        // is written here so the fixtures folder does not need a binary, and it
        // is deliberately larger than the 1 KiB range SYS-MED-01 asks for.
        var mediaRoot = Path.GetDirectoryName(db.Database.GetDbConnection().DataSource)!;
        mediaRoot = Path.Combine(mediaRoot, "media");
        Directory.CreateDirectory(mediaRoot);

        var presentPath = Path.Combine(mediaRoot, "seed-clip.bin");
        await File.WriteAllBytesAsync(presentPath, new byte[64 * 1024], ct);

        db.Media.AddRange(
            new MediaItem
            {
                Id = "med_present",
                Kind = "video/mp4",
                Filename = "seed-clip.bin",
                Path = presentPath,
                DurationMs = 12_000,
                Width = 1920,
                Height = 1080,
            },
            new MediaItem
            {
                Id = "med_missing",
                Kind = "image/jpeg",
                Filename = "moved-by-someone.jpg",
                Path = Path.Combine(mediaRoot, "moved-by-someone.jpg"),
            });

        await db.SaveChangesAsync(ct);
    }
}
