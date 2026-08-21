# Test Cases

**System:** Church Service Projection Application
**Version:** 0.1 (draft)
**Date:** 2026-08-20
**Status:** Draft — awaiting review
**Plan:** [TEST-PLAN.md](TEST-PLAN.md)

---

## 1. Conventions

Cases are written as **Given / When / Then**. Each states its requirement trace and whether it is critical.

| Marker | Meaning |
|---|---|
| **C** | Critical. Failure is visible to the congregation or loses content. No waiver — see TEST-PLAN §6. |
| **(P)** | Traces to a proposed requirement. Do not implement until the requirement is confirmed. |
| **M** | Manual. Cannot be automated. |

| Prefix | Level | Where it lives |
|---|---|---|
| `UNT-` | Unit | `tests/ChurchProjection.Domain.Tests/`, `tests/ChurchProjection.Application.Tests/` |
| `SYS-` | API / system | `tests/api/` (Bruno) |
| `INT-` | Integration / failure | Partly scripted, partly manual |
| `PERF-` | Performance | Booth machine |
| `MAN-` | Manual | Sanctuary |
| `UAT-` | Acceptance | Sanctuary |

---

## 2. Unit — the live aggregate

Type: `ChurchProjection.Domain.Live.LiveSession`. No I/O, no framework types (FR-LIV-18).

Interface under test:

```csharp
LiveSession.New() -> LiveSession
session.PreviewItem(ItemId, int pageIndex, IServiceOrder) -> LiveResult
session.Go() / Advance(IServiceOrder) / Back() / Clear()  -> LiveResult
session.SetBlackout(bool) / Skip(ItemId, IServiceOrder) / Unskip(ItemId) -> LiveResult
session.Snapshot() -> LiveSnapshot

// IServiceOrder: Contains(id), PageCount(id), MediaAvailable(id). Read-only.
// LiveResult: IsOk, or Refusal carrying a RefusalCode. A refusal leaves every
// property of the session untouched.
```

| ID | Traces | C | Case |
|---|---|---|---|
| UNT-LIV-01 | FR-LIV-02 | | **Given** a new machine, **then** live is null, preview is null, blackout is false, skipped is empty. |
| UNT-LIV-02 | FR-LIV-03, URS-LIVE-02 | **C** | **Given** live is item A, **when** preview item B, **then** preview is B and **live is still A, page unchanged**. |
| UNT-LIV-03 | FR-LIV-04 | | **Given** preview is B, **when** `go`, **then** live is B at the previewed page and preview is cleared. |
| UNT-LIV-04 | FR-LIV-04 | | **Given** preview is null, **when** `go`, **then** error `NO_PREVIEW` and state is identical. |
| UNT-LIV-05 | FR-LIV-05 | | **Given** live is A page 0 of 4, **when** `advance`, **then** live is A page 1. |
| UNT-LIV-06 | FR-LIV-05 | | **Given** live is A page 2, **when** `back`, **then** live is A page 1. |
| UNT-LIV-07 | FR-LIV-06, URS-LIVE-04 | **C** | **Given** live is A on its **last** page, **when** `advance`, **then** no error, page unchanged, item unchanged. It must not wrap to page 0 and must not move to the next item. |
| UNT-LIV-08 | FR-LIV-05 | | **Given** live is A page 0, **when** `back`, **then** no error and page stays 0. |
| UNT-LIV-09 | FR-LIV-05 | | **Given** live is null, **when** `advance`, **then** error `NO_LIVE_ITEM` and state identical. |
| UNT-LIV-10 | FR-LIV-07 | **C** | **Given** live is A page 2, **when** `blackout {on:true}`, **then** blackout is true and **live is still A page 2**. |
| UNT-LIV-11 | FR-LIV-07, URS-LIVE-09 | **C** | **Given** blackout is true and live is A page 2, **when** `blackout {on:false}`, **then** blackout is false and live is still A page 2. |
| UNT-LIV-12 | FR-LIV-08 | | **Given** live is A and preview is B, **when** `clear`, **then** live is null and preview is still B. |
| UNT-LIV-13 | FR-LIV-17, URS-AVL-04 | **C** | **Given** preview is a media item whose `MediaAvailable` is false, **when** `Go`, **then** refusal `MEDIA_UNAVAILABLE` and **live is unchanged**. The congregation never sees the broken item. |
| UNT-LIV-14 | API contract | | **Given** any state, **when** previewing an id where `Contains` is false, **then** refusal `UNKNOWN_ITEM` and state identical. |
| UNT-LIV-15 | FR-LIV-16, URS-LIVE-08 | | **When** `skip` item A, **then** A is in skipped and live and preview are unchanged. |
| UNT-LIV-16 | FR-LIV-16 | | **Given** A is skipped, **when** `unskip` A, **then** skipped is empty. |
| UNT-LIV-17 | FR-LIV-16, URS-LIVE-08 | **C** | **Given** A is skipped, **when** preview A then `go`, **then** A is live. Skipping must not make an item unreachable. |
| UNT-LIV-18 | FR-LIV-09, FR-LIV-18 | **C** | **When** a command is **refused**, **then** every property of the session equals a snapshot taken before the call. Guards run to completion before the first write. |
| UNT-LIV-19 | FR-LIV-15, URS-LIVE-07 | **C** | `IServiceOrder` exposes exactly `Contains`, `PageCount`, `MediaAvailable` and nothing that writes. Live reads the service; it can never edit it. Asserted by reflection so adding a mutating member fails the test. |
| UNT-LIV-20 | FR-LIV-01 | | **When** previewing a page index the item does not have, **then** refusal `PAGE_OUT_OF_RANGE`. No silent clamp. |

