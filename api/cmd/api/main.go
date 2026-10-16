package main

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"

	"gereja-apps/api/internal/access"
	"gereja-apps/api/internal/bible"
	"gereja-apps/api/internal/platform"
)

func main() {
	slog.SetDefault(slog.New(slog.NewJSONHandler(os.Stdout, nil)))
	if err := run(); err != nil {
		slog.Error("API stopped", "error", err)
		os.Exit(1)
	}
}

func run() error {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	databaseURL := os.Getenv("DATABASE_URL")
	if databaseURL == "" {
		return errors.New("DATABASE_URL is missing")
	}
	pool, err := pgxpool.New(ctx, databaseURL)
	if err != nil {
		return err
	}
	defer pool.Close()

	cookieSecret := os.Getenv("PAIR_COOKIE_SECRET")
	if cookieSecret == "" {
		return errors.New("PAIR_COOKIE_SECRET is missing")
	}

	accessSettings := access.DefaultSettings()
	// Test-only switches (AGENTS.md / AccessOptions parity): refuse both
	// outside a test environment before this ever leaves a trusted LAN.
	accessSettings.TestPin = os.Getenv("ACCESS_TEST_PIN")
	accessSettings.RequirePairingFromLoopback = os.Getenv("ACCESS_REQUIRE_PAIRING_FROM_LOOPBACK") == "true"

	accessHandler := access.NewHandler(access.NewStore(pool), []byte(cookieSecret), accessSettings)
	bibleHandler := bible.NewHandler(bible.NewStore(pool))

	mux := http.NewServeMux()
	accessHandler.Routes(mux)
	bibleHandler.Routes(mux, accessHandler.RequirePair)
	// A new context mounts here, same shape: its own NewHandler(...),
	// then .Routes(mux, accessHandler.RequirePair) for whichever of its
	// routes the contract requires paired.

	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, _ *http.Request) {
		_ = platform.WriteJSON(w, http.StatusOK, map[string]any{"ok": true, "version": version()})
	})

	address := os.Getenv("LISTEN_ADDR")
	if address == "" {
		address = "127.0.0.1:5000"
	}
	server := &http.Server{Addr: address, Handler: mux, ReadHeaderTimeout: 10 * time.Second}

	go func() {
		<-ctx.Done()
		shutdown, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		_ = server.Shutdown(shutdown)
	}()

	slog.Info("API starting", "address", address)
	if err := server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		return err
	}
	return nil
}

func version() string {
	if v := os.Getenv("APP_VERSION"); v != "" {
		return v
	}
	return "0.0.0"
}
