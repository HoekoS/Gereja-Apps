-- Revert gereja-apps:bible from pg
BEGIN;

DROP TABLE bible_verses;
DROP TABLE bible_book_names;
DROP TABLE bible_translations;

COMMIT;