## 3. Unit — PIN rotation

Types: `ChurchProjection.Domain.Access.Pin`, `PinRotation`. All timestamps are `DateTimeKind.Unspecified`, meaning local wall clock, so the suite is time-zone independent.

```csharp
Pin.Generate() -> Pin
PinRotation.ShouldRotate(DateTime pinRotatedAt, DateTime now) -> bool
```

| ID | Traces | C | Case |
|---|---|---|---|
| UNT-PIN-01 | FR-SEC-02 | | `generatePin()` returns exactly six characters, all digits. |
| UNT-PIN-02 | FR-SEC-03 | | 1,000 calls to `generatePin()` yield at least 900 distinct values. Catches a constant or a seeded weak generator. |
| UNT-PIN-03 | FR-SEC-04, URS-SEC-03 | **C** | Rotated Friday 23:59, now Saturday 00:00 → **true**. |
| UNT-PIN-04 | FR-SEC-04 | | Rotated Saturday 00:00, now the same Saturday 12:00 → **false**. Rotating twice in one weekend would lock out a paired device mid-service. |
| UNT-PIN-05 | FR-SEC-04 | | Rotated Saturday, now the following Friday 23:59 → **false**. |
| UNT-PIN-06 | FR-SEC-04 | | Rotated Saturday, now the following Saturday 00:01 → **true**. |
| UNT-PIN-07 | FR-SEC-04 | | Rotated exactly Saturday 00:00:00, now exactly that instant → **false**. Boundary is inclusive of the rotation itself. |
| UNT-PIN-08 | FR-SEC-04 | | Rotated three weeks ago, now Wednesday → **true**, and a single rotation brings it current. No catch-up loop. |
| UNT-PIN-09 | FR-SEC-04 | **C** | Rotated Saturday 00:00:00 minus 1 ms, now Saturday 00:00:00 → **true**. The one-millisecond boundary. |

## 4. Unit — import parsers

Type: `ChurchProjection.Infrastructure.Import`, through `ImportService.Parse(Stream, fileName) -> ImportPayload`. Throws `ImportException` carrying `.Detail`.

`ImportPayload` returns a completed result or nothing at all — it exposes lists, never a lazy sequence. That signature is what makes UNT-IMP-12 an assertion rather than a hope.

| ID | Traces | C | Case |
|---|---|---|---|
| UNT-IMP-01 | FR-IMP-03 | | Plain text song: the first line becomes the title. |
| UNT-IMP-02 | FR-IMP-03 | | Plain text song: a blank line starts a new page. Three blank-separated blocks produce three pages in order. |
| UNT-IMP-03 | FR-IMP-03 | | Plain text song: a line `[Reff]` becomes the section label of the page that follows it. |
| UNT-IMP-04 | FR-IMP-03 | | Plain text song: a line `Reff:` becomes the section label of the page that follows it. |
| UNT-IMP-05 | FR-LIB-12, URS-SONG-04 | **C** | Section label is stored **verbatim**. `Reff` stays `Reff` — not mapped to `Chorus`, not title-cased, not validated against a list. |
| UNT-IMP-06 | FR-IMP-07 | | Plain text song: an empty file throws `ImportError`. |
| UNT-IMP-07 | FR-IMP-07 | | Plain text song: a file with a title and no further content throws `ImportError`. A song with zero pages is not importable. |
| UNT-IMP-08 | FR-IMP-01 | | Zefania XML: parses to `kind: 'bible'` with records carrying book, chapter, verse, and text. |
| UNT-IMP-09 | FR-IMP-07 | | Zefania XML: malformed markup throws, and `.detail` names the failing element or line. |
| UNT-IMP-10 | FR-IMP-04 | | OpenLyrics XML: parses title, author, and ordered pages. |
| UNT-IMP-11 | IF-SW-04 | | Every parser returns one `ImportPayload`. `Kind` is `Bible` or `Song`, and only the matching collection is populated. |
| UNT-IMP-12 | FR-IMP-05, URS-AVL-06 | **C** | On a file whose fault is at the **last** record, the parser throws and returns nothing. No partial array escapes. Proves parse-fully-then-write is possible. |
| UNT-IMP-13 | FR-IMP-03 | | Plain text song: `\r\n` line endings parse identically to `\n`. Lyrics arrive from Word on Windows. |

