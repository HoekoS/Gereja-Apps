package access

import (
	"testing"
	"time"
)

func TestLimiterAllowsUpToTheLimitThenRejects(t *testing.T) {
	l := newLimiter(3, time.Minute)
	now := time.Date(2026, 1, 1, 0, 0, 0, 0, time.UTC)

	for i := 1; i <= 3; i++ {
		if ok, _ := l.allow("a", now); !ok {
			t.Fatalf("attempt %d: allow() = false, want true (within limit)", i)
		}
	}

	ok, retryAfter := l.allow("a", now)
	if ok {
		t.Fatal("4th attempt within the window: allow() = true, want false")
	}
	if retryAfter <= 0 {
		t.Errorf("retryAfter = %d, want > 0", retryAfter)
	}
}

func TestLimiterResetsAfterTheWindow(t *testing.T) {
	l := newLimiter(1, time.Minute)
	now := time.Date(2026, 1, 1, 0, 0, 0, 0, time.UTC)

	if ok, _ := l.allow("a", now); !ok {
		t.Fatal("first attempt: allow() = false, want true")
	}
	if ok, _ := l.allow("a", now.Add(30*time.Second)); ok {
		t.Fatal("within the same window: allow() = true, want false")
	}
	if ok, _ := l.allow("a", now.Add(61*time.Second)); !ok {
		t.Fatal("after the window elapsed: allow() = false, want true")
	}
}

func TestLimiterKeysAreIndependent(t *testing.T) {
	l := newLimiter(1, time.Minute)
	now := time.Date(2026, 1, 1, 0, 0, 0, 0, time.UTC)

	if ok, _ := l.allow("a", now); !ok {
		t.Fatal("key a: allow() = false, want true")
	}
	if ok, _ := l.allow("b", now); !ok {
		t.Fatal("key b should not be throttled by key a's usage: allow() = false, want true")
	}
}
