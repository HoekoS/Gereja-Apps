// Unit tests for the import parsers (SRS FR-IMP-01 to FR-IMP-07, IF-SW-04,
// TEST-CASES UNT-IMP-*).
//
// The contract that matters most here is atomicity's precondition: a parser must
// complete or throw, and must never hand back a partial record set (FR-IMP-05).
// A half-imported Bible is the failure this suite exists to prevent.
//
// RED PHASE: ChurchProjection.Infrastructure does not exist yet.

using System.Text;

using ChurchProjection.Application.Import;
using ChurchProjection.Infrastructure.Import;

namespace ChurchProjection.Application.Tests;

public class ImportParserTests
{
    private static readonly ImportService Import = ImportService.WithDefaultParsers();

    private static ImportPayload Parse(string fixtureName)
    {
        using var stream = File.OpenRead(Path.Combine("fixtures", fixtureName));
        return Import.Parse(stream, fixtureName);
    }

    private static ImportPayload ParseText(string content, string fileName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return Import.Parse(stream, fileName);
    }

    // --- plain text songs ---------------------------------------------------

    [Fact]
    public void UNT_IMP_01_the_first_line_becomes_the_title()
    {
        var payload = Parse("song-plain.txt");

        Assert.Equal(ImportKind.Song, payload.Kind);
        Assert.Equal("Kasih Setia-Mu", payload.Songs[0].Title);
    }

    [Fact]
    public void UNT_IMP_02_blank_lines_split_pages_in_order()
    {
        var song = Parse("song-plain.txt").Songs[0];

        Assert.Equal(4, song.Pages.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, song.Pages.Select(p => p.Position));
        Assert.StartsWith("Kasih setia-Mu", song.Pages[0].Text);
    }

    [Fact]
    public void UNT_IMP_03_a_bracketed_line_becomes_the_following_page_section_label()
    {
        var song = Parse("song-plain.txt").Songs[0];

        Assert.Equal("Reff", song.Pages[1].SectionLabel);
        Assert.DoesNotContain("[Reff]", song.Pages[1].Text); // the label line is consumed, not left in the text
    }

    [Fact]
    public void UNT_IMP_04_a_colon_terminated_line_becomes_the_following_page_section_label()
    {
        var song = Parse("song-plain.txt").Songs[0];

        Assert.Equal("Bait 2", song.Pages[2].SectionLabel);
    }

    [Fact]
    public void UNT_IMP_05_CRITICAL_section_labels_are_stored_verbatim()
    {
        // FR-LIB-12 / URS-SONG-04. This congregation writes "Reff". Not "Chorus",
        // not "REFF", not a normalised enum value.
        var labels = Parse("song-plain.txt").Songs[0].Pages.Select(p => p.SectionLabel);

        Assert.Equal(new string?[] { null, "Reff", "Bait 2", "Reff" }, labels);
    }

    [Fact]
    public void UNT_IMP_06_an_empty_file_is_rejected()
    {
        var error = Assert.Throws<ImportException>(() => ParseText("", "empty.txt"));

        Assert.False(string.IsNullOrWhiteSpace(error.Detail));
    }

    [Fact]
    public void UNT_IMP_07_a_title_with_no_pages_is_rejected() =>
        Assert.Throws<ImportException>(() => ParseText("Judul Saja\n", "title-only.txt"));

    [Fact]
    public void UNT_IMP_13_crlf_line_endings_parse_identically_to_lf()
    {
        // Lyrics arrive pasted out of Word, on Windows.
        var text = File.ReadAllText(Path.Combine("fixtures", "song-plain.txt"));

        var lf = ParseText(text.ReplaceLineEndings("\n"), "a.txt").Songs[0];
        var crlf = ParseText(text.ReplaceLineEndings("\r\n"), "b.txt").Songs[0];

        Assert.Equal(lf.Title, crlf.Title);
        Assert.Equal(
            lf.Pages.Select(p => (p.Position, p.SectionLabel, p.Text)),
            crlf.Pages.Select(p => (p.Position, p.SectionLabel, p.Text)));
    }

    // --- Zefania XML Bibles -------------------------------------------------

    [Fact]
    public void UNT_IMP_08_zefania_xml_parses_to_bible_records()
    {
        var payload = Parse("bible-zefania.xml");

        Assert.Equal(ImportKind.Bible, payload.Kind);
        Assert.NotEmpty(payload.Verses);

        var verse = payload.Verses.Single(v => v.Chapter == 1 && v.Verse == 1);
        Assert.True(verse.BookId > 0);
        Assert.False(string.IsNullOrWhiteSpace(verse.Text));
    }

    [Fact]
    public void UNT_IMP_09_malformed_xml_is_rejected_with_a_locating_detail()
    {
        var error = Assert.Throws<ImportException>(() => Parse("bible-zefania-malformed.xml"));

        // FR-ADM-02 requires telling the administrator what failed. "Import
        // failed" is not an acceptable message to hand a volunteer at 9pm on a
        // Saturday.
        Assert.False(string.IsNullOrWhiteSpace(error.Detail));
    }

    // --- OpenLyrics ---------------------------------------------------------

    [Fact]
    public void UNT_IMP_10_openlyrics_parses_title_author_and_ordered_pages()
    {
        var payload = Parse("song-openlyrics.xml");
        var song = payload.Songs[0];

        Assert.Equal(ImportKind.Song, payload.Kind);
        Assert.Equal("Amazing Grace", song.Title);
        Assert.Equal("John Newton", song.Author);
        Assert.True(song.Pages.Count >= 2);
        Assert.Equal(Enumerable.Range(0, song.Pages.Count), song.Pages.Select(p => p.Position));
    }

    // --- the shared contract ------------------------------------------------

    [Theory]
    [InlineData("song-plain.txt", ImportKind.Song)]
    [InlineData("bible-zefania.xml", ImportKind.Bible)]
    [InlineData("song-openlyrics.xml", ImportKind.Song)]
    public void UNT_IMP_11_every_parser_returns_the_same_shape(string fixtureName, ImportKind expected)
    {
        var payload = Parse(fixtureName);

        Assert.Equal(expected, payload.Kind);

        // IF-SW-04: one payload type, and the collection matching the kind is the
        // populated one. A parser that fills both has misreported what it read.
        if (expected == ImportKind.Song)
        {
            Assert.NotEmpty(payload.Songs);
            Assert.Empty(payload.Verses);
        }
        else
        {
            Assert.NotEmpty(payload.Verses);
            Assert.Empty(payload.Songs);
        }
    }

    [Fact]
    public void UNT_IMP_12_CRITICAL_a_fault_in_the_last_record_yields_nothing_at_all()
    {
        // The fixture is valid until its final verse. If a parser streamed
        // records out as it went, a caller could write 30,000 verses and then
        // fail — the half-imported Bible. Nothing may escape.
        //
        // The signature is what enforces this: Parse returns a completed payload
        // or throws. There is no IEnumerable to yield partway through, and this
        // test fails to compile if one is ever introduced.
        ImportPayload? escaped = null;

        Assert.Throws<ImportException>(() => escaped = Parse("bible-zefania-truncated.xml"));
        Assert.Null(escaped);
    }
}