## 5. Unit — Bible reference parsing

Type: `ChurchProjection.Domain.Bible.BibleReference`. `BibleReference.TryParse(string?) -> BibleReference?`, a record of `(BookId, Chapter, VerseStart, VerseEnd)`.

| ID | Traces | C | Case |
|---|---|---|---|
| UNT-REF-01 | FR-LIB-08 | | `"Yohanes 3:16"` → John, ch 3, v 16–16. |
| UNT-REF-02 | FR-LIB-08 | | `"Yoh 3:16"` → same. Indonesian abbreviation. |
| UNT-REF-03 | FR-LIB-08 | | `"John 3:16"` → same. English name resolves to the same canonical book id. |
| UNT-REF-04 | FR-LIB-08 | | `"yohanes 3:16"` → same. Case insensitive. |
| UNT-REF-05 | FR-LIB-08, URS-BIB-06 | | `"Kejadian 1:1-5"` → Genesis, ch 1, v 1–5. |
| UNT-REF-06 | FR-LIB-08 | | `"Mazmur 23"` → Psalms, ch 23, whole chapter. |
| UNT-REF-07 | FR-LIB-08 | | `"1 Korintus 13:4-7"` → 1 Corinthians. Leading book number is not read as a chapter. |
| UNT-REF-08 | FR-LIB-08 | | `"1Kor 13:4"` → same. No space after the book number. |
| UNT-REF-09 | FR-LIB-08 | | `"asdf"` → null. No throw. |
| UNT-REF-10 | FR-LIB-08 | | `""` → null. |
| UNT-REF-11 | FR-LIB-03 | | A book id outside 1–66 is accepted by the data layer. Verified by constructing a reference for a deuterocanonical book id and confirming it is not rejected as out of range. |

## 6. API / system tests

Bruno collection at `tests/api/`. Run against a server with the seed database from TEST-PLAN §4.1.

### 6.1 Health and access

| ID | Traces | C | Case |
|---|---|---|---|
| SYS-HLT-01 | — | | `GET /healthz` → 200, `{ ok: true }`. Entry gate for the rest of the suite. |
| SYS-SEC-01 | FR-SEC-02 | | `POST /api/pair` with a wrong PIN → 401 `BAD_PIN`, and no `pair` cookie is set. |
| SYS-SEC-02 | FR-SEC-02, FR-SEC-06 | | `POST /api/pair` with the correct PIN → 204, and the `pair` cookie is set with `HttpOnly` and `SameSite=Lax`. |
| SYS-SEC-03 | FR-SEC-01, URS-SEC-01 | **C** | Any write endpoint without the cookie → 401. Verified against `POST /api/live/command`, `POST /api/import`, and `POST /api/services`. |
| SYS-SEC-04 | FR-SEC-09, URS-SEC-04 | **C** | `GET /api/pin` from a non-loopback address → 403 `LOOPBACK_ONLY`, and the response body contains no PIN. |
| SYS-SEC-05 | NFR-SEC-05 | | Repeated wrong PINs from one address → 429 before the attempt count could exhaust a six-digit space in a week. |
| SYS-SEC-06 | FR-SEC-05, URS-SEC-03 | | A cookie issued before a PIN rotation is rejected 401 after the rotation. Forced by advancing the stored `pin_rotated_at`. |

### 6.2 Bible

| ID | Traces | C | Case |
|---|---|---|---|
| SYS-BIB-01 | FR-LIB-01, URS-BIB-01 | | `GET /api/translations` lists the seeded translations with abbrev, name, and language. |
| SYS-BIB-02 | FR-LIB-05, URS-BIB-03 | | `GET /api/bible/passage` for a known range returns exactly those verses, in ascending verse order, no extras. |
| SYS-BIB-03 | FR-LIB-08 | | `GET /api/bible/reference?q=Yohanes 3:16` returns the parsed reference. |
| SYS-BIB-04 | FR-LIB-06, URS-BIB-05 | **C** | The same book, chapter, and verse range requested under two translation ids returns the **same reference** and **different text**. Switching translation must not shift the passage. |
| SYS-BIB-05 | FR-LIB-07, URS-BIB-04 | | `GET /api/bible/search?q=<known word>` returns the seeded verse containing it. |
| SYS-BIB-06 | FR-LIB-04, URS-BIB-08 | | `bookName` in a passage response is in the translation's own language — `Kejadian` for an Indonesian translation, `Genesis` for an English one. |
| SYS-BIB-07 | FR-LIB-08 | | `GET /api/bible/reference?q=asdf` → 404 `UNPARSEABLE_REFERENCE`. Not a 500. |

