# Church Service Projection — Backend Design

**Date:** 2026-08-20
**Revised:** 2026-08-20 — stack changed from Node/Fastify to .NET 10 / C# with a clean-architecture layering. Requirements are unaffected; `URS.md` and `SRS.md` stand as written.
**Status:** Draft — awaiting review
**Scope:** Backend for the church service projection app. The operator's live control *screen* is specified separately in `.impeccable/surfaces/src-screens-livecontrol.md`.

## Context

A live presentation tool for church services. Content — Bible verses, song lyrics, announcement and sermon slides, and media — is prepared ahead of time and put on the congregation's screen in real time as the service runs.

The operator is often a rotating volunteer working in a dim media booth at the back of the sanctuary, following the pastor or worship leader. Success is speed with a low error rate. The governing constraint is that **a live service must not fail**, which rules out any dependency on church internet in the live path.

Product truth confirmed during discovery:

- **Dual-screen.** An operator control view drives a separate audience-facing output.
- **Four content types.** Bible verses, song lyrics, announcement/sermon slides, media (video, countdown, image).
- **Navigation is both.** A prepared run-of-service is the spine; the operator can break away at any moment to search and push anything on the fly.
- **Single congregation.** This is an internal tool for one church, not a product distributed to others.
- **Bible text:** Terjemahan Baru, Terjemahan Lama, and English. Fully offline.
- **Songs** are updated by importing lyrics.

## Decisions

### Deployment: LAN server on the booth PC

One ASP.NET Core process runs on the booth machine. The control view, the projector output, and any future phone remote are browser clients over church wifi. No internet is needed during a service.

Rejected: a local desktop app, which is simpler but confines everything to one machine and forecloses a second operator or a phone remote. Rejected: cloud SaaS, which puts the live path behind a network that regularly fails in this exact building.

### Store: SQLite through EF Core

One SQLite file. The Bible is roughly 31,000 verses per translation across three or more translations; verse lookup and free-text search need a real index, and FTS5 provides it.

EF Core owns the schema, the migrations, and every write. FTS5 is the one exception: EF cannot model a virtual table, so the FTS tables and their sync triggers are created in a migration as raw SQL and queried through `FromSqlRaw` inside the two search repositories.

> `// ponytail: FTS5 is reachable only from VerseRepository.SearchAsync and SongRepository.SearchAsync. If EF ever models virtual tables, only those two change.`

Media binaries live on disk and are referenced by path.

### Live state: server-authoritative

The server holds live state. All clients are dumb renderers. If the control browser crashes mid-sermon, the projector keeps showing what it was showing, and reopening the control view resyncs instantly.

Rejected: making the control view authoritative with the server as a relay. It is less server code, but it makes the control browser a single point of failure during a live service — the exact failure this product exists to prevent.

### Bible text and licensing

All translations, TB included, arrive through the import pipeline. Nothing is bundled.

LAI holds copyright on Terjemahan Baru. Loading TB onto this church's own booth PC for its own worship is ordinary liturgical use. Because the app is not distributed to other churches, no redistribution question arises. Were that to change, the import-only design already keeps TB out of any package, and the correct step would be a permission request to LAI.

### Redis: cache only, and always optional

Redis caches resolved verse pages and search results. It is never authoritative and never on the write path.

**A cache must not be able to stop a service.** If Redis is unreachable, `CachedVerseRepository` logs once and delegates straight to the EF repository behind it. Nothing on the live path throws because a cache is down. This is stated here because it is the difference between an optimisation and a new Sunday-morning failure mode.

> `// ponytail: cache-aside on verse pages only. Widen when a slow query is actually measured, not before.`

### Docker: development and CI, not the booth

`Dockerfile` and `compose.yaml` exist for development and the test pipeline, where a throwaway Redis is convenient.

The booth machine runs a self-contained `win-x64` publish installed as a Windows Service. A volunteer can restart a Windows Service. Keeping a container runtime alive on the booth PC is one more dependency standing between the congregation and the screen.

## Architecture

Onion layering. Dependencies point inward only; nothing in an inner layer references an outer one.

```
ChurchProjection.sln
src/
  ChurchProjection.Domain/          entities, value objects, LiveSession aggregate. No dependencies at all.
  ChurchProjection.Application/     use cases and port interfaces. Depends on Domain.
  ChurchProjection.Infrastructure/  EF Core, SQLite, FTS5, parsers, file media, Redis. Depends on Application.
  ChurchProjection.Api/             ASP.NET Core endpoints, SignalR hub, DI composition. Depends on all three.
tests/
  ChurchProjection.Domain.Tests/
  ChurchProjection.Application.Tests/
  ChurchProjection.Api.Tests/       integration, WebApplicationFactory
  api/                              Bruno collection, black-box HTTP
```

