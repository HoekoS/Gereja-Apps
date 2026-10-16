package access

import (
	"sync"
	"time"
)

// limiter is a fixed-window rate limiter, in-memory like the .NET
// implementation's AddRateLimiter — the booth runs as one process, so
// there is no multi-instance case that would need shared state.
type limiter struct {
	mu     sync.Mutex
	limit  int
	window time.Duration
	byKey  map[string]*rateWindow
}

type rateWindow struct {
	count   int
	resetAt time.Time
}

func newLimiter(limit int, window time.Duration) *limiter {
	return &limiter{limit: limit, window: window, byKey: make(map[string]*rateWindow)}
}

// allow reports whether key may proceed now, and, when it may not, the
// whole seconds remaining until the window resets.
func (l *limiter) allow(key string, now time.Time) (ok bool, retryAfterSeconds int) {
	l.mu.Lock()
	defer l.mu.Unlock()

	w, exists := l.byKey[key]
	if !exists || !now.Before(w.resetAt) {
		w = &rateWindow{resetAt: now.Add(l.window)}
		l.byKey[key] = w
	}
	w.count++
	if w.count > l.limit {
		remaining := w.resetAt.Sub(now)
		return false, int(remaining.Seconds()) + 1
	}
	return true, 0
}