### 6.3 Songs

| ID | Traces | C | Case |
|---|---|---|---|
| SYS-SNG-01 | FR-LIB-13, URS-SONG-05 | | `GET /api/songs?q=<title word>` returns the matching song. |
| SYS-SNG-02 | FR-LIB-13, URS-SONG-05 | | `GET /api/songs?q=<word from lyrics only>` returns the song. Search covers page text, not just titles. |
| SYS-SNG-03 | FR-LIB-11, FR-LIB-12 | | `GET /api/songs/:id` returns pages in ascending `position`, and the seeded `Reff` label is returned verbatim. |

### 6.4 Import

| ID | Traces | C | Case |
|---|---|---|---|
| SYS-IMP-01 | FR-IMP-03, URS-SONG-02 | | Import a valid song file → 200 with `imported: 1`. The song is then findable via `GET /api/songs?q=`. |
| SYS-IMP-02 | FR-IMP-06, FR-IMP-07, URS-AVL-06 | **C** | Record the song count and the full song list. Import a malformed file → 422 `IMPORT_REJECTED`. Re-read: **count and list are identical**. Nothing was written. |
| SYS-IMP-03 | FR-IMP-09, URS-SONG-03 | **C** | Import a song, then import the same title and author with changed lyrics → `updated: 1, imported: 0`, the song count is unchanged, and `GET /api/songs/:id` returns the new lyrics. No duplicate. |
| SYS-IMP-04 | FR-IMP-10 | | `POST /api/import` without the pair cookie → 401. |
| SYS-IMP-05 | FR-ADM-02, URS-ADM-02 | | The 422 body's `error.message` names the offending record or construct, not a generic failure. |

### 6.5 Services

Ids 01 to 05 are the files in `tests/api/06-services/`, in run order. A `b` suffix is a second file asserting the other half of the same case — the half that has to be checked after the first request has run. SYS-SVC-06 is listed here and **not yet written**; see §12.

| ID | Traces | C | Case |
|---|---|---|---|
| SYS-SVC-01 | FR-SVC-01, URS-PREP-01 | | `POST /api/services` creates a service with a name and date, and returns its id. |
| SYS-SVC-02 | FR-SVC-03, FR-SVC-04, URS-PREP-02 | | A `song` item can be appended and round-trips its `ref` unchanged. |
| SYS-SVC-02b | FR-SVC-03, FR-SVC-04, URS-PREP-02 | | A `verse` item can be appended and round-trips its `ref` unchanged. The remaining kinds — `slide`, `media`, `countdown` — are not yet covered; see §12. |
| SYS-SVC-03 | FR-SVC-06, URS-PREP-03 | | `POST .../items/reorder` with a permuted id list → the response lists the items in that order, with positions renumbered from zero and no gaps. |
| SYS-SVC-04 | FR-SVC-06 | | Reorder with a list missing one item → 400 `INCOMPLETE_ORDER`. |
| SYS-SVC-04b | FR-SVC-06 | **C** | After that rejection, `GET /api/services/:id` returns the order unchanged. A refused reorder must not half-apply. |
| SYS-SVC-05 | FR-SVC-07, URS-PREP-04 | | Delete a `song` item from a service → the item is gone from `GET /api/services/:id`. |
| SYS-SVC-05b | FR-SVC-07, URS-PREP-04 | **C** | `GET /api/songs/:id` for the song that item referenced still returns 200. Removing from a run must not delete from the library. |
| SYS-SVC-06 | FR-SVC-05, URS-PREP-05 | | An item's `label` round-trips and appears in `GET /api/services/:id`. |

### 6.6 Live

Ids 01 to 12 are the files in `tests/api/07-live/`, in run order. 13 to 16 are listed here and **not yet written**; see §12.