`Api` is the only project that names a concrete database, cache, or file system. Swapping SQLite for something else, or dropping Redis, touches Infrastructure and one DI registration.

**Domain holds no EF attributes.** Entities are plain classes; mapping lives in `IEntityTypeConfiguration<T>` classes in Infrastructure. The persistence model is Infrastructure's problem, and keeping it there is what makes the Domain tests run without a database.

### Data access: one repository per aggregate

Every read and every write crosses a repository interface. The interfaces are **ports in Application**; the EF Core implementations are **adapters in Infrastructure**. Application never sees `DbContext`, and no `IQueryable` crosses the boundary — a repository returns Domain objects or plain result records, fully materialised. A repository that returned a query would let a use case compose SQL by accident, which is the leak that makes the layering decorative.

There are six, one per aggregate, and they are named for what they store rather than for a table:

```csharp
// ChurchProjection.Application/Ports

public interface ITranslationRepository
{
    Task<IReadOnlyList<Translation>> ListAsync(CancellationToken ct);
    Task<Translation?> FindAsync(TranslationId id, CancellationToken ct);
}

public interface IVerseRepository
{
    Task<Passage?> GetAsync(TranslationId translation, BibleReference reference, CancellationToken ct);
    Task<IReadOnlyList<VerseHit>> SearchAsync(TranslationId translation, string query, int limit, CancellationToken ct);
    Task ReplaceTranslationAsync(TranslationId translation, ImportPayload payload, CancellationToken ct);
}

public interface ISongRepository
{
    Task<Song?> FindAsync(SongId id, CancellationToken ct);
    Task<IReadOnlyList<SongHit>> SearchAsync(string query, int limit, CancellationToken ct);
    Task<SongId> UpsertAsync(Song song, CancellationToken ct);
}

public interface IMediaRepository
{
    Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct);
    Task<MediaItem?> FindAsync(MediaId id, CancellationToken ct);
    Task<MediaId> AddAsync(MediaItem item, CancellationToken ct);
    Task RemoveAsync(MediaId id, CancellationToken ct);
}

public interface IServiceRepository
{
    Task<ServicePlan?> FindAsync(ServiceId id, CancellationToken ct);
    Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken ct);
    Task SaveAsync(ServicePlan plan, CancellationToken ct);   // whole aggregate, items included
    Task RemoveAsync(ServiceId id, CancellationToken ct);
}

public interface ILiveStateRepository
{
    Task<LiveSnapshot?> LoadAsync(CancellationToken ct);
    Task SaveAsync(LiveSnapshot snapshot, CancellationToken ct);
}
```

> `// ponytail: no generic IRepository<T>, no Add/Update/Delete/GetAll on everything. Each interface lists the queries that are actually called. A generic base would force every aggregate to admit operations that would corrupt it — nothing may delete a verse individually, and nothing outside import may write one.`

**`ServicePlan` saves whole.** `SaveAsync` takes the aggregate and reconciles its items in one call, rather than exposing per-item add, delete, and reorder methods. Reordering is then a property of the aggregate, and the "positions renumbered from zero with no gaps" rule has one place to live instead of three.

**Transactions belong to the use case, not the repository.** A repository method does not open a transaction, because import touches two of them and needs both to fail together:

```csharp
public interface IUnitOfWork
{
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
```

`ImportLibrary` (the use case) wraps its writes in that, while `ImportService` in Infrastructure only picks a parser and returns the payload; every other use case calls a single repository method and needs nothing. The EF implementation is `DbContext.Database.BeginTransactionAsync`.

**FTS5 does not escape.** `IVerseRepository.SearchAsync` and `ISongRepository.SearchAsync` are the only doors to the full-text tables, and their `FromSqlRaw` calls live inside those two adapters. The search query string is passed as a parameter, never concatenated.

**The cache is a decorator, not a call in the use case.** `CachedVerseRepository` implements `IVerseRepository`, wraps the EF one, and is registered over it in Infrastructure's DI. Use cases hold `IVerseRepository` and cannot tell the difference; if Redis is dropped, the registration is removed and nothing else changes. This is also what keeps the "a cache must not stop a service" rule in one class instead of at every call site.

**The test host does not fake repositories.** Integration tests run against a real SQLite file with the real adapters, because the queries — FTS5 especially — are the part most likely to be wrong. Hand-written fakes are used only where a Domain test needs `IServiceOrder`.

### Three bounded areas

**Library** owns content: translations, verses, songs, media. Import writes here. Exposes lookup and search.

**Service** owns the prepared run: an ordered list of items, editable at any time.

**Live** owns the authoritative now-state and its broadcast. It reads Library and Service and **never writes to either.** This makes structural the surface brief's rule that the run overlay never edits the terrain: pushing, skipping, and reordering during a service leave the stored content untouched.

### The live aggregate

