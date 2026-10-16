// Package platform holds what every context shares: the error envelope and
// JSON helpers. It imports no feature context (api/internal/bible, and
// whatever contexts follow it, import platform — never the other way).
package platform

import (
	"encoding/json"
	"log/slog"
	"net/http"
)

// Error is a user-facing failure. Code is the SNAKE_CASE value the API
// contract (docs/requirements/API-CONTRACT.md) promises on every non-2xx
// response; Message is a sentence safe to show. Internal detail is logged,
// never returned.
type Error struct {
	Status  int
	Code    string
	Message string
}

func (e *Error) Error() string { return e.Message }

func NewError(status int, code, message string) *Error {
	return &Error{Status: status, Code: code, Message: message}
}

func NotFound(code, message string) *Error { return NewError(http.StatusNotFound, code, message) }

// Unavailable reports a dependency outage (storage, cache) in the documented
// envelope without leaking the underlying error to the client.
func Unavailable(message string) *Error {
	return NewError(http.StatusServiceUnavailable, "STORAGE_UNAVAILABLE", message)
}

// Handler is an http.Handler that returns its failure instead of writing it.
type Handler func(w http.ResponseWriter, r *http.Request) error

func (h Handler) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	if err := h(w, r); err != nil {
		WriteError(w, err)
	}
}

// WriteError classifies err into the documented envelope. A caller that
// wants a specific status and code returns an *Error; anything else is
// logged and reported as 503, same principle as matsu-go: never let
// internal detail (a driver error, a query) reach the response body.
func WriteError(w http.ResponseWriter, err error) {
	apiErr, ok := err.(*Error)
	if !ok {
		slog.Error("Request failed", "error", err)
		apiErr = Unavailable("Storage unavailable")
	}
	_ = WriteJSON(w, apiErr.Status, map[string]any{
		"error": map[string]string{"code": apiErr.Code, "message": apiErr.Message},
	})
}

func WriteJSON(w http.ResponseWriter, status int, body any) error {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	return json.NewEncoder(w).Encode(body)
}
