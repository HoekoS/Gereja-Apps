# API Contract

**System:** Church Service Projection Application
**Version:** 0.1 (draft)
**Date:** 2026-08-20
**Status:** Draft — derived from [SRS.md](SRS.md) IF-SW-01 to IF-SW-03

This document fixes the HTTP and live-channel surface so the API test suite has something concrete to assert against. It is derived from the SRS, not independent of it: where the two disagree, the SRS governs.

## Conventions

- Base path for all JSON endpoints: `/api`.
- All request and response bodies are JSON unless stated otherwise.
- All timestamps are ISO 8601 with offset.
- Identifiers are strings.

### Errors

Every non-2xx response carries:

```json
{ "error": { "code": "SNAKE_CASE_CODE", "message": "Human-readable, no stack trace." } }
```

| Status | Meaning |
|---|---|
| 400 | Malformed request or invalid parameters |
| 401 | Not paired (`FR-SEC-01`) |
| 403 | Paired but not permitted, including LAN access to a loopback-only route (`FR-SEC-09`) |
| 404 | No such resource |
| 409 | Conflicts with current state |
| 413 | Upload exceeds the configured limit |
| 422 | Import file rejected (`FR-IMP-07`) |
| 429 | Rate limited (`NFR-SEC-05`) |

### Authentication

`POST /api/pair` exchanges the PIN for a cookie named `pair`, `HttpOnly`, `SameSite=Lax`, protected by ASP.NET Core Data Protection and bound to `pin_rotated_at` (`FR-SEC-05`, `FR-SEC-06`). Rotating the PIN invalidates every ticket issued before the rotation.

Every route except `GET /healthz`, `POST /api/pair`, and the `output`-role connection to `/hub/live` requires that cookie. Requests from the loopback address are exempt (`FR-SEC-08`).

---

## Endpoints

### Health

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/healthz` | none | Liveness. Returns `{ "ok": true, "version": "..." }`. |

### Access

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/api/pair` | none | Body `{ "pin": "123456" }`. On success 204 and sets the `pair` cookie. On failure 401 `BAD_PIN`. Rate limited. |
| GET | `/api/pin` | loopback only | Returns `{ "pin": "123456", "rotatedAt": "..." }`. From any non-loopback address, 403 `LOOPBACK_ONLY`. |

### Bible

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/translations` | paired | Array of `{ id, abbrev, name, language }`. |
| GET | `/api/bible/reference?q=` | paired | Parses a free-form reference (`FR-LIB-08`). Returns `{ bookId, chapter, verseStart, verseEnd }`, or 404 `UNPARSEABLE_REFERENCE`. |
| GET | `/api/bible/passage` | paired | Params `translationId`, `bookId`, `chapter`, `verseStart`, `verseEnd`. Returns `{ translationId, bookId, bookName, chapter, verses: [{ verse, text }] }`. `bookName` is in the translation's language (`FR-LIB-04`). |
| GET | `/api/bible/search?q=&translationId=` | paired | `translationId` optional; omitted searches all. Returns `{ results: [{ translationId, bookId, bookName, chapter, verse, text }] }`, capped at 100. |

Switching translation on the same passage (`FR-LIB-06`) is `GET /api/bible/passage` with the same `bookId`/`chapter`/verse range and a different `translationId`. No separate endpoint.

### Songs

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/songs?q=` | paired | Title and lyric search (`FR-LIB-13`). Empty `q` lists all. Returns `{ results: [{ id, title, author, language }] }`. |
| GET | `/api/songs/:id` | paired | Returns `{ id, title, author, ccli, language, pages: [{ position, sectionLabel, text }] }`. |

### Media

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/media` | paired | `{ results: [{ id, kind, filename, durationMs, width, height, available }] }`. `available` is false when the file is missing or unreadable (`FR-LIB-23`). |
| GET | `/api/media/:id` | paired | One media item in the same shape. 404 `MEDIA_NOT_FOUND`. |
| GET | `/api/media/:id/stream` | paired | The binary. Supports `Range` (`IF-SW-03`). 404 `MEDIA_FILE_MISSING` when the row exists but the file does not. |
| POST | `/api/media` | paired | `multipart/form-data`, field `file`. Filename sanitised; paths resolving outside the media directory are rejected 400 `BAD_FILENAME` (`NFR-SEC-04`). |

### Import

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/api/import` | paired | `multipart/form-data`, field `file`. Format detected from content and extension. Success 200 `{ kind, imported, updated }`. Malformed input 422 `IMPORT_REJECTED` with `error.message` naming the offending record, and nothing written (`FR-IMP-05` to `FR-IMP-07`). |

