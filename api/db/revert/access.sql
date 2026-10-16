-- Revert gereja-apps:access from pg
BEGIN;

DROP TABLE access_pin;

COMMIT;
