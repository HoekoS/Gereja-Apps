-- Deploy gereja-apps:access to pg
BEGIN;

-- Singleton row: the booth has exactly one shared PIN, never a table of
-- them. `id boolean ... check (id)` forces the only legal value to be
-- true, so a second INSERT collides on the primary key instead of quietly
-- creating a second PIN.
CREATE TABLE access_pin (
    id boolean PRIMARY KEY DEFAULT true CHECK (id),
    pin text NOT NULL,
    rotated_at timestamptz NOT NULL
);

COMMIT;
