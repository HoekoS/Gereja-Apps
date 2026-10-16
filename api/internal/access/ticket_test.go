package access

import (
	"net/http"
	"net/http/httptest"
	"testing"
	"time"
)

func TestTicketRoundTrip(t *testing.T) {
	secret := []byte("test-secret")
	rotatedAt := time.Date(2026, 1, 10, 0, 0, 0, 0, time.UTC)

	recorder := httptest.NewRecorder()
	Issue(recorder, rotatedAt, secret)

	request := httptest.NewRequest(http.MethodGet, "/", nil)
	for _, cookie := range recorder.Result().Cookies() {
		request.AddCookie(cookie)
	}

	if !Valid(request, rotatedAt, secret) {
		t.Fatal("Valid() = false for a ticket just issued for the same rotatedAt")
	}
}

func TestTicketInvalidAfterRotation(t *testing.T) {
	secret := []byte("test-secret")
	issuedAt := time.Date(2026, 1, 10, 0, 0, 0, 0, time.UTC)
	rotatedTo := issuedAt.Add(7 * 24 * time.Hour)

	recorder := httptest.NewRecorder()
	Issue(recorder, issuedAt, secret)

	request := httptest.NewRequest(http.MethodGet, "/", nil)
	for _, cookie := range recorder.Result().Cookies() {
		request.AddCookie(cookie)
	}

	if Valid(request, rotatedTo, secret) {
		t.Fatal("Valid() = true against a rotatedAt the ticket was not issued for; rotating the PIN must invalidate it")
	}
}

func TestTicketRejectsWrongSecret(t *testing.T) {
	rotatedAt := time.Date(2026, 1, 10, 0, 0, 0, 0, time.UTC)

	recorder := httptest.NewRecorder()
	Issue(recorder, rotatedAt, []byte("secret-a"))

	request := httptest.NewRequest(http.MethodGet, "/", nil)
	for _, cookie := range recorder.Result().Cookies() {
		request.AddCookie(cookie)
	}

	if Valid(request, rotatedAt, []byte("secret-b")) {
		t.Fatal("Valid() = true with the wrong secret")
	}
}

func TestTicketRejectsNoCookie(t *testing.T) {
	request := httptest.NewRequest(http.MethodGet, "/", nil)
	if Valid(request, time.Now(), []byte("secret")) {
		t.Fatal("Valid() = true with no cookie at all")
	}
}