`LiveSession` is a Domain aggregate root with no I/O and no framework types. Every command is a method that either succeeds or returns a refusal code, and **a refusal leaves every property untouched** — guard clauses run to completion before the first mutation.

```csharp
public sealed class LiveSession
{
    public Slot? Live { get; private set; }
    public Slot? Preview { get; private set; }
    public bool Blackout { get; private set; }
    public IReadOnlyCollection<ItemId> Skipped => _skipped;

    public LiveResult PreviewItem(ItemId id, int pageIndex, IServiceOrder order);
    public LiveResult Go();
    public LiveResult Advance(IServiceOrder order);
    public LiveResult Back();
    public LiveResult SetBlackout(bool on);
    public LiveResult Clear();
    public LiveResult Skip(ItemId id, IServiceOrder order);
    public LiveResult Unskip(ItemId id);
}
```

`IServiceOrder` is the read-only view the aggregate needs — does this item exist, how many pages does it have, is its media file present — and nothing more. It is an interface so the aggregate never reaches for a repository, which is what keeps the whole state machine unit-testable without a database.

`LiveResult` is either `Ok` or a refusal carrying one of the codes in the API contract. Refusals are values, not exceptions: they are expected operator behaviour, not faults.

### One command path, two doors

`LiveCommandHandler` in Application is the single place a command is applied, persisted, and broadcast. Both `POST /api/live/command` and the SignalR hub method call it. There is no second implementation to drift.

## Data model

Verses are keyed on a **canonical book id** rather than per-translation book names, so switching between TB, TL, and an English translation stays on the same verse.

```
translations(id, abbrev, name, language)
book_names(translation_id, book_id, name, abbrev)
verses(translation_id, book_id, chapter, verse, text)
verses_fts                                    -- FTS5, raw SQL migration
songs(id, title, author, ccli, language, updated_at)
song_pages(song_id, position, section_label, text)
songs_fts                                     -- FTS5, title + page text
media(id, kind, filename, path, duration_ms, width, height)
services(id, name, service_date)
service_items(id, service_id, position, kind, ref_json)
settings(key, value)                          -- current_pin, pin_rotated_at
live_state(id=1, service_id, item_id, page_index, blackout, skipped_json, updated_at)
```

`skipped_json` is an array of item ids the operator has passed over. It lives in `live_state` rather than on `service_items` because skipping is a fact about *this run*, not about the stored service — and because putting it there means a control-view crash mid-service does not lose the skip marks.

`book_id` is a plain integer and is not constrained to 1–66, so a deuterocanonical translation can be imported without a schema change.

`section_label` on `song_pages` is free text. Indonesian congregations write "Reff", not "Chorus".

`service_items.kind` is one of `bible | song | slide | media | countdown`. `ref_json` carries the kind-specific payload, mapped by EF as an owned JSON column. That is five kinds against the four content types above, because a countdown is not a stored file and needs its own kind.

> `// ponytail: ref_json is opaque to SQL. If "which services used song X" ever matters, add an index table then.`

`token_secret` is gone from `settings`. ASP.NET Core Data Protection signs the pairing ticket, and its key ring is persisted to the data folder so tickets survive a restart. That is a framework concern, and leaving it in the settings table would mean hand-rolling what the framework already does correctly.

`live_state` is a single row written on every change. It buys crash recovery for a handful of lines.

## Live protocol

SignalR over WebSocket. The server is authoritative. Message payloads are exactly as `docs/requirements/API-CONTRACT.md` defines; SignalR supplies the framing, reconnect, and group membership.

Hub: `/hub/live`.

| Direction | Member |
|---|---|
| Client to server | `SendCommand(LiveCommand)` |
| Server to clients | `StateChanged(LiveState)` |

Clients declare `role: control | output | remote` on connect and are placed in a matching group. `outputsConnected` is an in-process counter of the output group, which is correct because there is exactly one server instance and always will be.

Full state is broadcast on every change rather than deltas; the payload is one slide of text. On connect the server pushes full state immediately, and that is the entire resync story.

> `// ponytail: full-state broadcast; deltas when it measurably matters.`

## Import

One port, implemented several times:

```csharp
public interface IImportParser
{
    bool CanHandle(string fileName, ReadOnlySpan<byte> head);
    ImportPayload Parse(Stream input);   // throws ImportException with a stated reason
}
```

The parsers are registered as a collection and `ImportService` takes the first that handles the file. Adding a format is a new class and one registration.

- **Bible, JSON pack** — our own format.
- **Bible, Zefania XML** — the format actually available for download.
- **Song, plain text** — first line is the title, a blank line starts a new page, a bracketed line or one ending in a colon is a section label. This is the paste-from-Word path.
- **Song, OpenLyrics XML** — for migrating off OpenLP.

