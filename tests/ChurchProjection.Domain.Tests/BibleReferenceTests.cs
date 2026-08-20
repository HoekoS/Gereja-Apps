// Unit tests for free-form Bible reference parsing (SRS FR-LIB-08,
// TEST-CASES UNT-REF-*).
//
// This is the operator's fastest path to a passage during a live service: the
// pastor says a reference, the operator types it. It has to accept how people
// actually type — Indonesian names, English names, abbreviations, no spaces.
//
// RED PHASE: ChurchProjection.Domain does not exist yet.

using ChurchProjection.Domain.Bible;

namespace ChurchProjection.Domain.Tests;

public class BibleReferenceTests
{
    // Canonical book ids, protestant ordering.
    private const int Genesis = 1;
    private const int Psalms = 19;
    private const int John = 43;
    private const int FirstCorinthians = 46;

    [Fact]
    public void UNT_REF_01_full_indonesian_name_with_a_single_verse()
    {
        var reference = BibleReference.TryParse("Yohanes 3:16");

        Assert.Equal(new BibleReference(John, 3, 16, 16), reference);
    }

    [Fact]
    public void UNT_REF_02_indonesian_abbreviation()
    {
        var reference = BibleReference.TryParse("Yoh 3:16");

        Assert.Equal(new BibleReference(John, 3, 16, 16), reference);
    }

    [Fact]
    public void UNT_REF_03_english_name_resolves_to_the_same_canonical_book()
    {
        // FR-LIB-02: the reference is translation-independent. "John" and
        // "Yohanes" are the same book id, which is what makes SYS-BIB-04
        // possible.
        Assert.Equal(John, BibleReference.TryParse("John 3:16")!.BookId);
    }

    [Fact]
    public void UNT_REF_04_matching_is_case_insensitive() =>
        Assert.Equal(BibleReference.TryParse("Yohanes 3:16"), BibleReference.TryParse("yohanes 3:16"));

    [Fact]
    public void UNT_REF_05_a_verse_range()
    {
        var reference = BibleReference.TryParse("Kejadian 1:1-5");

        Assert.Equal(new BibleReference(Genesis, 1, 1, 5), reference);
    }

    [Fact]
    public void UNT_REF_06_a_chapter_with_no_verse_means_the_whole_chapter()
    {
        var reference = BibleReference.TryParse("Mazmur 23")!;

        Assert.Equal(Psalms, reference.BookId);
        Assert.Equal(23, reference.Chapter);
        Assert.Equal(1, reference.VerseStart);
        Assert.Null(reference.VerseEnd); // an open end means "to the end of the chapter"
    }

    [Fact]
    public void UNT_REF_07_a_leading_book_number_is_not_read_as_a_chapter()
    {
        var reference = BibleReference.TryParse("1 Korintus 13:4-7");

        Assert.Equal(new BibleReference(FirstCorinthians, 13, 4, 7), reference);
    }

    [Fact]
    public void UNT_REF_08_a_numbered_book_abbreviated_with_no_space()
    {
        var reference = BibleReference.TryParse("1Kor 13:4");

        Assert.Equal(new BibleReference(FirstCorinthians, 13, 4, 4), reference);
    }

    [Fact]
    public void UNT_REF_09_unrecognised_input_returns_null_rather_than_throwing() =>
        // The operator is typing mid-service. Every keystroke hits this method,
        // and most prefixes are not yet valid references. A thrown exception per
        // keystroke is both a cost and a noise source in the logs.
        Assert.Null(BibleReference.TryParse("asdf"));

    [Fact]
    public void UNT_REF_10_empty_input_returns_null()
    {
        Assert.Null(BibleReference.TryParse(""));
        Assert.Null(BibleReference.TryParse("   "));
        Assert.Null(BibleReference.TryParse(null));
    }

    [Fact]
    public void UNT_REF_11_book_ids_are_not_capped_at_66()
    {
        // FR-LIB-03. A deuterocanonical translation must be storable without a
        // schema change, so nothing in the reference layer may clamp the range.
        Assert.Null(Record.Exception(() => BibleReference.TryParse("Tobit 1:1")));
    }

    [Fact]
    public void UNT_REF_12_surrounding_whitespace_is_tolerated() =>
        Assert.Equal(BibleReference.TryParse("Yohanes 3:16"), BibleReference.TryParse("  Yohanes 3:16  "));

    [Fact]
    public void UNT_REF_13_a_reversed_range_is_rejected() =>
        // "Kejadian 1:5-1" is a typo, not a passage. Returning it would ask the
        // library for an empty set and put a blank slide on the screen.
        Assert.Null(BibleReference.TryParse("Kejadian 1:5-1"));
}