| ID | Traces | C | Case |
|---|---|---|---|
| SYS-LIV-01 | FR-LIV-01, URS-LIVE-01, URS-LIVE-11 | | `GET /api/live` returns the full state shape — `live`, `preview`, `blackout`, `skipped`, `outputsConnected` — not a delta. |
| SYS-LIV-02 | FR-LIV-03, URS-LIVE-02 | **C** | With item A live, `preview` item B → `preview.itemId` is B and `live.itemId` is still A. |
| SYS-LIV-03 | FR-LIV-04, URS-LIVE-03 | | `go` → `live` becomes the previewed item at the previewed page, `preview` becomes null, and `live.content` carries the renderable payload. |
| SYS-LIV-04 | FR-LIV-05, URS-LIVE-03 | **C** | `go` with nothing previewed → 409 `NO_PREVIEW`, and the returned `state` shows the output where it was. |
| SYS-LIV-05 | FR-LIV-06, URS-LIVE-04 | | `advance` → the page index moves forward one, within the same item. |
| SYS-LIV-06 | FR-LIV-07, URS-LIVE-04 | **C** | `advance` on the last page → 200, page unchanged, item unchanged. No wrap, no fall-through. |
| SYS-LIV-07 | FR-LIV-07, URS-LIVE-04 | | `back` on page 0 → 200 and the page index stays 0. Never negative. |
| SYS-LIV-08 | FR-LIV-08, URS-AVL-03 | **C** | `preview` an item not in the service → 409 `UNKNOWN_ITEM`, and the returned `state` shows the output untouched. |
| SYS-LIV-09 | FR-LIV-09, URS-LIVE-09 | **C** | `blackout {on:true}` → `blackout` is true and `live` still names the same item and page. |
| SYS-LIV-10 | FR-LIV-09, URS-LIVE-09 | **C** | `blackout {on:false}` → the same item and page return. |
| SYS-LIV-11 | FR-LIV-10, URS-LIVE-08 | **C** | `skip` an item → it appears in `skipped` and nothing on the screen changes. |
| SYS-LIV-12 | FR-LIV-11, URS-LIVE-10 | | `clear` → `live` is null and the skip marks survive. |
| SYS-LIV-13 | FR-LIV-01 | | An unrecognised `type` → 400. No silent no-op. **Covered by the integration suite**, not Bruno: it is a deserialisation concern at the API boundary. |
| SYS-LIV-14 | FR-LIV-17, URS-AVL-04 | **C** | Preview the seeded item whose media file is deliberately absent, then `go` → 409 `MEDIA_UNAVAILABLE` and `live` unchanged. Needs a media item in the seeded service. |
| SYS-LIV-15 | FR-LIV-15, FR-LIV-16, URS-LIVE-06, URS-LIVE-07 | **C** | Capture `GET /api/services/:id`, run a full live sequence including a free-form push of something outside the order, re-read the service: identical. Live never writes to the service. |
| SYS-LIV-16 | FR-LIV-12 | | `GET /api/live` and `POST /api/live/command` return identical keys. One state format, not two. |

### 6.7 Media

| ID | Traces | C | Case |
|---|---|---|---|
| SYS-MED-01 | IF-SW-03, URS-MED-03 | | `GET /api/media/:id/file` with `Range: bytes=0-99` → 206, `Content-Range` present, body 100 bytes. Video seeking depends on this. |
| SYS-MED-02 | NFR-SEC-04 | **C** | `POST /api/media` with a filename containing `../` or an absolute path → 400 `BAD_FILENAME`, and no file is written outside the media directory. |
| SYS-MED-03 | FR-LIB-23, URS-AVL-04 | **C** | `GET /api/media` marks the seeded missing file as `available: false` while still returning the rest of the list. One broken file must not break the library. |
| SYS-MED-04 | FR-LIB-20 | | `GET /api/media/:id/file` for the missing file → 404 `MEDIA_FILE_MISSING`, not 500. |

## 7. Integration and failure

These are the cases the product exists for. INT-01 to INT-03 and INT-07 to INT-10 should be scripted once the server exists; the rest need a person.

