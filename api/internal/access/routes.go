package access

import (
	"crypto/subtle"
	"encoding/json"
	"net"
	"net/http"
	"time"

	"gereja-apps/api/internal/platform"
)

// Settings mirrors AccessOptions from the .NET implementation.
type Settings struct {
	// TestPin and RequirePairingFromLoopback are test-only, same as their
	// .NET counterparts: refuse both outside a test environment before
	// wiring this to production settings.
	TestPin                    string
	RequirePairingFromLoopback bool

	// Pair attempts allowed per remote address per window (NFR-SEC-05).
	PairAttemptsPerWindow int
	PairWindow            time.Duration

	// Pair attempts allowed across every address together (NFR-SEC-05).
	// The per-address limit alone bounds nothing an attacker cares about,
	// because one laptop can bind a dozen addresses.
	PairAttemptsPerGlobalWindow int
	PairGlobalWindow            time.Duration
}

func DefaultSettings() Settings {
	return Settings{
		PairAttemptsPerWindow:       5,
		PairWindow:                  time.Minute,
		PairAttemptsPerGlobalWindow: 60,
		PairGlobalWindow:            time.Hour,
	}
}

// Handler holds this context's dependencies, and is the one way every
// other context reaches it: through RequirePair. There is no per-user
// Actor the way Identity's is in the Matsu template — pairing is binary,
// paired or not, for the one shared booth machine.
type Handler struct {
	store    *Store
	secret   []byte
	settings Settings
	perIP    *limiter
	global   *limiter
}

const globalPairKey = "pair"

func NewHandler(store *Store, secret []byte, settings Settings) *Handler {
	return &Handler{
		store:    store,
		secret:   secret,
		settings: settings,
		perIP:    newLimiter(settings.PairAttemptsPerWindow, settings.PairWindow),
		global:   newLimiter(settings.PairAttemptsPerGlobalWindow, settings.PairGlobalWindow),
	}
}

// Routes registers every access route. Unlike every other context's
// routes, these are never wrapped in RequirePair themselves — pair is how
// pairing happens, and pin is deliberately reachable before pairing by
// loopback-only design (FR-SEC-09).
func (h *Handler) Routes(mux *http.ServeMux) {
	mux.Handle("POST /api/pair", platform.Handler(h.pair))
	mux.Handle("GET /api/pin", platform.Handler(h.pin))
}

// RequirePair wraps a handler so it refuses any caller without a valid
// pairing cookie (FR-SEC-01). Applied by hand at each route that needs it,
// visible right there, rather than as blanket middleware with the
// exemptions listed somewhere else — same reasoning as the .NET PairGate.
func (h *Handler) RequirePair(next platform.Handler) platform.Handler {
	return func(w http.ResponseWriter, r *http.Request) error {
		paired, err := h.isPaired(r)
		if err != nil {
			return err
		}
		if !paired {
			return platform.NewError(http.StatusUnauthorized, "NOT_PAIRED", "Enter the PIN shown on the booth screen.")
		}
		return next(w, r)
	}
}

func (h *Handler) isPaired(r *http.Request) (bool, error) {
	// FR-SEC-08: the booth's own browser never types the PIN. Settings
	// switches this off in tests so the gate can be observed to reject.
	if !h.settings.RequirePairingFromLoopback && IsLoopback(r) {
		return true, nil
	}
	_, rotatedAt, err := h.store.Current(r.Context(), h.settings.TestPin)
	if err != nil {
		return false, err
	}
	return Valid(r, rotatedAt, h.secret), nil
}

func (h *Handler) pair(w http.ResponseWriter, r *http.Request) error {
	ip := remoteIP(r)
	if ok, retryAfter := h.perIP.allow(ip, time.Now()); !ok {
		return platform.Throttled(retryAfter)
	}
	// The backstop that makes the total finite: scoped to this route by
	// hand, rather than a limiter that sees every request in the app and
	// throttles routes that were never meant to be.
	if ok, retryAfter := h.global.allow(globalPairKey, time.Now()); !ok {
		return platform.Throttled(retryAfter)
	}

	var body struct {
		Pin string `json:"pin"`
	}
	decoder := json.NewDecoder(r.Body)
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&body); err != nil {
		return platform.NewError(http.StatusBadRequest, "INVALID_BODY", "Request body must be JSON.")
	}

	pin, rotatedAt, err := h.store.Current(r.Context(), h.settings.TestPin)
	if err != nil {
		return err
	}

	if body.Pin == "" || !constantTimeEqual(body.Pin, pin) {
		return platform.NewError(http.StatusUnauthorized, "BAD_PIN", "That PIN is not the one on the booth screen.")
	}

	Issue(w, rotatedAt, h.secret)
	w.WriteHeader(http.StatusNoContent)
	return nil
}

func (h *Handler) pin(w http.ResponseWriter, r *http.Request) error {
	// FR-SEC-09. The PIN is readable only by someone already at the booth
	// machine; from anywhere else this route does not exist as far as the
	// caller is concerned.
	if !IsLoopback(r) {
		return platform.NewError(http.StatusForbidden, "LOOPBACK_ONLY", "The PIN can only be read on the booth machine.")
	}

	pin, rotatedAt, err := h.store.Current(r.Context(), h.settings.TestPin)
	if err != nil {
		return err
	}

	return platform.WriteJSON(w, http.StatusOK, map[string]any{"pin": pin, "rotatedAt": rotatedAt})
}

// IsLoopback reports whether r arrived from 127.0.0.1 or ::1.
func IsLoopback(r *http.Request) bool {
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		host = r.RemoteAddr
	}
	ip := net.ParseIP(host)
	return ip != nil && ip.IsLoopback()
}

func remoteIP(r *http.Request) string {
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		return r.RemoteAddr
	}
	return host
}

// constantTimeEqual is constant-time on purpose: the PIN is six digits and
// the attacker is on the same LAN, so there is no reason to hand them a
// timing signal as well.
func constantTimeEqual(a, b string) bool {
	return subtle.ConstantTimeCompare([]byte(a), []byte(b)) == 1
}
