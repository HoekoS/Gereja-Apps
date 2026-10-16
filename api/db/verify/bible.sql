-- Verify gereja-apps:bible on pg
BEGIN;

SELECT id, abbrev, name, language, created_at FROM bible_translations WHERE false;
SELECT translation_id, book_id, name, abbrev FROM bible_book_names WHERE false;
SELECT translation_id, book_id, chapter, verse, text, search FROM bible_verses WHERE false;

ROLLBACK;