| ID | Traces | C | Case |
|---|---|---|---|
| INT-01 | NFR-REL-02, URS-AVL-02 | **C** | **Given** a verse is live, **when** the control browser is force-killed, **then** the congregation display shows the same verse, unchanged, indefinitely. Reopening the control view shows the same live state. |
| INT-02 | FR-LIV-13, FR-LIV-14, NFR-REL-03, URS-AVL-03 | **C** | **Given** a verse is live on page 2, **when** the server process is killed and restarted, **then** within 30 seconds the congregation display returns to the same item and page with no operator action. |
| INT-03 | FR-OUT-05, NFR-REL-05, URS-AVL-03 | **C** | **When** the output client's network is interrupted and restored, **then** it reconnects within 5 seconds and renders current live state without the operator re-pushing anything. |
| INT-04 | NFR-REL-01, URS-AVL-01 | **C** M | **When** the building's internet uplink is physically disconnected, **then** every function — lookup, search, import, media playback, live control — works unchanged. |
| INT-05 | NFR-REL-07 | M | **When** a media upload fills the disk, **then** the upload fails with a stated reason, no partial file remains in the media directory, and no `media` row exists. |
| INT-06 | FR-OUT-02, NFR-REL-04, URS-AVL-05 | **C** | **When** the output client loses its connection, **then** it renders black — not the last frame with an error overlay, not a browser error page, not a reconnect spinner. |
| INT-07 | FR-LIV-12 | **C** | **When** a client connects to `/hub/live`, **then** the first message it receives is a complete state object, before any other message. |
| INT-08 | FR-LIV-11 | **C** | **When** one client issues a command, **then** every connected client receives the resulting full state. Verified with three clients connected. |
| INT-09 | FR-SEC-10 | | **When** a client connects with `role=output` and no pair cookie, **then** the connection is accepted. |
| INT-10 | FR-SEC-07, URS-SEC-01 | **C** | **When** a client connects with `role=control` and no pair cookie, **then** the upgrade is rejected. |
| INT-11 | FR-LIV-02, URS-LIVE-11 | | **When** the output client disconnects, **then** `outputsConnected` drops and the control view shows the disconnected state within 5 seconds. |
| INT-12 | FR-ADM-01, URS-ADM-01 | M | **When** the database file and media directory are copied to another machine and the server started there, **then** all content and the prepared services are present. Backup is proven by restore, not by the copy succeeding. |
| INT-13 | NFR-REL-09, URS-AVL-01 | **C** | **When** the server is started with no cache configured, **then** it starts and serves normally. A cache is never a precondition for the app running. |
| INT-14 | NFR-REL-09 | **C** | **Given** the cache is configured but unreachable, **when** a verse page is requested, **then** it is served from the database, one warning is logged, and no request fails. |
| INT-15 | FR-LIB-05, FR-LIB-13 | **C** | **When** the migrations are applied and a verse row is inserted, **then** the FTS5 index contains it. An index is only ever wrong against a real database, which is why there is no mock here. |
| INT-16 | FR-SVC-02, FR-SVC-04 | | **When** a service with items is saved and reloaded, **then** the items come back in position order with their kind-specific `ref` intact, and a reorder that is not a permutation is refused. |

## 8. Performance

Measured on the booth machine over the sanctuary network. All thresholds are unconfirmed targets — see SRS-OPN-03.

| ID | Traces | Case |
|---|---|---|
| PERF-01 | NFR-PERF-01 | GO to congregation display change ≤ 200 ms. |
| PERF-02 | NFR-PERF-02 | Page advance to display change ≤ 200 ms. |
| PERF-03 | NFR-PERF-03 | Verse reference lookup ≤ 150 ms. |
| PERF-04 | NFR-PERF-04 | Full-text search across all translations ≤ 800 ms to first results. |
| PERF-05 | NFR-PERF-05 | Song search ≤ 300 ms. |
| PERF-06 | NFR-PERF-06 | Video playback begins ≤ 1 s after going live. |
| PERF-07 | NFR-PERF-07 | Server accepts connections ≤ 15 s after start. |
| PERF-08 | NFR-PERF-08 | Client loads and shows current state ≤ 5 s. |
| PERF-09 | NFR-CAP-01 to NFR-CAP-06 | With 6 translations of 40,000+ verses, 3,000 songs, 200 services, a 100-item service, and 8 connected clients, PERF-01 to PERF-08 still hold. This is the only case run at full capacity. |

## 9. Manual

Nothing here can be automated. Each needs a person, and MAN-01, MAN-02, and MAN-07 need the actual sanctuary.

| ID | Traces | C | Case |
|---|---|---|---|
| MAN-01 | FR-OUT-06, URS-OUT-04 | **C** | Verse and lyric text is readable from the rear-most seat in the sanctuary, by a person with ordinary corrected vision. |
| MAN-02 | NFR-USE-01, IF-UI-04, CON-05 | **C** | Walk every screen and state of the control view in the darkened booth. No state presents a predominantly light screen, and the operator's face is not lit. |
| MAN-03 | NFR-USE-02, IF-UI-05, CON-06 | **C** | Step through all ten material states. No control used during a service changes position, size, or order between any two of them. |
| MAN-04 | NFR-USE-03, URS-LIVE-03 | **C** | The GO control is separated from every other control by a gap large enough that no adjacent control can be struck instead of it. Verified by deliberate fast, imprecise operation. |
| MAN-05 | NFR-USE-04, URS-LIVE-01 | **C** | Live and staged content are distinguishable at a glance, from across the booth, without reading any text. |
| MAN-06 | NFR-USE-05 | | All ten states in the surface brief — empty service, live, staged, armed, skipped, deferred, empty search, media loading, output disconnected, blackout — have a defined visual treatment. None falls back to a default or shows nothing. |
| MAN-07 | NFR-USE-06, URS-ADM-03 **(P)** | | A volunteer who has never seen the system reads one page of instructions and runs a prepared service unaided. Observed, not self-reported. |
| MAN-08 | IF-HW-01, URS-OUT-03 | **C** | The output view fills the second display with no browser chrome, no address bar, no OS taskbar, and no cursor. |
| MAN-09 | FR-OUT-01, URS-OUT-02 | **C** | Over a full run-through, the congregation display never shows a control, a menu, a search result, staged content, or an error. Watched continuously by a second person facing the screen. |

