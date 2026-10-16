package access

import (
	"context"
	"log/slog"
	"time"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
)

// Store is this context's only way to reach access_pin — no other package
// queries it directly.
type Store struct{ pool *pgxpool.Pool }

func NewStore(pool *pgxpool.Pool) *Store { return &Store{pool: pool} }

// Current reads the shared PIN, rotating it when the stored timestamp is
// older than the most recent Saturday midnight (FR-SEC-03). testPin, when
// non-empty, replaces a random PIN on rotation — test-only, same as
// AccessOptions.TestPin in the .NET implementation. The row is locked for
// the transaction's duration, so two requests racing to rotate at once
// serialize instead of each generating their own PIN.
func (s *Store) Current(ctx context.Context, testPin string) (pin string, rotatedAt time.Time, err error) {
	err = pgx.BeginFunc(ctx, s.pool, func(tx pgx.Tx) error {
		if _, err := tx.Exec(ctx,
			`INSERT INTO access_pin (id, pin, rotated_at) VALUES (true, '', 'epoch') ON CONFLICT (id) DO NOTHING`,
		); err != nil {
			return err
		}

		row := tx.QueryRow(ctx, `SELECT pin, rotated_at FROM access_pin WHERE id FOR UPDATE`)
		if err := row.Scan(&pin, &rotatedAt); err != nil {
			return err
		}

		now := time.Now()
		if pin == "" || ShouldRotate(rotatedAt, now) {
			pin, rotatedAt = newPin(testPin), now
			if _, err := tx.Exec(ctx,
				`UPDATE access_pin SET pin = $1, rotated_at = $2 WHERE id`, pin, rotatedAt,
			); err != nil {
				return err
			}
			// FR-SEC-11. The console is the booth's own screen and this is
			// a LAN device PIN that /api/pin already serves to anyone
			// standing at that machine; without it the volunteer has no
			// way to learn the PIN they are supposed to read out.
			slog.Info("Pairing PIN rotated", "pin", pin, "rotatedAt", rotatedAt)
		}
		return nil
	})
	return pin, rotatedAt, err
}

func newPin(testPin string) string {
	if testPin != "" {
		return testPin
	}
	return GeneratePin()
}
