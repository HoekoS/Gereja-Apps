package access

import (
	"testing"
	"time"
)

func mustParse(t *testing.T, value string) time.Time {
	t.Helper()
	parsed, err := time.Parse(time.RFC3339, value)
	if err != nil {
		t.Fatalf("mustParse(%q): %v", value, err)
	}
	return parsed
}

func TestShouldRotate(t *testing.T) {
	// 2024-01-06 is a Saturday; the suite anchors every case to it so the
	// boundary (turns over sharply at Saturday midnight, not "that week")
	// is visible in the test names.
	cases := []struct {
		name          string
		lastRotatedAt string
		now           string
		want          bool
	}{
		{"one second after its own rotation, same Saturday", "2024-01-06T00:00:00Z", "2024-01-06T00:00:01Z", false},
		{"midweek, same rotation week", "2024-01-06T00:00:00Z", "2024-01-10T12:00:00Z", false},
		{"the night before the next Saturday", "2024-01-06T00:00:00Z", "2024-01-12T23:59:59Z", false},
		{"one second after the next Saturday midnight", "2024-01-06T00:00:00Z", "2024-01-13T00:00:01Z", true},
		{"exactly at its own rotation instant", "2024-01-13T00:00:00Z", "2024-01-13T00:00:00Z", false},
		{"a server that was off for a month, come back up", "2024-01-06T00:00:00Z", "2024-02-10T09:00:00Z", true},
	}

	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := ShouldRotate(mustParse(t, c.lastRotatedAt), mustParse(t, c.now))
			if got != c.want {
				t.Errorf("ShouldRotate(%s, %s) = %v, want %v", c.lastRotatedAt, c.now, got, c.want)
			}
		})
	}
}

func TestGeneratePinIsSixDigits(t *testing.T) {
	for range 50 {
		pin := GeneratePin()
		if len(pin) != 6 {
			t.Fatalf("GeneratePin() = %q, want 6 digits", pin)
		}
		for _, c := range pin {
			if c < '0' || c > '9' {
				t.Fatalf("GeneratePin() = %q, want only digits", pin)
			}
		}
	}
}
