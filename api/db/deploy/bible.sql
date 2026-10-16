-- Deploy gereja-apps:bible to pg
BEGIN;

-- id is the slug a translation was imported under (e.g. "tb"), not a UUID:
-- it is already a human-meaningful, non-sensitive identifier, so there is
-- no public_id indirection to add, unlike an account or role.
CREATE TABLE bible_translations (
    id text PRIMARY KEY CHECK (length(id) BETWEEN 1 AND 64),
    abbrev text NOT NULL,
    name text NOT NULL,
    language text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

-- A book's name in one translation's own language. Not every id in
-- bible_verses need appear here — BookName() in the Go domain layer falls
-- back to the canonical Indonesian table when a translation's import never
-- carried its own names.
CREATE TABLE bible_book_names (
    translation_id text NOT NULL REFERENCES bible_translations(id) ON DELETE CASCADE,
    book_id integer NOT NULL CHECK (book_id > 0),
    name text NOT NULL,
    abbrev text,
    PRIMARY KEY (translation_id, book_id)
);

CREATE TABLE bible_verses (
    translation_id text NOT NULL REFERENCES bible_translations(id) ON DELETE CASCADE,
    book_id integer NOT NULL CHECK (book_id > 0),
    chapter integer NOT NULL CHECK (chapter > 0),
    verse integer NOT NULL CHECK (verse > 0),
    text text NOT NULL,
    -- Generated, not populated by the application: every write — import,
    -- and later re-import — gets a correct search column for free.
    search tsvector GENERATED ALWAYS AS (to_tsvector('simple', text)) STORED,
    PRIMARY KEY (translation_id, book_id, chapter, verse)
);
CREATE INDEX bible_verses_search ON bible_verses USING gin (search);

COMMIT;
