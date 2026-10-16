package bible

import (
	"net/http"
	"strconv"
	"strings"

	"gereja-apps/api/internal/platform"
)

const searchCap = 100

// Handler holds this context's dependencies.
type Handler struct{ store *Store }

func NewHandler(store *Store) *Handler { return &Handler{store: store} }

// Routes registers every bible route, each wrapped in requirePair
// (typically access.Handler.RequirePair) so the "paired" gate the API
// contract documents is visible right here at the routes that need it,
// rather than as blanket middleware with the exemptions listed somewhere
// else. A new context adds its own Routes function and is mounted in
// cmd/api/main.go, same shape as Identity's in the Matsu template this
// follows — except downstream of Access instead of Identity.
func (h *Handler) Routes(mux *http.ServeMux, requirePair func(platform.Handler) platform.Handler) {
	mux.Handle("GET /api/translations", requirePair(h.listTranslations))
	mux.Handle("GET /api/bible/reference", requirePair(h.parseReference))
	mux.Handle("GET /api/bible/passage", requirePair(h.passage))
	mux.Handle("GET /api/bible/search", requirePair(h.search))
}

func (h *Handler) listTranslations(w http.ResponseWriter, r *http.Request) error {
	translations, err := h.store.ListTranslations(r.Context())
	if err != nil {
		return err
	}

	body := make([]map[string]string, len(translations))
	for i, t := range translations {
		body[i] = map[string]string{"id": t.ID, "abbrev": t.Abbrev, "name": t.Name, "language": t.Language}
	}
	return platform.WriteJSON(w, http.StatusOK, body)
}

// parseReference parses free-form operator input. 404 rather than 400: the
// operator types into this field one character at a time, and most
// prefixes are not references yet — a 400 storm in the log would hide the
// failures that matter (SYS-BIB-07).
func (h *Handler) parseReference(w http.ResponseWriter, r *http.Request) error {
	q := r.URL.Query().Get("q")

	reference, ok := ParseReference(q)
	if !ok {
		return platform.NotFound("UNPARSEABLE_REFERENCE", "'"+q+"' is not a book, chapter and verse.")
	}

	var verseEnd any
	if reference.VerseEnd != 0 {
		verseEnd = reference.VerseEnd
	}
	return platform.WriteJSON(w, http.StatusOK, map[string]any{
		"bookId":     reference.BookID,
		"chapter":    reference.Chapter,
		"verseStart": reference.VerseStart,
		"verseEnd":   verseEnd,
	})
}

func (h *Handler) passage(w http.ResponseWriter, r *http.Request) error {
	query := r.URL.Query()

	translationID := query.Get("translationId")
	bookID, err := strconv.Atoi(query.Get("bookId"))
	if err != nil {
		return platform.NewError(http.StatusBadRequest, "INVALID_QUERY", "bookId must be an integer.")
	}
	chapter, err := strconv.Atoi(query.Get("chapter"))
	if err != nil {
		return platform.NewError(http.StatusBadRequest, "INVALID_QUERY", "chapter must be an integer.")
	}
	verseStart := 1
	if text := query.Get("verseStart"); text != "" {
		if verseStart, err = strconv.Atoi(text); err != nil {
			return platform.NewError(http.StatusBadRequest, "INVALID_QUERY", "verseStart must be an integer.")
		}
	}
	verseEnd := 0
	if text := query.Get("verseEnd"); text != "" {
		if verseEnd, err = strconv.Atoi(text); err != nil {
			return platform.NewError(http.StatusBadRequest, "INVALID_QUERY", "verseEnd must be an integer.")
		}
	}

	reference := Reference{BookID: bookID, Chapter: chapter, VerseStart: verseStart, VerseEnd: verseEnd}

	passage, err := h.store.Passage(r.Context(), translationID, reference)
	if err != nil {
		return err
	}
	if passage == nil {
		return platform.NotFound("PASSAGE_NOT_FOUND", "That passage is not in this translation.")
	}

	verses := make([]map[string]any, len(passage.Verses))
	for i, v := range passage.Verses {
		verses[i] = map[string]any{"verse": v.Verse, "text": v.Text}
	}
	return platform.WriteJSON(w, http.StatusOK, map[string]any{
		"translationId": passage.TranslationID,
		"bookId":        passage.BookID,
		"bookName":      passage.BookName,
		"chapter":       passage.Chapter,
		"verses":        verses,
	})
}

func (h *Handler) search(w http.ResponseWriter, r *http.Request) error {
	query := r.URL.Query()

	q := strings.TrimSpace(query.Get("q"))
	var translationID *string
	if id := query.Get("translationId"); id != "" {
		translationID = &id
	}

	var hits []VerseHit
	if q != "" {
		var err error
		if hits, err = h.store.Search(r.Context(), translationID, q, searchCap); err != nil {
			return err
		}
	}

	results := make([]map[string]any, len(hits))
	for i, hit := range hits {
		results[i] = map[string]any{
			"translationId": hit.TranslationID,
			"bookId":        hit.BookID,
			"bookName":      hit.BookName,
			"chapter":       hit.Chapter,
			"verse":         hit.Verse,
			"text":          hit.Text,
		}
	}
	return platform.WriteJSON(w, http.StatusOK, map[string]any{"results": results})
}
