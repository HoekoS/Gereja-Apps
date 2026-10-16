// Package access ports FR-SEC-01..FR-SEC-11: the booth's shared six-digit
// PIN, its weekly rotation, and the pairing cookie that stands in for a
// login the booth has no use for. Every other context reaches it only
// through Handler.RequirePair — the same shape Identity's Actor plays in
// the Matsu template this follows, except there is no per-user identity
// here, only "paired" or not.
package access

import (
	"crypto/rand"
	"fmt"
	"math/big"
	"time"
)

// GeneratePin returns a fresh six-digit PIN. Short enough to read off a
// card taped to the booth desk, which is why it rotates weekly and why
// pairing is rate limited.
func GeneratePin() string {
	n, err := rand.Int(rand.Reader, big.NewInt(1_000_000))
	if err != nil {
		panic("access: crypto/rand failed: " + err.Error())
	}
	return fmt.Sprintf("%06d", n.Int64())
}

// ShouldRotate reports whether a Saturday midnight has passed since the PIN
// was last set. Evaluated on demand — nothing schedules this — so a server
// that was off for a month rotates exactly once when it comes back up, and
// the number handed out at Saturday rehearsal is the number that works on
// Sunday morning.
func ShouldRotate(lastRotatedAt, now time.Time) bool {
	return mostRecentSaturdayMidnight(now).After(lastRotatedAt)
}

func mostRecentSaturdayMidnight(now time.Time) time.Time {
	daysSinceSaturday := (int(now.Weekday()) - int(time.Saturday) + 7) % 7
	year, month, day := now.Date()
	midnight := time.Date(year, month, day, 0, 0, 0, 0, now.Location())
	return midnight.AddDate(0, 0, -daysSinceSaturday)
}