## 10. Acceptance

| ID | Traces | C | Case |
|---|---|---|---|
| UAT-01 | URS §11 | **C** | A full service is run end to end on the booth machine and the real displays, with the sanctuary network live and the building's internet uplink physically disconnected, using a service prepared the previous day, including at least one unprepared passage found and shown on the fly, and including a deliberate mid-service restart of both the control view and the server. No intervention outside the system is required at any point. |
| UAT-02 | URS-PREP-06 | | A service prepared on Saturday is present and unchanged on Sunday, after the machine has been powered off overnight. |
| UAT-03 | URS-LIVE-06, URS-LIVE-07 | **C** | The pastor names a passage not in the order. The operator finds and shows it within 15 seconds, and the prepared order is untouched afterwards. |
| UAT-04 | URS-LIVE-08 | | A prepared item is skipped during the service and shown later from the same run. |
| UAT-05 | URS-SONG-02 | | An administrator imports a new song on the Wednesday. It is findable and usable on the Sunday. |
| UAT-06 | URS-SEC-03 | | A device paired before Saturday must re-enter the PIN after Saturday. The new PIN is readable at the booth machine. |
| UAT-07 | URS-PREP-07 | | An item is added to the service after the service has already begun, without disturbing what is live. |

---

## 11. Coverage matrix

Every URS requirement, with the cases that verify it.

| URS | Verified by |
|---|---|
| URS-PREP-01 | SYS-SVC-01 |
| URS-PREP-02 | SYS-SVC-02, SYS-SVC-02b |
| URS-PREP-03 | SYS-SVC-03, SYS-SVC-04, SYS-SVC-04b |
| URS-PREP-04 | SYS-SVC-05, SYS-SVC-05b |
| URS-PREP-05 | SYS-SVC-06 |
| URS-PREP-06 | UAT-02 |
| URS-PREP-07 | SYS-SVC-02, UAT-07 |
| URS-PREP-08 **(P)** | *pending — requirement unconfirmed* |
| URS-LIVE-01 | SYS-LIV-01, MAN-05 |
| URS-LIVE-02 | UNT-LIV-02, SYS-LIV-02 |
| URS-LIVE-03 | UNT-LIV-03, UNT-LIV-04, SYS-LIV-03, SYS-LIV-04, MAN-04 |
| URS-LIVE-04 | UNT-LIV-05 to UNT-LIV-08, SYS-LIV-05, SYS-LIV-06, SYS-LIV-07 |
| URS-LIVE-05 | SYS-SVC-02, UAT-01 |
| URS-LIVE-06 | SYS-BIB-05, SYS-SNG-02, SYS-LIV-15, UAT-03 |
| URS-LIVE-07 | UNT-LIV-19, SYS-LIV-15, UAT-03 |
| URS-LIVE-08 | UNT-LIV-15 to UNT-LIV-17, SYS-LIV-11, UAT-04 |
| URS-LIVE-09 | UNT-LIV-10, UNT-LIV-11, SYS-LIV-09, SYS-LIV-10 |
| URS-LIVE-10 | UNT-LIV-12, SYS-LIV-12 |
| URS-LIVE-11 | SYS-LIV-01, INT-11 |
| URS-LIVE-12 **(P)** | *pending — requirement unconfirmed; INT-08 covers the mechanism* |
| URS-BIB-01 | SYS-BIB-01 |
| URS-BIB-02 | INT-04 |
| URS-BIB-03 | UNT-REF-01 to UNT-REF-11, SYS-BIB-02, SYS-BIB-03 |
| URS-BIB-04 | SYS-BIB-05 |
| URS-BIB-05 | SYS-BIB-04 |
| URS-BIB-06 | UNT-REF-05, SYS-BIB-02 |
| URS-BIB-07 | UNT-IMP-08, SYS-IMP-01 |
| URS-BIB-08 | SYS-BIB-06 |
| URS-SONG-01 | SYS-SNG-03, UNT-LIV-05 |
| URS-SONG-02 | SYS-IMP-01, UAT-05 |
| URS-SONG-03 | SYS-IMP-03 |
| URS-SONG-04 | UNT-IMP-03 to UNT-IMP-05, SYS-SNG-03 |
| URS-SONG-05 | SYS-SNG-01, SYS-SNG-02 |
| URS-SONG-06 | INT-04 |
| URS-SONG-07 | SYS-SNG-03 |
| URS-MED-01 | SYS-MED-01 — *`media` item kind not covered; see §12* |
| URS-MED-02 | SYS-MED-02 |
| URS-MED-03 | SYS-MED-01, PERF-06 |
| URS-MED-04 | SYS-SVC-02 — *rendering not covered; see §12* |
| URS-MED-05 | INT-04 |
| URS-OUT-01 | MAN-08 |
| URS-OUT-02 | MAN-09, INT-06 |
| URS-OUT-03 | MAN-08 |
| URS-OUT-04 | MAN-01 |
| URS-AVL-01 | INT-04 |
| URS-AVL-02 | INT-01 |
| URS-AVL-03 | INT-02, INT-03 |
| URS-AVL-04 | UNT-LIV-13, SYS-LIV-14, SYS-MED-03 |
| URS-AVL-05 | INT-06, MAN-09, SYS-MED-04 |
| URS-AVL-06 | UNT-IMP-12, SYS-IMP-02 |
| URS-SEC-01 | SYS-SEC-03, INT-10 |
| URS-SEC-02 | SYS-SEC-01, SYS-SEC-02 |
| URS-SEC-03 | UNT-PIN-03 to UNT-PIN-09, SYS-SEC-06, UAT-06 |
| URS-SEC-04 | SYS-SEC-04, UAT-06 |
| URS-SEC-05 | SYS-SEC-04 *(inverse)* — *see §12* |
| URS-ADM-01 | INT-12 |
| URS-ADM-02 | SYS-IMP-05 |
| URS-ADM-03 **(P)** | MAN-07 |
| URS-ADM-04 **(P)** | *pending — requirement unconfirmed* |
| CON-05 | MAN-02 |
| CON-06 | MAN-03, MAN-07 |

