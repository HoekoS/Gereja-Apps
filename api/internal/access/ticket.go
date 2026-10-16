package access

import (
	"crypto/hmac"
	"crypto/sha256"
	"encoding/base64"
	"net/http"
	"strings"
	"time"
)

// CookieName is the pair cookie. Its payload is the PIN's rotation
// timestamp, so rotating the PIN invalidates every ticket issued before it
// (FR-SEC-06) without keeping a server-side session list — the same design
// as the .NET implementation's PairTicket, with an HMAC in place of ASP.NET
// Data Protection: both are "tamper-evident, server-verifiable, bound to
// rotatedAt", just with a secret this process is handed directly rather
// than a key ring it manages itself.
const CookieName = "pair"

const cookieMaxAge = 7 * 24 * time.Hour

// Issue sets the pair cookie for rotatedAt.
func Issue(w http.ResponseWriter, rotatedAt time.Time, secret []byte) {
	http.SetCookie(w, &http.Cookie{
		Name:     CookieName,
		Value:    sign(rotatedAt, secret),
		Path:     "/",
		HttpOnly: true,
		SameSite: http.SameSiteLaxMode,
		// Not Secure: this is plain HTTP on the LAN by design — see the
		// accepted risk this ports from the .NET PairTicket.
		Secure: false,
		MaxAge: int(cookieMaxAge.Seconds()),
	})
}

// Valid reports whether r carries a pair cookie that is both unforged and
// bound to the current rotatedAt.
func Valid(r *http.Request, rotatedAt time.Time, secret []byte) bool {
	cookie, err := r.Cookie(CookieName)
	if err != nil || cookie.Value == "" {
		return false
	}
	return hmac.Equal([]byte(cookie.Value), []byte(sign(rotatedAt, secret)))
}

func sign(rotatedAt time.Time, secret []byte) string {
	payload := rotatedAt.UTC().Format(time.RFC3339Nano)
	mac := hmac.New(sha256.New, secret)
	mac.Write([]byte(payload))
	return strings.Join([]string{
		base64.RawURLEncoding.EncodeToString([]byte(payload)),
		base64.RawURLEncoding.EncodeToString(mac.Sum(nil)),
	}, ".")
}
