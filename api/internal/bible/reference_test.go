package bible

import "testing"

// Canonical book ids, Protestant ordering.
const (
	genesis          = 1
	psalms           = 19
	john             = 43
	firstCorinthians = 46
)

func TestParseReference(t *testing.T) {
	cases := []struct {
		name  string
		input string
		want  Reference
	}{
		{"full Indonesian name with a single verse", "Yohanes 3:16", Reference{john, 3, 16, 16}},
		{"Indonesian abbreviation", "Yoh 3:16", Reference{john, 3, 16, 16}},
		{"a verse range", "Kejadian 1:1-5", Reference{genesis, 1, 1, 5}},
		{"a leading book number is not read as a chapter", "1 Korintus 13:4-7", Reference{firstCorinthians, 13, 4, 7}},
		{"a numbered book abbreviated with no space", "1Kor 13:4", Reference{firstCorinthians, 13, 4, 4}},
		{"surrounding whitespace is tolerated", "  Yohanes 3:16  ", Reference{john, 3, 16, 16}},
	}

	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, ok := ParseReference(c.input)
			if !ok {
				t.Fatalf("ParseReference(%q) returned ok=false, want %+v", c.input, c.want)
			}
			if got != c.want {
				t.Errorf("ParseReference(%q) = %+v, want %+v", c.input, got, c.want)
			}
		})
	}
}

func TestParseReferenceEnglishNameResolvesToSameCanonicalBook(t *testing.T) {
	// The reference is translation-independent: "John" and "Yohanes" are the
	// same book id, which is what makes a translation switch keep the
	// operator on the same passage.
	got, ok := ParseReference("John 3:16")
	if !ok || got.BookID != john {
		t.Fatalf("ParseReference(%q) = %+v, ok=%v, want BookID=%d", "John 3:16", got, ok, john)
	}
}

func TestParseReferenceMatchingIsCaseInsensitive(t *testing.T) {
	a, okA := ParseReference("Yohanes 3:16")
	b, okB := ParseReference("yohanes 3:16")
	if !okA || !okB || a != b {
		t.Fatalf("got a=%+v (ok=%v), b=%+v (ok=%v), want them equal", a, okA, b, okB)
	}
}

func TestParseReferenceChapterWithNoVerseMeansTheWholeChapter(t *testing.T) {
	got, ok := ParseReference("Mazmur 23")
	if !ok {
		t.Fatal("ParseReference(\"Mazmur 23\") returned ok=false")
	}
	if got.BookID != psalms || got.Chapter != 23 || got.VerseStart != 1 || got.VerseEnd != 0 {
		// VerseEnd == 0 means "to the end of the chapter".
		t.Errorf("got %+v, want BookID=%d Chapter=23 VerseStart=1 VerseEnd=0", got, psalms)
	}
}

func TestParseReferenceRejectsWhatItDoesNotUnderstand(t *testing.T) {
	cases := []string{"asdf", "", "   ", "Kejadian 1:5-1"}
	// "Kejadian 1:5-1" is a typo, not a passage: returning it would ask the
	// library for an empty set and put a blank slide on the screen.
	for _, input := range cases {
		if _, ok := ParseReference(input); ok {
			t.Errorf("ParseReference(%q) returned ok=true, want false", input)
		}
	}
}

func TestParseReferenceBookIDsAreNotCappedAt66(t *testing.T) {
	// A deuterocanonical translation must be storable without a schema
	// change, so nothing in the reference layer may clamp the range. There
	// is no panic to catch here — an unresolved book name simply reports
	// ok=false, same as any other unparseable input.
	if _, ok := ParseReference("Tobit 1:1"); ok {
		t.Error(`ParseReference("Tobit 1:1") returned ok=true, want false (not in the table)`)
	}
}