## 12. Requirements with no test, and why

Stated so that a gap reads as a decision.

| Requirement | Why no case |
|---|---|
| URS-PREP-08, URS-LIVE-12, URS-ADM-04 | Marked **(P)**. Proposed by the analyst, not confirmed by the church. Cases will be written once confirmed. |
| URS-MED-04 (countdown rendering) | `FR-OUT-04` renders a live-updating countdown on the client. Verifying that it counts correctly needs a client-side test harness that does not exist yet. **Gap — should be closed before release.** |
| URS-SEC-05 (loopback skips the PIN) | The positive case needs the test client to originate from loopback while the negative case needs it not to. The Bruno suite runs from one host. **Gap — needs a two-host or interface-bound test.** |
| SYS-SVC-06 (item label round-trips) | Specified above, no file written. The label is sent in SYS-SVC-02 and SYS-SVC-02b but never read back. One assertion on an existing `GET`; write it. |
| SYS-SVC-02 for `slide`, `media`, `countdown` | Only `song` and `verse` items are appended by the suite. The other three kinds share the same endpoint and the same `ref` column, so the risk is a per-kind validation rule, not the write path. **Gap — cheap to close, extend the seeded service.** |
| SYS-LIV-14 to SYS-LIV-16 | Specified in §6.6, no files written. 14 needs a media item whose file is absent to exist in the seeded service; 15 and 16 are assertions over responses the suite already collects. |
| `Access:TestPin` and `Access:RequirePairingFromLoopback` refused in Production | The design says both settings are refused when the environment is Production, so a test convenience cannot survive into a real start. Nothing asserts it. Needs a host started with `Environment=Production` and those keys set, asserting startup fails. **Gap — this one guards the access control itself.** |
| SRS §2.4 CON-07 to CON-10 | Constraints on construction, not observable behaviour. Verified by inspection, not by test. |
| NFR-MNT-01, NFR-MNT-05 | Structural properties. Verified by code review. |
| SRS-OPN-01 to SRS-OPN-06 | Open decisions, not requirements. |
| Positioning (URS OPN-01) | Not a requirement and not testable. |

Three of the gaps above are the kind that lets a real fault through, and all three should be closed before the system runs a live service: countdown rendering, the loopback exemption, and the Production refusal of the two test-only settings. The rest are specified cases waiting on a file, which is bookkeeping rather than risk.