### Services

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/services` | paired | Array of `{ id, name, serviceDate, itemCount }`. |
| POST | `/api/services` | paired | Body `{ name, serviceDate }`. Returns the created service. |
| GET | `/api/services/:id` | paired | `{ id, name, serviceDate, items: [...] }`, items in `position` order. |
| PATCH | `/api/services/:id` | paired | Body may contain `name`, `serviceDate`. |
| DELETE | `/api/services/:id` | paired | 204. |
| POST | `/api/services/:id/items` | paired | Body `{ kind, label, ref }`. Appends. Returns the created item. |
| PATCH | `/api/services/:id/items/:itemId` | paired | Body may contain `label`, `ref`. |
| DELETE | `/api/services/:id/items/:itemId` | paired | 204. Does not delete referenced library content (`FR-SVC-07`). |
| POST | `/api/services/:id/items/reorder` | paired | Body `{ itemIds: [...] }` — the complete new order. 400 `INCOMPLETE_ORDER` if it is not a permutation of the service's items. |

An item's `ref` by kind:

| kind | ref |
|---|---|
| `bible` | `{ translationId, bookId, chapter, verseStart, verseEnd }` |
| `song` | `{ songId }` |
| `slide` | `{ text }` |
| `media` | `{ mediaId }` |
| `countdown` | `{ targetTime: "10:30" }` |

### Live

The hub is the operating interface. These REST routes exist so that live behaviour is testable without a hub client, and so the control view can recover state over plain HTTP.

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/live` | paired | Current live state, identical in shape to the hub broadcast. |
| POST | `/api/live/command` | paired | Body is one command, identical in shape to a hub client message. Returns the resulting state. |

Command bodies (`FR-LIV-10`, plus skip per `FR-LIV-16`):

```json
{ "type": "preview", "itemId": "...", "pageIndex": 0 }
{ "type": "go" }
{ "type": "advance" }
{ "type": "back" }
{ "type": "blackout", "on": true }
{ "type": "clear" }
{ "type": "skip", "itemId": "..." }
{ "type": "unskip", "itemId": "..." }
```

State shape, returned by both routes and broadcast on the socket (`FR-LIV-11`):

```json
{
  "serviceId": "svc_1",
  "live":    { "itemId": "itm_3", "pageIndex": 1, "content": { } },
  "preview": { "itemId": "itm_4", "pageIndex": 0, "content": { } },
  "blackout": false,
  "skipped": ["itm_2"],
  "outputsConnected": 1
}
```

`live` and `preview` are `null` when nothing is set. `content` is the resolved, renderable payload for that page — the client never re-queries the library to render.

`go` clears `preview` once it has promoted it, so a second `go` with nothing staged is refused rather than repeating the last action. The example above shows a state where the operator has already staged the next item after going live on the current one.

Refusals are 409 with a code, and leave state unchanged:

| Code | Cause |
|---|---|
| `NO_PREVIEW` | `go` with nothing staged |
| `NO_LIVE_ITEM` | `advance` or `back` with nothing live |
| `MEDIA_UNAVAILABLE` | `go` on an item whose media file is missing (`FR-LIV-17`) |
| `UNKNOWN_ITEM` | `preview` or `skip` naming an item not in the service |
| `PAGE_OUT_OF_RANGE` | `preview` naming a page the item does not have — a stale control view after the song was re-imported shorter |

A 409 from `POST /api/live/command` carries the unchanged state alongside the error, so a control screen that issued a stale command resyncs from the refusal itself rather than needing a second request:

```json
{ "error": { "code": "UNKNOWN_ITEM", "message": "..." },
  "state": { } }
```

This shape is specific to the live command endpoint. Every other endpoint returns the bare error envelope.

`advance` on the last page and `back` on the first page are **not** errors. They return 200 with unchanged state (`FR-LIV-06`).

### Live channel

`/hub/live?role=control|output|remote` — a SignalR hub, negotiated then upgraded to a WebSocket.

- `control` and `remote` require the `pair` cookie (`FR-SEC-07`). `output` does not (`FR-SEC-10`).
- On connect the server invokes `StateChanged` with one full state object before anything else (`FR-LIV-12`).
- Clients invoke `SendCommand(command)` with a command object as above. The server broadcasts `StateChanged(state)` with a full state object as above. No deltas (`FR-LIV-11`).
- One role per connection, fixed at connect time. A client that needs another role opens another connection.

---

## Not in this contract

- Any endpoint for the deferred `/remote` view beyond the hub role.
- Deletion of songs, media, or translations. Traces to `FR-ADM-04`, which is marked **(P)** and unconfirmed.
- Pagination. Capacities in SRS §5.6 are small enough that search caps suffice.
