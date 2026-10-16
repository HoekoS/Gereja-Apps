package bible

import (
	"context"
	"errors"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
)

// Translation is a Bible edition: a slug id (how it was imported, e.g.
// "tb"), never a UUID — it is already a human-meaningful, non-sensitive
// identifier, so there is no public-id indirection to add.
type Translation struct {
	ID       string
	Abbrev   string
	Name     string
	Language string
}

// VerseLine is one verse of a resolved passage.
type VerseLine struct {
	Verse int
	Text  string
}

// Passage is a resolved run of verses, ready to render. BookName is in the
// translation's own language, which is why it travels with the passage
// rather than being looked up again by the client.
type Passage struct {
	TranslationID string
	BookID        int
	BookName      string
	Chapter       int
	Verses        []VerseLine
}

// VerseHit is a search result. Flat and already carrying its book name,
// because the client renders the list without a second round trip.
type VerseHit struct {
	TranslationID string
	BookID        int
	BookName      string
	Chapter       int
	Verse         int
	Text          string
}

// Store is this context's only way to reach bible_translations,
// bible_book_names and bible_verses — no other package queries these
// tables directly.
type Store struct{ pool *pgxpool.Pool }

func NewStore(pool *pgxpool.Pool) *Store { return &Store{pool: pool} }

func (s *Store) ListTranslations(ctx context.Context) ([]Translation, error) {
	rows, err := s.pool.Query(ctx,
		`SELECT id, abbrev, name, language FROM bible_translations ORDER BY id`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var translations []Translation
	for rows.Next() {
		var t Translation
		if err := rows.Scan(&t.ID, &t.Abbrev, &t.Name, &t.Language); err != nil {
			return nil, err
		}
		translations = append(translations, t)
	}
	return translations, rows.Err()
}

// Passage resolves a reference against one translation. VerseEnd of 0 (the
// whole-chapter sentinel) reaches down to the open-ended range directly,
// rather than being special-cased here.
func (s *Store) Passage(ctx context.Context, translationID string, ref Reference) (*Passage, error) {
	verseEnd := ref.VerseEnd
	if verseEnd == 0 {
		verseEnd = 1<<31 - 1 // "to the end of the chapter"
	}

	rows, err := s.pool.Query(ctx, `
		SELECT v.verse, v.text, COALESCE(b.name, '') AS book_name
		FROM bible_verses v
		LEFT JOIN bible_book_names b
		       ON b.translation_id = v.translation_id AND b.book_id = v.book_id
		WHERE v.translation_id = $1 AND v.book_id = $2 AND v.chapter = $3
		  AND v.verse >= $4 AND v.verse <= $5
		ORDER BY v.verse`,
		translationID, ref.BookID, ref.Chapter, ref.VerseStart, verseEnd)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	passage := &Passage{TranslationID: translationID, BookID: ref.BookID, Chapter: ref.Chapter}
	for rows.Next() {
		var line VerseLine
		if err := rows.Scan(&line.Verse, &line.Text, &passage.BookName); err != nil {
			return nil, err
		}
		passage.Verses = append(passage.Verses, line)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	if len(passage.Verses) == 0 {
		return nil, nil
	}
	if passage.BookName == "" {
		if name, ok := BookName(ref.BookID); ok {
			passage.BookName = name
		}
	}
	return passage, nil
}

// Search finds verses whose text matches query, in translation when it is
// non-nil, across every translation otherwise. Results are capped at limit
// by the caller's SQL, not filtered after the fact, so a common word cannot
// pull thirty thousand rows off a booth machine mid-service.
func (s *Store) Search(ctx context.Context, translationID *string, query string, limit int) ([]VerseHit, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT v.translation_id, v.book_id, COALESCE(b.name, '') AS book_name,
		       v.chapter, v.verse, v.text
		FROM bible_verses v
		LEFT JOIN bible_book_names b
		       ON b.translation_id = v.translation_id AND b.book_id = v.book_id
		WHERE v.search @@ phraseto_tsquery('simple', $1)
		  AND ($2::text IS NULL OR v.translation_id = $2)
		ORDER BY v.translation_id, v.book_id, v.chapter, v.verse
		LIMIT $3`,
		query, translationID, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var hits []VerseHit
	for rows.Next() {
		var h VerseHit
		if err := rows.Scan(&h.TranslationID, &h.BookID, &h.BookName, &h.Chapter, &h.Verse, &h.Text); err != nil {
			return nil, err
		}
		hits = append(hits, h)
	}
	return hits, rows.Err()
}

var errNoTranslation = errors.New("bible: payload must carry a translation")

// ReplaceTranslation overwrites one translation's book names and verses in
// a single transaction. Verses are never merged or patched one at a time; a
// Bible is imported whole or not at all, matching ImportLibrary.cs's
// ReplaceTranslationAsync in the .NET implementation this ports.
func (s *Store) ReplaceTranslation(ctx context.Context, translation Translation, books []ImportedBookName, verses []ImportedVerse) error {
	if translation.ID == "" {
		return errNoTranslation
	}
	return pgx.BeginFunc(ctx, s.pool, func(tx pgx.Tx) error {
		if _, err := tx.Exec(ctx, `DELETE FROM bible_verses WHERE translation_id = $1`, translation.ID); err != nil {
			return err
		}
		if _, err := tx.Exec(ctx, `DELETE FROM bible_book_names WHERE translation_id = $1`, translation.ID); err != nil {
			return err
		}
		if _, err := tx.Exec(ctx, `
			INSERT INTO bible_translations (id, abbrev, name, language) VALUES ($1, $2, $3, $4)
			ON CONFLICT (id) DO UPDATE SET abbrev = $2, name = $3, language = $4`,
			translation.ID, translation.Abbrev, translation.Name, translation.Language); err != nil {
			return err
		}
		for _, book := range books {
			if _, err := tx.Exec(ctx,
				`INSERT INTO bible_book_names (translation_id, book_id, name, abbrev) VALUES ($1, $2, $3, $4)`,
				translation.ID, book.BookID, book.Name, book.Abbrev); err != nil {
				return err
			}
		}
		batch := &pgx.Batch{}
		for _, verse := range verses {
			batch.Queue(
				`INSERT INTO bible_verses (translation_id, book_id, chapter, verse, text) VALUES ($1, $2, $3, $4, $5)`,
				translation.ID, verse.BookID, verse.Chapter, verse.Verse, verse.Text)
		}
		return tx.SendBatch(ctx, batch).Close()
	})
}

// ImportedBookName and ImportedVerse mirror the shape ImportPayload.cs
// hands the repository today. The parser that produces them (Zefania XML,
// OpenLyrics, plain text) is a later slice; this store only needs their
// shape to exist so ReplaceTranslation has something to accept.
type ImportedBookName struct {
	BookID int
	Name   string
	Abbrev string
}

type ImportedVerse struct {
	BookID  int
	Chapter int
	Verse   int
	Text    string
}