> `// ponytail: OSIS and SWORD parsers when someone actually has a file in those formats.`

The whole file parses before anything is inserted, and insertion runs inside one `IUnitOfWork.InTransactionAsync` call. Re-import **updates** rather than duplicates, matched on translation abbrev for Bibles and on title plus author for songs. This is what makes "import the lyric to update the songs" work. A successful Bible import evicts that translation's cache entries.

## Media

Files are stored under the configured media root and served with `enableRangeProcessing: true`, which video seeking requires. Metadata lives in the `media` table.

Media is addressed by database id, never by a caller-supplied path. Uploaded filenames are sanitised on the way in, and the resolved path is checked with `Path.GetFullPath` against the media root before any stream is opened. Both are a few lines and both are the difference between a media server and an arbitrary-file-read.

Countdown is not a file. It is a `service_items.kind` whose `ref_json` holds a target time, rendered by the output client.

## Access control

A six-digit PIN is stored in `settings`, generated with `RandomNumberGenerator`. Rotation is checked lazily on each request: if `pin_rotated_at` predates the most recent Saturday 00:00 local time, the server rotates the PIN and logs the new value to the booth console.

> `// ponytail: lazy rotation on request, no hosted background service.`

`POST /api/pair {pin}` issues a cookie authentication ticket carrying `pin_rotated_at` as a claim. The ticket is rejected the moment that claim no longer matches the stored value, so devices re-pair weekly with no extra code and no revocation list. The SignalR negotiate request carries the same cookie and is authorised identically.

Rotation lands at Saturday 00:00, ahead of both Saturday-evening and Sunday services. No service crosses a rotation.

Requests from loopback skip the PIN — that is the operator's own machine. `GET /api/pin` is loopback-only so the booth can display the current PIN.

The loopback exemption can be switched off by configuration so the API test suite can prove the pair gate actually rejects. **That switch is refused when the environment is Production**, because a test convenience that survives into a real start is not a test convenience, it is a hole.

Failed pairing is rate-limited per source address using the framework's fixed-window limiter, returning 429 with `Retry-After`. Six digits is a million combinations; without a limit, a script on the church wifi walks it in an afternoon.

**Accepted risk.** This is plain HTTP over the LAN, so the PIN and the ticket cross the wire in cleartext and anyone capturing traffic on the church network can read them. Accepted: the asset is a screen in a room those people are already sitting in, and terminating TLS on a LAN host means certificate management no volunteer can maintain. Revisit before the server is ever reachable beyond the sanctuary network.

## Failure modes

| Failure | Behavior |
|---|---|
| Output window disconnects | Server keeps state. Control view shows a disconnected badge. Output resyncs to current live content on reconnect, with no manual re-push. |
| Control view crashes or reloads | Projector is unaffected. Reconnect pulls full state. |
| Server process dies | `live_state` is persisted. SignalR reconnects with backoff and clients land on the same slide. The projector shows black during the gap, never garbage. |
| **Redis unreachable** | Cache reads return a miss, one warning is logged, and the request reads SQLite. No user-visible effect beyond latency. |
| Media file missing from disk | Renders as a labeled placeholder **in preview** and refuses to go live with a stated reason. Fails loud in preview, never in front of the congregation. |
| Import file malformed | The whole file is rejected in one transaction, the offending record is reported, and the database is unchanged. Never a half-imported Bible. |
| Disk full during media upload | Upload fails with a stated reason; no partial file is left on disk and no `media` row is written. |

## Testing

Detailed in `docs/testing/TEST-PLAN.md` and `docs/testing/TEST-CASES.md`. In summary:

- **Domain tests (xUnit)** — `LiveSession`, PIN rotation, Bible reference parsing. No database, no host, no mocks: these types have no dependencies to mock.
- **Application tests (xUnit)** — import parsers against fixture files, including a truncated file that must reject wholly.
- **Integration tests (xUnit + `WebApplicationFactory`)** — the composed pipeline against a real SQLite file with the real EF repositories, plus a SignalR client asserting that a command broadcasts to a second connection. The repositories are deliberately not faked here: their queries, FTS5 above all, are the part most likely to be wrong.
- **API tests (Bruno)** — black-box HTTP against a running server, unchanged by this stack decision because they were written against the contract rather than the implementation.

Not covered: React component tests.

## Out of scope for this design

- The operator's live control screen UI, specified in `.impeccable/surfaces/src-screens-livecontrol.md`.
- The content prep/planning UI.
- The phone remote (`/remote`), which needs no server change when it arrives.

## Open decisions, deliberately not invented

- **Product positioning.** Asked directly during `init`; answered "not decided yet." Nothing in this design depends on it.
- **The full content-type list** beyond the five item kinds above.
- **Client build tooling and state approach**, beyond the choice of a browser client talking to this API.
- **Which English translation** to import.
