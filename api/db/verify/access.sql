-- Verify gereja-apps:access on pg
BEGIN;

SELECT id, pin, rotated_at FROM access_pin WHERE false;

ROLLBACK;
