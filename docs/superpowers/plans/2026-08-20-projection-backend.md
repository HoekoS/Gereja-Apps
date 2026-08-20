# Church Projection Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the offline LAN server that stores the church's Bible translations, songs, slides, media and service orders, and drives the projection output as the single authority on what is live.

**Architecture:** Onion layers — `Domain` (pure rules, no dependencies), `Application` (use cases and ports), `Infrastructure` (EF Core/SQLite, Redis, import parsers), `Api` (ASP.NET Core minimal APIs plus one SignalR hub). Every read and write crosses a repository port; live state lives in one server-side aggregate and is broadcast whole, never as deltas.

**Tech Stack:** .NET 10, C#, ASP.NET Core minimal APIs, SignalR, EF Core 10 + SQLite (FTS5 for search), Redis (cache only, optional), xUnit v3, Bruno for API tests, Docker for dev/CI only.

**Spec:** `docs/superpowers/specs/2026-08-20-projection-backend-design.md`

**Supporting documents the executor must have open:**
- `docs/requirements/API-CONTRACT.md` — authoritative HTTP surface. Where a task and the contract disagree, the contract wins.
- `docs/requirements/SRS.md`, `docs/requirements/URS.md` — requirement IDs quoted in test names.
- `docs/testing/TEST-CASES.md`, `docs/testing/TEST-PLAN.md` — the cases each task turns green.

**The tests already exist and are red.** This is not a plan that writes tests from scratch. `tests/` holds a complete xUnit and Bruno suite written before any implementation; the unit tests fail to compile because no production type exists, and the Bruno suite fails on connection refused. Every task below names the exact tests it turns green. Where a task needs a test that does not exist yet, the task writes it first — those are called out explicitly.

## Global Constraints

- Target framework `net10.0`; `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true` — already set in `Directory.Build.props`. Do not weaken them; fix the warning instead.
- Package versions come from `Directory.Packages.props` (central package management). Add a package by adding a `PackageVersion` there and a bare `PackageReference` in the project.
- Dependency direction is one-way: `Domain` references nothing, `Application` references `Domain`, `Infrastructure` references `Application`, `Api` references `Application` and `Infrastructure`. A reference in the other direction is a build failure, not a discussion.
- No EF Core attributes, `DbContext`, or `IQueryable` outside `Infrastructure`. Repositories return Domain objects or plain result records, fully materialised.
- FTS5 is reachable only from `VerseRepository.SearchAsync` and `SongRepository.SearchAsync`. The user's query string is always a SQL parameter, never concatenated.
- The cache must never be a precondition. An absent, misconfigured, or unreachable Redis must not stop startup and must not fail a request (NFR-REL-09).
- `Access:TestPin` and `Access:RequirePairingFromLoopback` are test-only settings and are refused when the environment is `Production`.
- Refusals are values, not exceptions. `LiveSession` returns a `LiveResult` carrying a `RefusalCode`; the Api maps it to a status code.
- Live state is broadcast whole. No deltas, ever.
- Commit after every task. Conventional commit prefixes (`feat:`, `test:`, `chore:`, `fix:`).

## File Structure

```
src/
  ChurchProjection.Domain/
    Bible/BibleReference.cs          reference parsing, canonical book table
    Bible/BookNames.cs               id <-> name aliases, Indonesian + English
    Bible/Passage.cs                 a resolved run of verses
    Access/Pin.cs                    six-digit PIN value type + generation
    Access/PinRotation.cs            "is it past the most recent Saturday" rule
    Live/ItemId.cs                   service item identifier
    Live/Slot.cs                     what is on a screen: item + page + media flag
    Live/LiveSession.cs              the live aggregate — all state transitions
    Live/LiveSnapshot.cs             serialisable state of the aggregate
    Live/LiveResult.cs               ok-or-refusal result value
    Live/RefusalCode.cs              the five refusals
    Live/IServiceOrder.cs            what the aggregate may ask about a service
    Library/Song.cs, SongPage.cs, MediaItem.cs, Translation.cs, Verse.cs
    Services/ServicePlan.cs          service aggregate; owns item ordering
    Services/ServiceItem.cs, ItemRef.cs
  ChurchProjection.Application/
    Ports/ITranslationRepository.cs  one file per port
    Ports/IVerseRepository.cs
    Ports/ISongRepository.cs
    Ports/IMediaRepository.cs
    Ports/IServiceRepository.cs
    Ports/ILiveStateRepository.cs
    Ports/ISettingsRepository.cs
    Ports/IUnitOfWork.cs
    Import/ImportPayload.cs          parsed-but-not-yet-stored import
    Import/IImportParser.cs, IImportReader.cs, ImportException.cs
    Import/ImportLibrary.cs          the use case: parse, then write in one transaction
    Live/LiveCommand.cs              the command union
    Live/LiveCommandHandler.cs       load, apply, persist, return
    Live/ServiceOrderView.cs         IServiceOrder over a loaded ServicePlan
    Live/EmptyOrder.cs               the order when no service is attached
    Live/ContentResolver.cs          item + page -> the words the screen paints
  ChurchProjection.Infrastructure/
    Persistence/ProjectionDbContext.cs
    Persistence/Configurations/*.cs  one IEntityTypeConfiguration per entity
    Persistence/Migrations/          EF migrations, including the raw-SQL FTS5 one
    Persistence/UnitOfWork.cs
    Repositories/*.cs                the EF adapters, one per port
    Caching/CachedVerseRepository.cs decorator over IVerseRepository
    Import/ImportService.cs          IImportReader; owns parser selection
    Import/PlainTextSongParser.cs, OpenLyricsParser.cs, ZefaniaBibleParser.cs
    Persistence/DevSeed.cs           the API-test seed described in TEST-PLAN 4.1
  ChurchProjection.Api/
    Program.cs                       host wiring and the pipeline
    CompositionRoot.cs               AddProjection — the one DI entry point
    Options/StorageOptions.cs, CacheOptions.cs, AccessOptions.cs
    ApiError.cs                      the { error: { code, message } } envelope
    Contracts/*.cs                   response records — the JSON shapes
    Endpoints/AccessEndpoints.cs     pair, pin
    Endpoints/BibleEndpoints.cs      translations, reference, passage, search
    Endpoints/SongEndpoints.cs
    Endpoints/MediaEndpoints.cs
    Endpoints/ImportEndpoints.cs
    Endpoints/ServiceEndpoints.cs
    Endpoints/LiveEndpoints.cs
    Live/LiveHub.cs                  the SignalR hub
    Live/LiveStateDto.cs             the wire shape the hub broadcasts
    Live/OutputCounter.cs            how many output screens are connected
    Access/PairGate.cs               the pair-or-loopback authorization filter
    Access/PinService.cs             generates and rotates the shared PIN
    Access/PairTicket.cs             the Data Protection pairing cookie
    Media/MediaPaths.cs              resolve and contain every media path
tests/                               already written; only the two files named in
                                     Task 7 and Task 9 are added by this plan
```

**Why files split this way.** Endpoints are grouped by the resource they serve because those are the ones that change together when a contract row changes. The live aggregate is alone in its own file because it is the only place in the system where a bug is visible to the congregation, and it must be readable in one screen. Repositories are one-per-port so that a query change touches one file.

---

### Task 1: Repository, solution scaffold, and a health endpoint

There is no git repository and no source project yet — only tests, docs, and build configuration. This task makes the tree buildable and turns the first two tests green.

**Files:**
- Create: `.gitignore`
- Create: `src/ChurchProjection.Domain/ChurchProjection.Domain.csproj`
- Create: `src/ChurchProjection.Application/ChurchProjection.Application.csproj`
- Create: `src/ChurchProjection.Infrastructure/ChurchProjection.Infrastructure.csproj`
- Create: `src/ChurchProjection.Api/ChurchProjection.Api.csproj`
- Create: `src/ChurchProjection.Api/Program.cs`
- Test: `tests/ChurchProjection.Api.Tests/LiveBroadcastTests.cs` (already written — INT_13), `tests/api/01-health/health.bru` (already written — SYS-HLT-01)

**Interfaces:**
- Consumes: nothing.
- Produces: a `public partial class Program` in the global namespace, which `ProjectionAppFactory : WebApplicationFactory<Program>` binds to. Every later Api task adds to `Program.cs`.

- [ ] **Step 1: Initialise the repository**

```bash
git init
git symbolic-ref HEAD refs/heads/main
```

- [ ] **Step 2: Write `.gitignore`**

```gitignore
bin/
obj/
node_modules/
*.user
.vs/
artifacts/
TestResults/
*.db
*.db-shm
*.db-wal
```

- [ ] **Step 3: Commit what already exists**

```bash
git add .
git commit -m "chore: initial commit of requirements, tests, and build configuration"
```

- [ ] **Step 4: Confirm the red state**

Run: `dotnet build ChurchProjection.slnx`
Expected: FAIL — the solution lists projects under `src/` that do not exist.

- [ ] **Step 5: Create the four projects**

```bash
dotnet new classlib -n ChurchProjection.Domain         -o src/ChurchProjection.Domain
dotnet new classlib -n ChurchProjection.Application    -o src/ChurchProjection.Application
dotnet new classlib -n ChurchProjection.Infrastructure -o src/ChurchProjection.Infrastructure
dotnet new web      -n ChurchProjection.Api            -o src/ChurchProjection.Api
rm src/ChurchProjection.Domain/Class1.cs src/ChurchProjection.Application/Class1.cs src/ChurchProjection.Infrastructure/Class1.cs
```

`Directory.Build.props` already supplies `TargetFramework`, `Nullable`, and the rest. Delete those properties from each generated `.csproj` so there is one source of truth; each `.csproj` should end up holding only its `Sdk` attribute and its references.

- [ ] **Step 6: Wire the dependency direction**

```bash
dotnet add src/ChurchProjection.Application    reference src/ChurchProjection.Domain
dotnet add src/ChurchProjection.Infrastructure reference src/ChurchProjection.Application
dotnet add src/ChurchProjection.Api            reference src/ChurchProjection.Application
dotnet add src/ChurchProjection.Api            reference src/ChurchProjection.Infrastructure
```

`Api` references `Infrastructure` for one reason only: composing the DI container in `Program.cs`. No endpoint file may `using ChurchProjection.Infrastructure`.

- [ ] **Step 7: Write `Program.cs`**

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/healthz", () => Results.Json(new
{
    ok = true,
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
}));

app.Run();

// ProjectionAppFactory is a WebApplicationFactory<Program>, and top-level
// statements generate an internal Program. This makes it visible to the test
// project without an InternalsVisibleTo.
public partial class Program;
```

- [ ] **Step 8: Run the integration test that only needs a host**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter INT_13`
Expected: PASS. `ProjectionAppFactory` leaves `Cache:Redis:ConnectionString` null on purpose; a host that starts without it is the whole assertion (NFR-REL-09).

- [ ] **Step 9: Run the health check over real HTTP**

```bash
dotnet run --project src/ChurchProjection.Api &
npx @usebruno/cli run tests/api/01-health --env local
```

Expected: SYS-HLT-01 passes — 200 and `{ "ok": true }`. Stop the server afterwards.

- [ ] **Step 10: Commit**

```bash
git add .gitignore src ChurchProjection.slnx
git commit -m "feat: scaffold onion projects and health endpoint"
```

---

### Task 2: Domain — Bible reference parsing

`BibleReferenceTests` cannot compile until every Domain type the test project touches exists, so this task creates the whole Domain public surface and implements one part of it. The unimplemented parts throw, which is the correct red: an assertion failure rather than a compile failure.

**Files:**
- Create: `src/ChurchProjection.Domain/Bible/BookNames.cs`
- Create: `src/ChurchProjection.Domain/Bible/BibleReference.cs`
- Create: `src/ChurchProjection.Domain/Access/Pin.cs` (stub)
- Create: `src/ChurchProjection.Domain/Access/PinRotation.cs` (stub)
- Create: `src/ChurchProjection.Domain/Live/ItemId.cs`, `Slot.cs`, `RefusalCode.cs`, `LiveResult.cs`, `LiveSnapshot.cs`, `IServiceOrder.cs`, `LiveSession.cs` (stub)
- Test: `tests/ChurchProjection.Domain.Tests/BibleReferenceTests.cs` (already written)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `ChurchProjection.Domain.Bible.BibleReference` — `sealed record BibleReference(int BookId, int Chapter, int VerseStart, int? VerseEnd)` with `static BibleReference? TryParse(string? input)`.
  - `ChurchProjection.Domain.Bible.BookNames` — `static bool TryResolve(string name, out int bookId)` and `static string? Name(int bookId)`.
  - The `ChurchProjection.Domain.Live` types listed above, whose behaviour Task 4 fills in.

- [ ] **Step 1: Read the failing test**

The test is already written at `tests/ChurchProjection.Domain.Tests/BibleReferenceTests.cs`. Read it in full before writing code. It pins these behaviours:

```csharp
Assert.Equal(new BibleReference(John, 3, 16, 16), BibleReference.TryParse("Yohanes 3:16"));
Assert.Equal(new BibleReference(John, 3, 16, 16), BibleReference.TryParse("yoh 3:16"));
Assert.Equal(new BibleReference(FirstCorinthians, 13, 4, 7), BibleReference.TryParse("1 Korintus 13:4-7"));
Assert.Equal(new BibleReference(FirstCorinthians, 13, 4, 4), BibleReference.TryParse("1Kor 13:4"));

var chapterOnly = BibleReference.TryParse("Mazmur 23")!;
Assert.Equal(1, chapterOnly.VerseStart);
Assert.Null(chapterOnly.VerseEnd);

Assert.Null(BibleReference.TryParse("asdf"));
Assert.Null(BibleReference.TryParse(""));
Assert.Null(BibleReference.TryParse("   "));
Assert.Null(BibleReference.TryParse(null));
Assert.Null(BibleReference.TryParse("Kejadian 1:5-1"));   // reversed range
Assert.Null(Record.Exception(() => BibleReference.TryParse("Tobit 1:1")));
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test tests/ChurchProjection.Domain.Tests`
Expected: FAIL to build — `The type or namespace name 'BibleReference' could not be found`, plus the same for `LiveSession`, `Pin`, and friends. That is the compile-unit reality of C#: the whole test project must resolve before any test in it runs.

- [ ] **Step 3: Write the book table**

```csharp
namespace ChurchProjection.Domain.Bible;

/// <summary>
/// Canonical book ids with the Indonesian and English names an operator
/// actually types. Ids follow the usual Protestant order, but nothing here
/// enforces 1..66 — a deuterocanonical import may carry higher ids, and this
/// table simply will not resolve their names.
/// </summary>
public static class BookNames
{
    // id | Indonesian name | Indonesian abbreviation | English name | English abbreviation
    private static readonly string[] Table =
    [
        "1|Kejadian|Kej|Genesis|Gen",
        "2|Keluaran|Kel|Exodus|Exod",
        "3|Imamat|Im|Leviticus|Lev",
        "4|Bilangan|Bil|Numbers|Num",
        "5|Ulangan|Ul|Deuteronomy|Deut",
        "6|Yosua|Yos|Joshua|Josh",
        "7|Hakim-Hakim|Hak|Judges|Judg",
        "8|Rut|Rut|Ruth|Ruth",
        "9|1 Samuel|1Sam|1 Samuel|1Sam",
        "10|2 Samuel|2Sam|2 Samuel|2Sam",
        "11|1 Raja-Raja|1Raj|1 Kings|1Kgs",
        "12|2 Raja-Raja|2Raj|2 Kings|2Kgs",
        "13|1 Tawarikh|1Taw|1 Chronicles|1Chr",
        "14|2 Tawarikh|2Taw|2 Chronicles|2Chr",
        "15|Ezra|Ezr|Ezra|Ezra",
        "16|Nehemia|Neh|Nehemiah|Neh",
        "17|Ester|Est|Esther|Esth",
        "18|Ayub|Ayb|Job|Job",
        "19|Mazmur|Mzm|Psalms|Ps",
        "20|Amsal|Ams|Proverbs|Prov",
        "21|Pengkhotbah|Pkh|Ecclesiastes|Eccl",
        "22|Kidung Agung|Kid|Song of Songs|Song",
        "23|Yesaya|Yes|Isaiah|Isa",
        "24|Yeremia|Yer|Jeremiah|Jer",
        "25|Ratapan|Rat|Lamentations|Lam",
        "26|Yehezkiel|Yeh|Ezekiel|Ezek",
        "27|Daniel|Dan|Daniel|Dan",
        "28|Hosea|Hos|Hosea|Hos",
        "29|Yoel|Yl|Joel|Joel",
        "30|Amos|Am|Amos|Amos",
        "31|Obaja|Ob|Obadiah|Obad",
        "32|Yunus|Yun|Jonah|Jonah",
        "33|Mikha|Mi|Micah|Mic",
        "34|Nahum|Nah|Nahum|Nah",
        "35|Habakuk|Hab|Habakkuk|Hab",
        "36|Zefanya|Zef|Zephaniah|Zeph",
        "37|Hagai|Hag|Haggai|Hag",
        "38|Zakharia|Za|Zechariah|Zech",
        "39|Maleakhi|Mal|Malachi|Mal",
        "40|Matius|Mat|Matthew|Matt",
        "41|Markus|Mrk|Mark|Mark",
        "42|Lukas|Luk|Luke|Luke",
        "43|Yohanes|Yoh|John|John",
        "44|Kisah Para Rasul|Kis|Acts|Acts",
        "45|Roma|Rm|Romans|Rom",
        "46|1 Korintus|1Kor|1 Corinthians|1Cor",
        "47|2 Korintus|2Kor|2 Corinthians|2Cor",
        "48|Galatia|Gal|Galatians|Gal",
        "49|Efesus|Ef|Ephesians|Eph",
        "50|Filipi|Flp|Philippians|Phil",
        "51|Kolose|Kol|Colossians|Col",
        "52|1 Tesalonika|1Tes|1 Thessalonians|1Thess",
        "53|2 Tesalonika|2Tes|2 Thessalonians|2Thess",
        "54|1 Timotius|1Tim|1 Timothy|1Tim",
        "55|2 Timotius|2Tim|2 Timothy|2Tim",
        "56|Titus|Tit|Titus|Titus",
        "57|Filemon|Flm|Philemon|Phlm",
        "58|Ibrani|Ibr|Hebrews|Heb",
        "59|Yakobus|Yak|James|Jas",
        "60|1 Petrus|1Ptr|1 Peter|1Pet",
        "61|2 Petrus|2Ptr|2 Peter|2Pet",
        "62|1 Yohanes|1Yoh|1 John|1John",
        "63|2 Yohanes|2Yoh|2 John|2John",
        "64|3 Yohanes|3Yoh|3 John|3John",
        "65|Yudas|Yud|Jude|Jude",
        "66|Wahyu|Why|Revelation|Rev",
    ];

    private static readonly Dictionary<string, int> Aliases = BuildAliases();
    private static readonly Dictionary<int, string> Canonical = BuildCanonical();

    /// <summary>Resolves any spelling in the table to its book id.</summary>
    public static bool TryResolve(string name, out int bookId) =>
        Aliases.TryGetValue(Normalise(name), out bookId);

    /// <summary>The Indonesian display name, or null for an id not in the table.</summary>
    public static string? Name(int bookId) =>
        Canonical.TryGetValue(bookId, out var name) ? name : null;

    /// <summary>
    /// Lowercases and strips whitespace and periods, so "1 Korintus",
    /// "1korintus" and "1 Kor." all collapse to one key.
    /// </summary>
    private static string Normalise(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        var length = 0;

        foreach (var c in name)
        {
            if (char.IsWhiteSpace(c) || c is '.')
            {
                continue;
            }

            buffer[length++] = char.ToLowerInvariant(c);
        }

        return new string(buffer[..length]);
    }

    private static Dictionary<string, int> BuildAliases()
    {
        var aliases = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in Table)
        {
            var parts = row.Split('|');
            var id = int.Parse(parts[0]);

            for (var i = 1; i < parts.Length; i++)
            {
                aliases[Normalise(parts[i])] = id;
            }
        }

        return aliases;
    }

    private static Dictionary<int, string> BuildCanonical() =>
        Table.Select(row => row.Split('|'))
             .ToDictionary(parts => int.Parse(parts[0]), parts => parts[1]);
}
```

- [ ] **Step 4: Write the parser**

```csharp
using System.Text.RegularExpressions;

namespace ChurchProjection.Domain.Bible;

/// <summary>
/// A place in the Bible, independent of translation. VerseEnd is null for a
/// whole chapter and equal to VerseStart for a single verse — the operator
/// asked for one verse, not for a range that happens to be one long.
/// </summary>
public sealed partial record BibleReference(int BookId, int Chapter, int VerseStart, int? VerseEnd)
{
    [GeneratedRegex(
        @"^(?<book>.+?)\s*(?<chapter>\d+)(?::(?<start>\d+)(?:\s*-\s*(?<end>\d+))?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>
    /// Parses free-form operator input. Returns null for anything it does not
    /// understand — an unparseable reference is an ordinary outcome of typing,
    /// not an exceptional condition.
    /// </summary>
    public static BibleReference? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = Pattern().Match(input.Trim());

        if (!match.Success || !BookNames.TryResolve(match.Groups["book"].Value, out var bookId))
        {
            return null;
        }

        var chapter = int.Parse(match.Groups["chapter"].Value);

        if (chapter < 1)
        {
            return null;
        }

        if (!match.Groups["start"].Success)
        {
            return new BibleReference(bookId, chapter, 1, null);
        }

        var start = int.Parse(match.Groups["start"].Value);
        var end = match.Groups["end"].Success ? int.Parse(match.Groups["end"].Value) : start;

        if (start < 1 || end < start)
        {
            return null;
        }

        return new BibleReference(bookId, chapter, start, end);
    }
}
```

The book group is lazy and the chapter group is anchored to the end, so `1 Korintus 13:4-7` backtracks until the book is `1 Korintus` rather than stopping at the leading `1`.

- [ ] **Step 5: Write the Domain types the test project needs in order to compile**

These are stubs on purpose. Task 3 fills `Access`, Task 4 fills `Live`.

```csharp
// src/ChurchProjection.Domain/Access/Pin.cs
namespace ChurchProjection.Domain.Access;

public readonly record struct Pin(string Value)
{
    public static Pin Generate() => throw new NotImplementedException();
}
```

```csharp
// src/ChurchProjection.Domain/Access/PinRotation.cs
namespace ChurchProjection.Domain.Access;

public static class PinRotation
{
    public static bool ShouldRotate(DateTime lastRotatedAt, DateTime now) =>
        throw new NotImplementedException();
}
```

```csharp
// src/ChurchProjection.Domain/Live/ItemId.cs
namespace ChurchProjection.Domain.Live;

public readonly record struct ItemId(string Value)
{
    public static implicit operator ItemId(string value) => new(value);

    public override string ToString() => Value;
}
```

```csharp
// src/ChurchProjection.Domain/Live/Slot.cs
namespace ChurchProjection.Domain.Live;

/// <summary>
/// What is on a screen. MediaAvailable is captured when the slot is staged, so
/// Go() can refuse without asking the service order again.
/// </summary>
public sealed record Slot(ItemId ItemId, int PageIndex, bool MediaAvailable);
```

```csharp
// src/ChurchProjection.Domain/Live/RefusalCode.cs
namespace ChurchProjection.Domain.Live;

public enum RefusalCode
{
    None = 0,
    NoPreview,
    NoLiveItem,
    MediaUnavailable,
    UnknownItem,
    PageOutOfRange,
}
```

```csharp
// src/ChurchProjection.Domain/Live/LiveResult.cs
namespace ChurchProjection.Domain.Live;

/// <summary>
/// A refusal is a value, not an exception. The operator pressing a key that
/// cannot apply right now is normal, and normal control flow must not unwind
/// the stack in front of a congregation.
/// </summary>
public readonly record struct LiveResult(RefusalCode Refusal)
{
    public bool IsOk => Refusal == RefusalCode.None;

    public static LiveResult Ok { get; } = new(RefusalCode.None);

    public static LiveResult Refuse(RefusalCode code) => new(code);
}
```

```csharp
// src/ChurchProjection.Domain/Live/LiveSnapshot.cs
namespace ChurchProjection.Domain.Live;

/// <summary>
/// The whole live state, flat and copyable. This is what gets persisted and
/// what gets broadcast; there is no delta form anywhere in the system.
/// </summary>
public sealed record LiveSnapshot(
    Slot? Live,
    Slot? Preview,
    bool Blackout,
    IReadOnlyList<ItemId> Skipped,
    string? ServiceId);
```

```csharp
// src/ChurchProjection.Domain/Live/IServiceOrder.cs
namespace ChurchProjection.Domain.Live;

/// <summary>
/// Everything the live aggregate is allowed to know about a service. Three
/// members, deliberately: widen this and the aggregate starts making decisions
/// that belong to the library.
/// </summary>
public interface IServiceOrder
{
    bool Contains(ItemId id);

    int PageCount(ItemId id);

    bool MediaAvailable(ItemId id);
}
```

```csharp
// src/ChurchProjection.Domain/Live/LiveSession.cs
namespace ChurchProjection.Domain.Live;

public sealed class LiveSession
{
    public Slot? Live { get; private set; }

    public Slot? Preview { get; private set; }

    public bool Blackout { get; private set; }

    public IReadOnlyCollection<ItemId> Skipped => throw new NotImplementedException();

    public static LiveSession New() => throw new NotImplementedException();

    public LiveSnapshot Snapshot() => throw new NotImplementedException();

    public LiveResult PreviewItem(ItemId id, int pageIndex, IServiceOrder order) =>
        throw new NotImplementedException();

    public LiveResult Go() => throw new NotImplementedException();

    public LiveResult Advance(IServiceOrder order) => throw new NotImplementedException();

    public LiveResult Back() => throw new NotImplementedException();

    public LiveResult SetBlackout(bool on) => throw new NotImplementedException();

    public LiveResult Clear() => throw new NotImplementedException();

    public LiveResult Skip(ItemId id, IServiceOrder order) => throw new NotImplementedException();

    public LiveResult Unskip(ItemId id) => throw new NotImplementedException();
}
```

- [ ] **Step 6: Run the reference tests**

Run: `dotnet test tests/ChurchProjection.Domain.Tests --filter UNT_REF`
Expected: PASS, 13 tests. The `UNT_LIV` and `UNT_PIN` tests in the same project now fail with `NotImplementedException` — the correct red for Tasks 3 and 4.

- [ ] **Step 7: Commit**

```bash
git add src/ChurchProjection.Domain
git commit -m "feat: parse free-form Bible references in Indonesian and English"
```

---

### Task 3: Domain — PIN generation and Saturday rotation

**Files:**
- Modify: `src/ChurchProjection.Domain/Access/Pin.cs`, `src/ChurchProjection.Domain/Access/PinRotation.cs`
- Test: `tests/ChurchProjection.Domain.Tests/PinRotationTests.cs` (already written)

**Interfaces:**
- Consumes: the stubs from Task 2.
- Produces:
  - `Pin.Generate() -> Pin`, `Pin.Value -> string` (six digits).
  - `PinRotation.ShouldRotate(DateTime lastRotatedAt, DateTime now) -> bool`.

- [ ] **Step 1: Run the failing tests**

Run: `dotnet test tests/ChurchProjection.Domain.Tests --filter UNT_PIN`
Expected: FAIL, 11 tests, `System.NotImplementedException`.

The tests pin these boundaries. Every timestamp is `DateTimeKind.Unspecified` — the rule is about the wall clock in the booth:

| Last rotated | Now | Rotate? |
|---|---|---|
| Fri 23:59 | Sat 00:00 | yes |
| Sat 00:00 | Sat 00:00 | no |
| Sat Aug 15 00:00 | Fri Aug 21 23:59 | no |
| Sat Aug 15 00:00 | Sat Aug 22 00:01 | yes |
| Sat 18:00 | Sun 09:30 | no |
| Sat Jul 25 00:00 | Wed Aug 19 | yes, once |

- [ ] **Step 2: Implement the PIN**

```csharp
using System.Security.Cryptography;

namespace ChurchProjection.Domain.Access;

/// <summary>
/// The shared six-digit PIN. Short enough to read off a card taped to the
/// booth desk, which is exactly why it rotates weekly and why pairing is rate
/// limited.
/// </summary>
public readonly record struct Pin(string Value)
{
    public static Pin Generate() =>
        new(RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"));
}
```

`RandomNumberGenerator`, not `Random`. A predictable PIN is not a PIN.

- [ ] **Step 3: Implement the rotation rule**

```csharp
namespace ChurchProjection.Domain.Access;

/// <summary>
/// The PIN is good for one week and turns over at Saturday midnight, local
/// wall clock, so the number handed out at Saturday rehearsal is the number
/// that works on Sunday morning.
/// </summary>
public static class PinRotation
{
    /// <summary>
    /// True when a Saturday midnight has passed since the PIN was last set.
    /// Evaluated on demand: nothing schedules this, so a server that was off
    /// for a month rotates exactly once when it comes back up.
    /// </summary>
    public static bool ShouldRotate(DateTime lastRotatedAt, DateTime now) =>
        MostRecentSaturdayMidnight(now) > lastRotatedAt;

    private static DateTime MostRecentSaturdayMidnight(DateTime now)
    {
        var daysSinceSaturday = ((int)now.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;

        return now.Date.AddDays(-daysSinceSaturday);
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/ChurchProjection.Domain.Tests --filter UNT_PIN`
Expected: PASS, 11 tests. UNT-PIN-01 draws 1000 PINs and requires at least 900 distinct values; if that fails, the generator is seeded wrong.

- [ ] **Step 5: Commit**

```bash
git add src/ChurchProjection.Domain/Access
git commit -m "feat: generate six-digit PINs and rotate them each Saturday"
```

---

### Task 4: Domain — the live aggregate

This is the class the congregation sees. Every transition is here, and nothing outside it may set `Live` or `Preview`.

**Files:**
- Modify: `src/ChurchProjection.Domain/Live/LiveSession.cs`
- Test: `tests/ChurchProjection.Domain.Tests/LiveSessionTests.cs` (already written, 20 tests)

**Interfaces:**
- Consumes: `ItemId`, `Slot`, `LiveResult`, `RefusalCode`, `LiveSnapshot`, `IServiceOrder` from Task 2.
- Produces:
  - `LiveSession.New() -> LiveSession`
  - `LiveSession.Restore(LiveSnapshot snapshot) -> LiveSession`
  - `session.Snapshot() -> LiveSnapshot`
  - `session.AttachService(string? serviceId)` — sets which service the snapshot names
  - the eight command methods, each returning `LiveResult`

- [ ] **Step 1: Run the failing tests**

Run: `dotnet test tests/ChurchProjection.Domain.Tests --filter UNT_LIV`
Expected: FAIL, 20 tests, `System.NotImplementedException`.

Read `LiveSessionTests.cs` in full first. Two things in it drive the design and are easy to miss:

1. `Go()` takes no `IServiceOrder`, yet UNT-LIV-13 expects `Go()` to refuse with `MediaUnavailable`. Availability is therefore captured on the `Slot` when the item is staged, not looked up at go time.
2. UNT-LIV-19 asserts the exact member list of `IServiceOrder` by reflection:

```csharp
var members = typeof(IServiceOrder).GetMembers().Select(m => m.Name).Order().ToArray();
Assert.Equal(new[] { "Contains", "MediaAvailable", "PageCount" }, members);
```

Adding a fourth member to that interface breaks the test on purpose. It is a guard against the aggregate growing an appetite for the library.

The fixture is a three-item order: `A` with 1 page, `B` with 4 pages, `C` with 1 page and a missing media file.

- [ ] **Step 2: Implement the aggregate**

```csharp
namespace ChurchProjection.Domain.Live;

/// <summary>
/// The single authority on what is on the screen. Held in memory by the
/// server, persisted after every change, and broadcast whole. Refusals are
/// returned, never thrown: an operator pressing a key that cannot apply right
/// now is an ordinary Sunday, not an error condition.
/// </summary>
public sealed class LiveSession
{
    private readonly List<ItemId> _skipped = [];

    public Slot? Live { get; private set; }

    public Slot? Preview { get; private set; }

    public bool Blackout { get; private set; }

    public string? ServiceId { get; private set; }

    public IReadOnlyCollection<ItemId> Skipped => _skipped;

    public static LiveSession New() => new();

    public static LiveSession Restore(LiveSnapshot snapshot)
    {
        var session = new LiveSession
        {
            Live = snapshot.Live,
            Preview = snapshot.Preview,
            Blackout = snapshot.Blackout,
            ServiceId = snapshot.ServiceId,
        };

        session._skipped.AddRange(snapshot.Skipped);

        return session;
    }

    public void AttachService(string? serviceId) => ServiceId = serviceId;

    /// <summary>
    /// A copy. The skipped list is materialised rather than handed out live,
    /// so a snapshot taken before a command still reads the same afterwards.
    /// </summary>
    public LiveSnapshot Snapshot() =>
        new(Live, Preview, Blackout, [.. _skipped], ServiceId);

    public LiveResult PreviewItem(ItemId id, int pageIndex, IServiceOrder order)
    {
        if (!order.Contains(id))
        {
            return LiveResult.Refuse(RefusalCode.UnknownItem);
        }

        if (pageIndex < 0 || pageIndex >= order.PageCount(id))
        {
            // A control view that still thinks the song has six pages after it
            // was re-imported with four. Refuse rather than clamp: the operator
            // needs to know their screen is stale.
            return LiveResult.Refuse(RefusalCode.PageOutOfRange);
        }

        Preview = new Slot(id, pageIndex, order.MediaAvailable(id));

        return LiveResult.Ok;
    }

    public LiveResult Go()
    {
        if (Preview is not { } staged)
        {
            return LiveResult.Refuse(RefusalCode.NoPreview);
        }

        if (!staged.MediaAvailable)
        {
            return LiveResult.Refuse(RefusalCode.MediaUnavailable);
        }

        Live = staged;
        Preview = null;

        return LiveResult.Ok;
    }

    public LiveResult Advance(IServiceOrder order)
    {
        if (Live is not { } live)
        {
            return LiveResult.Refuse(RefusalCode.NoLiveItem);
        }

        var lastPage = order.PageCount(live.ItemId) - 1;

        // Holding on the last page is not an error. The operator holds the key
        // down at the end of a chorus; the screen must simply stay put.
        Live = live with { PageIndex = Math.Min(live.PageIndex + 1, lastPage) };

        return LiveResult.Ok;
    }

    public LiveResult Back()
    {
        if (Live is not { } live)
        {
            return LiveResult.Refuse(RefusalCode.NoLiveItem);
        }

        Live = live with { PageIndex = Math.Max(live.PageIndex - 1, 0) };

        return LiveResult.Ok;
    }

    public LiveResult SetBlackout(bool on)
    {
        Blackout = on;

        return LiveResult.Ok;
    }

    /// <summary>
    /// Clears what is live. Preview is left staged on purpose: clearing the
    /// screen is not the same as abandoning what comes next.
    /// </summary>
    public LiveResult Clear()
    {
        Live = null;

        return LiveResult.Ok;
    }

    public LiveResult Skip(ItemId id, IServiceOrder order)
    {
        if (!order.Contains(id))
        {
            return LiveResult.Refuse(RefusalCode.UnknownItem);
        }

        if (!_skipped.Contains(id))
        {
            _skipped.Add(id);
        }

        return LiveResult.Ok;
    }

    public LiveResult Unskip(ItemId id)
    {
        _skipped.Remove(id);

        return LiveResult.Ok;
    }
}
```

- [ ] **Step 3: Run the tests**

Run: `dotnet test tests/ChurchProjection.Domain.Tests`
Expected: PASS, all 44 tests in the project.

If UNT-LIV-18 (`refusals leave state untouched`) fails while the others pass, `Snapshot()` is handing out the live `_skipped` list instead of a copy.

- [ ] **Step 4: Commit**

```bash
git add src/ChurchProjection.Domain/Live
git commit -m "feat: add the live session aggregate with value-typed refusals"
```

---

### Task 5: Domain library types and Application ports

No behaviour, only shape: the entities the repositories return and the interfaces they are reached through. The deliverable is a build, and a reviewer's gate is whether these interfaces admit anything they should not.

**Files:**
- Create: `src/ChurchProjection.Domain/Library/Ids.cs`, `Translation.cs`, `Verse.cs`, `Passage.cs`, `Song.cs`, `SongPage.cs`, `MediaItem.cs`
- Create: `src/ChurchProjection.Domain/Services/ServicePlan.cs`, `ServiceItem.cs`, `ItemRef.cs`
- Create: `src/ChurchProjection.Application/Ports/ITranslationRepository.cs`, `IVerseRepository.cs`, `ISongRepository.cs`, `IMediaRepository.cs`, `IServiceRepository.cs`, `ILiveStateRepository.cs`, `ISettingsRepository.cs`, `IUnitOfWork.cs`
- Create: `src/ChurchProjection.Application/Import/ImportPayload.cs`, `IImportParser.cs`, `IImportReader.cs`, `ImportException.cs`

**Interfaces:**
- Consumes: `ItemId`, `LiveSnapshot` from Task 2.
- Produces: everything below. Tasks 6 to 17 consume these names exactly as written.

- [ ] **Step 1: Write the identifiers**

```csharp
// src/ChurchProjection.Domain/Library/Ids.cs
namespace ChurchProjection.Domain.Library;

public readonly record struct TranslationId(string Value)
{
    public static implicit operator TranslationId(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct SongId(string Value)
{
    public static implicit operator SongId(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct MediaId(string Value)
{
    public static implicit operator MediaId(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct ServiceId(string Value)
{
    public static implicit operator ServiceId(string value) => new(value);

    public override string ToString() => Value;
}
```

- [ ] **Step 2: Write the library entities**

```csharp
// src/ChurchProjection.Domain/Library/Translation.cs
namespace ChurchProjection.Domain.Library;

public sealed class Translation
{
    public required TranslationId Id { get; init; }

    public required string Abbrev { get; init; }

    public required string Name { get; init; }

    public required string Language { get; init; }
}
```

```csharp
// src/ChurchProjection.Domain/Library/Verse.cs
namespace ChurchProjection.Domain.Library;

public sealed class Verse
{
    public long Id { get; init; }

    public required TranslationId TranslationId { get; init; }

    public required int BookId { get; init; }

    public required int Chapter { get; init; }

    public required int Number { get; init; }

    public required string Text { get; init; }
}
```

```csharp
// src/ChurchProjection.Domain/Library/Passage.cs
namespace ChurchProjection.Domain.Library;

/// <summary>A resolved run of verses, ready to render. BookName is in the
/// translation's own language, which is why it travels with the passage
/// rather than being looked up by the client.</summary>
public sealed record Passage(
    TranslationId TranslationId,
    int BookId,
    string BookName,
    int Chapter,
    IReadOnlyList<Verse> Verses);
```

```csharp
// src/ChurchProjection.Domain/Library/Song.cs
namespace ChurchProjection.Domain.Library;

public sealed class Song
{
    public required SongId Id { get; set; }

    public required string Title { get; set; }

    public string? Author { get; set; }

    public string? Ccli { get; set; }

    public string? Language { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<SongPage> Pages { get; init; } = [];
}
```

```csharp
// src/ChurchProjection.Domain/Library/SongPage.cs
namespace ChurchProjection.Domain.Library;

/// <summary>One projected page. SectionLabel is free text because the church
/// writes "Reff", not "chorus".</summary>
public sealed class SongPage
{
    public required int Position { get; set; }

    public string? SectionLabel { get; set; }

    public required string Text { get; set; }
}
```

```csharp
// src/ChurchProjection.Domain/Library/MediaItem.cs
namespace ChurchProjection.Domain.Library;

public sealed class MediaItem
{
    public required MediaId Id { get; init; }

    public required string Kind { get; init; }          // image | video | audio

    public required string Filename { get; init; }

    public required string Path { get; init; }

    public int? DurationMs { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}
```

- [ ] **Step 3: Write the service aggregate**

`ServicePlan` owns item ordering. Nothing outside it renumbers positions, which is what keeps SYS-SVC-03 honest.

```csharp
// src/ChurchProjection.Domain/Services/ItemRef.cs
namespace ChurchProjection.Domain.Services;

/// <summary>
/// The kind-specific payload of a service item. One nullable-heavy record
/// rather than a class hierarchy: it is stored as a single JSON column and
/// every consumer switches on Kind anyway.
/// </summary>
public sealed record ItemRef
{
    public string? TranslationId { get; init; }

    public int? BookId { get; init; }

    public int? Chapter { get; init; }

    public int? VerseStart { get; init; }

    public int? VerseEnd { get; init; }

    public string? SongId { get; init; }

    public string? MediaId { get; init; }

    public string? Text { get; init; }

    public string? TargetTime { get; init; }
}
```

```csharp
// src/ChurchProjection.Domain/Services/ServiceItem.cs
namespace ChurchProjection.Domain.Services;

public sealed class ServiceItem
{
    public required string Id { get; init; }

    public required string Kind { get; set; }           // bible | song | slide | media | countdown

    public required string Label { get; set; }

    public required ItemRef Ref { get; set; }

    public int Position { get; set; }
}
```

```csharp
// src/ChurchProjection.Domain/Services/ServicePlan.cs
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Domain.Services;

public sealed class ServicePlan
{
    private readonly List<ServiceItem> _items = [];

    public required ServiceId Id { get; init; }

    public required string Name { get; set; }

    public required DateOnly ServiceDate { get; set; }

    /// <summary>Always in position order.</summary>
    public IReadOnlyList<ServiceItem> Items => _items;

    public void Load(IEnumerable<ServiceItem> items)
    {
        _items.Clear();
        _items.AddRange(items.OrderBy(item => item.Position));
        Renumber();
    }

    public ServiceItem Append(ServiceItem item)
    {
        _items.Add(item);
        Renumber();

        return item;
    }

    public bool Remove(string itemId)
    {
        var removed = _items.RemoveAll(item => item.Id == itemId) > 0;
        Renumber();

        return removed;
    }

    public ServiceItem? Find(string itemId) => _items.SingleOrDefault(item => item.Id == itemId);

    /// <summary>
    /// Reorders to exactly the given ids. Returns false and changes nothing
    /// unless the list is a permutation of the current items — a partial
    /// reorder would silently drop whatever the caller forgot.
    /// </summary>
    public bool Reorder(IReadOnlyList<string> itemIds)
    {
        if (itemIds.Count != _items.Count || itemIds.Distinct().Count() != itemIds.Count)
        {
            return false;
        }

        var byId = _items.ToDictionary(item => item.Id);

        if (!itemIds.All(id => byId.ContainsKey(id)))
        {
            return false;
        }

        var reordered = itemIds.Select(id => byId[id]).ToList();

        _items.Clear();
        _items.AddRange(reordered);
        Renumber();

        return true;
    }

    private void Renumber()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].Position = i;
        }
    }
}
```

- [ ] **Step 4: Write the ports**

Each interface lists the queries that are actually called, and nothing else.

```csharp
// src/ChurchProjection.Application/Ports/ITranslationRepository.cs
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

public interface ITranslationRepository
{
    Task<IReadOnlyList<Translation>> ListAsync(CancellationToken ct);

    Task<Translation?> FindAsync(TranslationId id, CancellationToken ct);
}
```

```csharp
// src/ChurchProjection.Application/Ports/IVerseRepository.cs
using ChurchProjection.Application.Import;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

/// <summary>A search hit. Flat and already carrying its book name, because the
/// client renders the list without a second round trip.</summary>
public sealed record VerseHit(
    string TranslationId,
    int BookId,
    string BookName,
    int Chapter,
    int Verse,
    string Text);

public interface IVerseRepository
{
    Task<Passage?> GetAsync(TranslationId translation, BibleReference reference, CancellationToken ct);

    /// <summary>Full-text search. <paramref name="translation"/> null searches every translation.</summary>
    Task<IReadOnlyList<VerseHit>> SearchAsync(TranslationId? translation, string query, int limit, CancellationToken ct);

    /// <summary>Replaces a whole translation. Individual verses are never
    /// written or deleted; a Bible is imported or it is not.</summary>
    Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct);
}
```

```csharp
// src/ChurchProjection.Application/Ports/ISongRepository.cs
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

public sealed record SongHit(string Id, string Title, string? Author, string? Language);

public interface ISongRepository
{
    Task<Song?> FindAsync(SongId id, CancellationToken ct);

    Task<Song?> FindByTitleAsync(string title, CancellationToken ct);

    /// <summary>Title and lyric search. An empty query lists everything.</summary>
    Task<IReadOnlyList<SongHit>> SearchAsync(string query, int limit, CancellationToken ct);

    Task<SongId> UpsertAsync(Song song, CancellationToken ct);
}
```

```csharp
// src/ChurchProjection.Application/Ports/IMediaRepository.cs
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Ports;

public interface IMediaRepository
{
    Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct);

    Task<MediaItem?> FindAsync(MediaId id, CancellationToken ct);

    Task<MediaId> AddAsync(MediaItem item, CancellationToken ct);

    Task RemoveAsync(MediaId id, CancellationToken ct);
}
```

```csharp
// src/ChurchProjection.Application/Ports/IServiceRepository.cs
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Ports;

public sealed record ServiceSummary(string Id, string Name, DateOnly ServiceDate, int ItemCount);

public interface IServiceRepository
{
    Task<ServicePlan?> FindAsync(ServiceId id, CancellationToken ct);

    Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken ct);

    /// <summary>Saves the whole aggregate, items included. Positions are the
    /// aggregate's business, so there is no per-item write.</summary>
    Task SaveAsync(ServicePlan plan, CancellationToken ct);

    Task RemoveAsync(ServiceId id, CancellationToken ct);
}
```

```csharp
// src/ChurchProjection.Application/Ports/ILiveStateRepository.cs
using ChurchProjection.Domain.Live;

namespace ChurchProjection.Application.Ports;

public interface ILiveStateRepository
{
    Task<LiveSnapshot?> LoadAsync(CancellationToken ct);

    Task SaveAsync(LiveSnapshot snapshot, CancellationToken ct);
}
```

```csharp
// src/ChurchProjection.Application/Ports/ISettingsRepository.cs
namespace ChurchProjection.Application.Ports;

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct);

    Task SetAsync(string key, string value, CancellationToken ct);
}
```

```csharp
// src/ChurchProjection.Application/Ports/IUnitOfWork.cs
namespace ChurchProjection.Application.Ports;

/// <summary>
/// A transaction belongs to the use case that needs one, not to a repository.
/// Only the import uses this: it is the only operation where a half-written
/// result is worse than no result.
/// </summary>
public interface IUnitOfWork
{
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
```

- [ ] **Step 5: Write the import contracts**

```csharp
// src/ChurchProjection.Application/Import/ImportPayload.cs
namespace ChurchProjection.Application.Import;

public enum ImportKind
{
    Bible,
    Song,
}

/// <summary>
/// A parsed file that has not been written yet. Parsing either produces a
/// complete payload or throws; nothing yields records one at a time, because a
/// stream that fails halfway is exactly the half-written import FR-IMP-07
/// forbids.
/// </summary>
public sealed record ImportPayload(
    ImportKind Kind,
    IReadOnlyList<ImportedSong> Songs,
    IReadOnlyList<ImportedVerse> Verses,
    ImportedTranslation? Translation);

public sealed record ImportedSong(
    string Title,
    string? Author,
    string? Ccli,
    string? Language,
    IReadOnlyList<ImportedPage> Pages);

public sealed record ImportedPage(int Position, string? SectionLabel, string Text);

public sealed record ImportedVerse(int BookId, int Chapter, int Verse, string Text);

public sealed record ImportedTranslation(
    string Id,
    string Abbrev,
    string Name,
    string Language,
    IReadOnlyList<ImportedBookName> Books);

public sealed record ImportedBookName(int BookId, string Name, string? Abbrev);
```

```csharp
// src/ChurchProjection.Application/Import/ImportException.cs
namespace ChurchProjection.Application.Import;

/// <summary>
/// Thrown when a file cannot be turned into a payload. Detail names the
/// offending record and is shown to the operator, so it must say which line or
/// which verse — "invalid file" tells a volunteer nothing.
/// </summary>
public sealed class ImportException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
```

```csharp
// src/ChurchProjection.Application/Import/IImportParser.cs
namespace ChurchProjection.Application.Import;

public interface IImportParser
{
    bool CanHandle(string fileName, ReadOnlySpan<byte> head);

    ImportPayload Parse(Stream input, string fileName);
}
```

```csharp
// src/ChurchProjection.Application/Import/IImportReader.cs
namespace ChurchProjection.Application.Import;

/// <summary>Picks a parser and runs it. Implemented in Infrastructure, where
/// the parsers live.</summary>
public interface IImportReader
{
    ImportPayload Parse(Stream input, string fileName);
}
```

- [ ] **Step 6: Build**

Run: `dotnet build src/ChurchProjection.Application`
Expected: SUCCESS with zero warnings. `TreatWarningsAsErrors` is on, so a missing `using` or an unused field fails the build.

- [ ] **Step 7: Check the dependency direction by hand**

Run: `grep -rn "using ChurchProjection.Infrastructure" src/ChurchProjection.Domain src/ChurchProjection.Application`
Expected: no matches. Same for `Microsoft.EntityFrameworkCore` in either project.

- [ ] **Step 8: Commit**

```bash
git add src/ChurchProjection.Domain src/ChurchProjection.Application
git commit -m "feat: add library entities, the service aggregate, and repository ports"
```

---

### Task 6: Infrastructure — import parsers

Three formats, one payload shape. The contract that matters is atomicity's precondition: a parser returns a complete payload or throws. There is no `IEnumerable` to yield partway through, and UNT-IMP-12 fails to compile if one is ever introduced.

**Files:**
- Create: `src/ChurchProjection.Infrastructure/Import/ImportService.cs`
- Create: `src/ChurchProjection.Infrastructure/Import/PlainTextSongParser.cs`
- Create: `src/ChurchProjection.Infrastructure/Import/OpenLyricsParser.cs`
- Create: `src/ChurchProjection.Infrastructure/Import/ZefaniaBibleParser.cs`
- Create: `src/ChurchProjection.Application/Import/ImportLibrary.cs`
- Test: `tests/ChurchProjection.Application.Tests/ImportParserTests.cs` (already written, 13 tests)

**Interfaces:**
- Consumes: `ImportPayload`, `ImportedSong`, `ImportedPage`, `ImportedVerse`, `ImportedTranslation`, `ImportedBookName`, `ImportKind`, `ImportException`, `IImportParser`, `IImportReader` (Task 5); `IVerseRepository`, `ISongRepository`, `IUnitOfWork` (Task 5).
- Produces:
  - `ChurchProjection.Infrastructure.Import.ImportService : IImportReader` with `static ImportService WithDefaultParsers()` and `ImportPayload Parse(Stream input, string fileName)`.
  - `ChurchProjection.Application.Import.ImportLibrary` with `Task<ImportOutcome> ExecuteAsync(Stream file, string fileName, CancellationToken ct)`.
  - `ChurchProjection.Application.Import.ImportOutcome(string Kind, int Imported, int Updated)`.

- [ ] **Step 1: Run the failing tests**

Run: `dotnet test tests/ChurchProjection.Application.Tests`
Expected: FAIL to build — `The type or namespace name 'Infrastructure' does not exist in the namespace 'ChurchProjection'`.

The fixtures the tests read are already in `tests/fixtures/`. Open `song-plain.txt` before writing the text parser: the labels it must produce, in page order, are `null`, `"Reff"`, `"Bait 2"`, `"Reff"` — stored verbatim, never translated to "chorus".

- [ ] **Step 2: Write the plain-text song parser**

```csharp
using System.Text;
using System.Text.RegularExpressions;

using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// The format lyrics actually arrive in: a title, a blank line, then one block
/// of lines per projected page. A block may open with a section label, either
/// bracketed or colon-terminated, because both are what people type.
/// </summary>
public sealed partial class PlainTextSongParser : IImportParser
{
    [GeneratedRegex(@"^\[(?<label>.+)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedLabel();

    // ponytail: a label is a short line ending in a colon. A lyric line that
    // ends in a colon would be misread as a label; nobody writes those, and the
    // fix if they ever do is to require the block to have another line.
    [GeneratedRegex(@"^(?<label>[^:]{1,30}):$", RegexOptions.CultureInvariant)]
    private static partial Regex ColonLabel();

    public bool CanHandle(string fileName, ReadOnlySpan<byte> head) =>
        Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase);

    public ImportPayload Parse(Stream input, string fileName)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        // Word on Windows produces CRLF. Normalise once, here, so no rule below
        // has to think about it (UNT-IMP-13).
        var lines = reader.ReadToEnd().ReplaceLineEndings("\n").Split('\n');

        var titleIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));

        if (titleIndex < 0)
        {
            throw new ImportException($"'{fileName}' is empty. A song file needs a title line and at least one verse.");
        }

        var title = lines[titleIndex].Trim();
        var pages = ReadPages(lines.Skip(titleIndex + 1));

        if (pages.Count == 0)
        {
            throw new ImportException($"'{title}' has a title but no verses. Separate each projected page with a blank line.");
        }

        var song = new ImportedSong(title, Author: null, Ccli: null, Language: null, pages);

        return new ImportPayload(ImportKind.Song, [song], [], Translation: null);
    }

    private static List<ImportedPage> ReadPages(IEnumerable<string> lines)
    {
        var pages = new List<ImportedPage>();
        var block = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                AddPage(pages, block);
                block.Clear();
            }
            else
            {
                block.Add(line.TrimEnd());
            }
        }

        AddPage(pages, block);

        return pages;
    }

    private static void AddPage(List<ImportedPage> pages, List<string> block)
    {
        if (block.Count == 0)
        {
            return;
        }

        var label = ReadLabel(block[0]);
        var body = string.Join("\n", label is null ? block : block.Skip(1)).Trim();

        if (body.Length == 0)
        {
            return;
        }

        pages.Add(new ImportedPage(pages.Count, label, body));
    }

    private static string? ReadLabel(string line)
    {
        var trimmed = line.Trim();
        var bracketed = BracketedLabel().Match(trimmed);

        if (bracketed.Success)
        {
            return bracketed.Groups["label"].Value.Trim();
        }

        var colon = ColonLabel().Match(trimmed);

        return colon.Success ? colon.Groups["label"].Value.Trim() : null;
    }
}
```

- [ ] **Step 3: Write the OpenLyrics parser**

```csharp
using System.Xml;
using System.Xml.Linq;

using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// OpenLyrics is what other projection software exports, so it is the format a
/// church switching to this system arrives with.
/// </summary>
public sealed class OpenLyricsParser : IImportParser
{
    private static readonly XNamespace Ns = "http://openlyrics.info/namespace/2009/song";

    public bool CanHandle(string fileName, ReadOnlySpan<byte> head) =>
        Path.GetExtension(fileName).Equals(".xml", StringComparison.OrdinalIgnoreCase)
        && Contains(head, "openlyrics.info");

    public ImportPayload Parse(Stream input, string fileName)
    {
        XDocument document;

        try
        {
            document = XDocument.Load(input, LoadOptions.SetLineInfo);
        }
        catch (XmlException error)
        {
            throw new ImportException(
                $"'{fileName}' is not valid XML: {error.Message} (line {error.LineNumber}, position {error.LinePosition}).");
        }

        var root = document.Root
            ?? throw new ImportException($"'{fileName}' has no root element.");

        var title = root.Element(Ns + "properties")?.Element(Ns + "titles")?.Element(Ns + "title")?.Value.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ImportException($"'{fileName}' has no <title>. A song without a title cannot be searched for.");
        }

        var authors = root.Element(Ns + "properties")?.Element(Ns + "authors")?
            .Elements(Ns + "author").Select(author => author.Value.Trim()).ToArray() ?? [];

        var pages = root.Element(Ns + "lyrics")?.Elements(Ns + "verse")
            .Select((verse, index) => new ImportedPage(
                index,
                verse.Attribute("name")?.Value,
                LineText(verse.Element(Ns + "lines"))))
            .Where(page => page.Text.Length > 0)
            .ToArray() ?? [];

        if (pages.Length == 0)
        {
            throw new ImportException($"'{title}' has no <verse> elements with any lines.");
        }

        var song = new ImportedSong(
            title,
            authors.Length == 0 ? null : string.Join(", ", authors),
            root.Element(Ns + "properties")?.Element(Ns + "ccliNo")?.Value,
            root.Attribute("lang")?.Value,
            pages);

        return new ImportPayload(ImportKind.Song, [song], [], Translation: null);
    }

    /// <summary>A &lt;br/&gt; is a line break on the projected page, not whitespace.</summary>
    private static string LineText(XElement? lines) =>
        lines is null
            ? string.Empty
            : string.Concat(lines.Nodes().Select(node => node switch
            {
                XText text => text.Value,
                XElement { Name.LocalName: "br" } => "\n",
                XElement element => element.Value,
                _ => string.Empty,
            })).Trim();

    internal static bool Contains(ReadOnlySpan<byte> head, string needle) =>
        System.Text.Encoding.UTF8.GetString(head).Contains(needle, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Write the Zefania Bible parser**

```csharp
using System.Xml;
using System.Xml.Linq;

using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// Zefania XML is how Terjemahan Baru and Terjemahan Lama are distributed.
/// Book numbers are canonical and are stored as given — nothing here clamps
/// them to 1..66, because a deuterocanonical edition is a real file someone
/// may hand the administrator.
/// </summary>
public sealed class ZefaniaBibleParser : IImportParser
{
    public bool CanHandle(string fileName, ReadOnlySpan<byte> head) =>
        Path.GetExtension(fileName).Equals(".xml", StringComparison.OrdinalIgnoreCase)
        && OpenLyricsParser.Contains(head, "XMLBIBLE");

    public ImportPayload Parse(Stream input, string fileName)
    {
        XDocument document;

        try
        {
            // Loaded whole. A streaming reader could hand back 30,000 verses and
            // then fail on the last one, which is the half-imported Bible
            // FR-IMP-05 exists to prevent.
            document = XDocument.Load(input, LoadOptions.SetLineInfo);
        }
        catch (XmlException error)
        {
            throw new ImportException(
                $"'{fileName}' is not valid XML: {error.Message} (line {error.LineNumber}, position {error.LinePosition}).");
        }

        var root = document.Root
            ?? throw new ImportException($"'{fileName}' has no root element.");

        var verses = new List<ImportedVerse>();
        var books = new List<ImportedBookName>();

        foreach (var book in root.Elements("BIBLEBOOK"))
        {
            var bookId = RequiredInt(book, "bnumber", fileName);

            books.Add(new ImportedBookName(
                bookId,
                book.Attribute("bname")?.Value ?? $"Book {bookId}",
                book.Attribute("bsname")?.Value));

            foreach (var chapter in book.Elements("CHAPTER"))
            {
                var chapterNumber = RequiredInt(chapter, "cnumber", fileName);

                foreach (var verse in chapter.Elements("VERS"))
                {
                    verses.Add(new ImportedVerse(
                        bookId,
                        chapterNumber,
                        RequiredInt(verse, "vnumber", fileName),
                        verse.Value.Trim()));
                }
            }
        }

        if (verses.Count == 0)
        {
            throw new ImportException($"'{fileName}' contains no <VERS> elements.");
        }

        var name = root.Attribute("biblename")?.Value ?? Path.GetFileNameWithoutExtension(fileName);
        var id = Slug(Path.GetFileNameWithoutExtension(fileName));

        var translation = new ImportedTranslation(
            id,
            root.Attribute("abbrev")?.Value ?? id.ToUpperInvariant(),
            name,
            root.Attribute("lang")?.Value ?? "id",
            books);

        return new ImportPayload(ImportKind.Bible, [], verses, translation);
    }

    private static int RequiredInt(XElement element, string attribute, string fileName)
    {
        var value = element.Attribute(attribute)?.Value;

        if (!int.TryParse(value, out var number))
        {
            var line = (element as IXmlLineInfo).LineNumber;

            throw new ImportException(
                $"'{fileName}' line {line}: <{element.Name.LocalName}> has {attribute}='{value}', which is not a number.");
        }

        return number;
    }

    private static string Slug(string value) =>
        new(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
```

- [ ] **Step 5: Write the parser host**

```csharp
using ChurchProjection.Application.Import;

namespace ChurchProjection.Infrastructure.Import;

/// <summary>
/// Picks a parser by extension and by what the first bytes actually contain,
/// because an OpenLyrics song and a Zefania Bible are both called ".xml".
/// </summary>
public sealed class ImportService(IReadOnlyList<IImportParser> parsers) : IImportReader
{
    private const int HeadBytes = 512;

    public static ImportService WithDefaultParsers() =>
        new([new ZefaniaBibleParser(), new OpenLyricsParser(), new PlainTextSongParser()]);

    public ImportPayload Parse(Stream input, string fileName)
    {
        // ponytail: the whole file is buffered so the head can be sniffed and
        // the parser can still read from the start. Imports are a few megabytes;
        // revisit if a full-canon Bible with audio ever arrives in one upload.
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);

        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            throw new ImportException($"'{fileName}' is empty.");
        }

        var head = bytes.AsSpan(0, Math.Min(HeadBytes, bytes.Length));
        var parser = parsers.FirstOrDefault(candidate => candidate.CanHandle(fileName, head))
            ?? throw new ImportException(
                $"Nothing here can read '{fileName}'. Supported: plain-text lyrics (.txt), OpenLyrics (.xml), Zefania Bibles (.xml).");

        using var replay = new MemoryStream(bytes, writable: false);

        return parser.Parse(replay, fileName);
    }
}
```

Order matters: Zefania and OpenLyrics both claim `.xml`, so they are asked first and the plain-text parser is the fallback. `ParseText("", "empty.txt")` never reaches a parser — the empty check above rejects it, which satisfies UNT-IMP-06.

- [ ] **Step 6: Run the parser tests**

Run: `dotnet test tests/ChurchProjection.Application.Tests`
Expected: PASS, 13 tests.

UNT-IMP-11 is the one to watch: for a song payload `Verses` must be empty and for a Bible payload `Songs` must be empty. A parser that populates both has misreported what it read.

- [ ] **Step 7: Write the use case that stores a payload**

This is the Application half. It has no test of its own here — SYS-IMP-01 to SYS-IMP-04 cover it over HTTP in Task 13 — but it belongs with the parsers and it is what makes the transaction rule concrete.

```csharp
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Application.Import;

public sealed record ImportOutcome(string Kind, int Imported, int Updated);

/// <summary>
/// Parse, then write everything in one transaction. Parsing happens outside the
/// transaction on purpose: a file that cannot be read must not even open one.
/// </summary>
public sealed class ImportLibrary(
    IImportReader reader,
    IVerseRepository verses,
    ISongRepository songs,
    IUnitOfWork unitOfWork)
{
    public Task<ImportOutcome> ExecuteAsync(Stream file, string fileName, CancellationToken ct)
    {
        var payload = reader.Parse(file, fileName);

        return unitOfWork.InTransactionAsync(token => payload.Kind switch
        {
            ImportKind.Bible => StoreBibleAsync(payload, token),
            _ => StoreSongsAsync(payload, token),
        }, ct);
    }

    private async Task<ImportOutcome> StoreBibleAsync(ImportPayload payload, CancellationToken ct)
    {
        var written = await verses.ReplaceTranslationAsync(payload, ct);

        return new ImportOutcome("bible", written, 0);
    }

    private async Task<ImportOutcome> StoreSongsAsync(ImportPayload payload, CancellationToken ct)
    {
        var imported = 0;
        var updated = 0;

        foreach (var incoming in payload.Songs)
        {
            var existing = await songs.FindByTitleAsync(incoming.Title, ct);

            var song = existing ?? new Song
            {
                Id = Guid.NewGuid().ToString("n"),
                Title = incoming.Title,
            };

            song.Title = incoming.Title;
            song.Author = incoming.Author;
            song.Ccli = incoming.Ccli;
            song.Language = incoming.Language;
            song.UpdatedAt = DateTime.UtcNow;

            // Re-importing a song replaces its pages outright. That is the point:
            // the operator fixed a typo in the second verse and expects the fixed
            // verse on the screen, not a fifth copy of the song (FR-IMP-04).
            song.Pages.Clear();
            song.Pages.AddRange(incoming.Pages.Select(page => new SongPage
            {
                Position = page.Position,
                SectionLabel = page.SectionLabel,
                Text = page.Text,
            }));

            await songs.UpsertAsync(song, ct);

            if (existing is null)
            {
                imported++;
            }
            else
            {
                updated++;
            }
        }

        return new ImportOutcome("song", imported, updated);
    }
}
```

- [ ] **Step 8: Build and re-run**

Run: `dotnet test tests/ChurchProjection.Application.Tests`
Expected: PASS, 13 tests, zero warnings.

- [ ] **Step 9: Commit**

```bash
git add src/ChurchProjection.Infrastructure/Import src/ChurchProjection.Application/Import
git commit -m "feat: parse plain-text, OpenLyrics, and Zefania imports atomically"
```

---

### Task 7: Infrastructure — schema, migrations, and FTS5

**Files:**
- Modify: `Directory.Packages.props` (nothing to add if the EF entries are already there — verify), `src/ChurchProjection.Infrastructure/ChurchProjection.Infrastructure.csproj`
- Create: `src/ChurchProjection.Infrastructure/Persistence/ProjectionDbContext.cs`
- Create: `src/ChurchProjection.Infrastructure/Persistence/ProjectionDbContextFactory.cs`
- Create: `src/ChurchProjection.Infrastructure/Persistence/LiveStateRow.cs`, `SettingRow.cs`, `BookNameRow.cs`
- Create: `src/ChurchProjection.Infrastructure/Persistence/Configurations/*.cs`
- Create: `src/ChurchProjection.Infrastructure/Persistence/Migrations/` (generated, then one hand-written)
- Create: `tests/ChurchProjection.Api.Tests/PersistenceTests.cs`
- Modify: `docs/testing/TEST-CASES.md` §7

**Interfaces:**
- Consumes: the Domain entities and `ImportPayload` from Task 5.
- Produces:
  - `ProjectionDbContext` with `DbSet<Translation> Translations`, `DbSet<BookNameRow> BookNames`, `DbSet<Verse> Verses`, `DbSet<Song> Songs`, `DbSet<MediaItem> Media`, `DbSet<ServicePlan> Services`, `DbSet<SettingRow> Settings`, `DbSet<LiveStateRow> LiveState`.
  - `ProjectionDbContext.ApplyMigrationsAsync(CancellationToken)` — used by the host in Task 10.

- [ ] **Step 1: Reference EF Core**

```xml
<!-- src/ChurchProjection.Infrastructure/ChurchProjection.Infrastructure.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
</ItemGroup>
```

Versions come from `Directory.Packages.props`; do not put a `Version` attribute here.

- [ ] **Step 2: Write the persistence-only rows**

Three tables have no Domain counterpart because nothing in the Domain needs them as objects.

```csharp
// src/ChurchProjection.Infrastructure/Persistence/BookNameRow.cs
namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>A book's name in one translation's own language (FR-LIB-04).</summary>
public sealed class BookNameRow
{
    public required string TranslationId { get; init; }

    public required int BookId { get; init; }

    public required string Name { get; init; }

    public string? Abbrev { get; init; }
}
```

```csharp
// src/ChurchProjection.Infrastructure/Persistence/SettingRow.cs
namespace ChurchProjection.Infrastructure.Persistence;

public sealed class SettingRow
{
    public required string Key { get; init; }

    public required string Value { get; set; }
}
```

```csharp
// src/ChurchProjection.Infrastructure/Persistence/LiveStateRow.cs
namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>
/// One row, id 1, rewritten on every command. A table rather than a file so it
/// shares the database's durability guarantees — this is what makes a restart
/// mid-service invisible to the congregation (FR-LIV-13).
/// </summary>
public sealed class LiveStateRow
{
    public int Id { get; init; } = 1;

    public string? ServiceId { get; set; }

    public string? LiveItemId { get; set; }

    public int LivePageIndex { get; set; }

    public bool LiveMediaAvailable { get; set; }

    public string? PreviewItemId { get; set; }

    public int PreviewPageIndex { get; set; }

    public bool PreviewMediaAvailable { get; set; }

    public bool Blackout { get; set; }

    public required string SkippedJson { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 3: Write the DbContext**

```csharp
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Persistence;

public sealed class ProjectionDbContext(DbContextOptions<ProjectionDbContext> options) : DbContext(options)
{
    public DbSet<Translation> Translations => Set<Translation>();

    public DbSet<BookNameRow> BookNames => Set<BookNameRow>();

    public DbSet<Verse> Verses => Set<Verse>();

    public DbSet<Song> Songs => Set<Song>();

    public DbSet<MediaItem> Media => Set<MediaItem>();

    public DbSet<ServicePlan> Services => Set<ServicePlan>();

    public DbSet<SettingRow> Settings => Set<SettingRow>();

    public DbSet<LiveStateRow> LiveState => Set<LiveStateRow>();

    public Task ApplyMigrationsAsync(CancellationToken ct) => Database.MigrateAsync(ct);

    protected override void OnModelCreating(ModelBuilder builder) =>
        builder.ApplyConfigurationsFromAssembly(typeof(ProjectionDbContext).Assembly);
}
```

- [ ] **Step 4: Write the entity configurations**

All mapping lives here so the Domain classes stay free of persistence attributes.

```csharp
// src/ChurchProjection.Infrastructure/Persistence/Configurations/LibraryConfigurations.cs
using ChurchProjection.Domain.Library;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurchProjection.Infrastructure.Persistence.Configurations;

public sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.ToTable("translations");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").HasConversion(id => id.Value, value => new TranslationId(value));
        builder.Property(t => t.Abbrev).HasColumnName("abbrev").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").IsRequired();
        builder.Property(t => t.Language).HasColumnName("language").IsRequired();
    }
}

public sealed class BookNameConfiguration : IEntityTypeConfiguration<BookNameRow>
{
    public void Configure(EntityTypeBuilder<BookNameRow> builder)
    {
        builder.ToTable("book_names");
        builder.HasKey(b => new { b.TranslationId, b.BookId });
        builder.Property(b => b.TranslationId).HasColumnName("translation_id");
        builder.Property(b => b.BookId).HasColumnName("book_id");
        builder.Property(b => b.Name).HasColumnName("name").IsRequired();
        builder.Property(b => b.Abbrev).HasColumnName("abbrev");
    }
}

public sealed class VerseConfiguration : IEntityTypeConfiguration<Verse>
{
    public void Configure(EntityTypeBuilder<Verse> builder)
    {
        builder.ToTable("verses");

        // An explicit rowid, because verses_fts is an external-content FTS5
        // table keyed on it.
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(v => v.TranslationId).HasColumnName("translation_id")
            .HasConversion(id => id.Value, value => new TranslationId(value));
        builder.Property(v => v.BookId).HasColumnName("book_id");
        builder.Property(v => v.Chapter).HasColumnName("chapter");
        builder.Property(v => v.Number).HasColumnName("verse");
        builder.Property(v => v.Text).HasColumnName("text").IsRequired();

        builder.HasIndex(v => new { v.TranslationId, v.BookId, v.Chapter, v.Number }).IsUnique();
    }
}

public sealed class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        builder.ToTable("songs");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasConversion(id => id.Value, value => new SongId(value));
        builder.Property(s => s.Title).HasColumnName("title").IsRequired();
        builder.Property(s => s.Author).HasColumnName("author");
        builder.Property(s => s.Ccli).HasColumnName("ccli");
        builder.Property(s => s.Language).HasColumnName("language");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.OwnsMany(s => s.Pages, pages =>
        {
            pages.ToTable("song_pages");
            pages.WithOwner().HasForeignKey("song_id");
            pages.HasKey("song_id", nameof(SongPage.Position));
            pages.Property(p => p.Position).HasColumnName("position");
            pages.Property(p => p.SectionLabel).HasColumnName("section_label");
            pages.Property(p => p.Text).HasColumnName("text").IsRequired();
        });

        builder.Navigation(s => s.Pages).AutoInclude();
    }
}

public sealed class MediaConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").HasConversion(id => id.Value, value => new MediaId(value));
        builder.Property(m => m.Kind).HasColumnName("kind").IsRequired();
        builder.Property(m => m.Filename).HasColumnName("filename").IsRequired();
        builder.Property(m => m.Path).HasColumnName("path").IsRequired();
        builder.Property(m => m.DurationMs).HasColumnName("duration_ms");
        builder.Property(m => m.Width).HasColumnName("width");
        builder.Property(m => m.Height).HasColumnName("height");
    }
}
```

```csharp
// src/ChurchProjection.Infrastructure/Persistence/Configurations/ServiceConfigurations.cs
using System.Text.Json;

using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurchProjection.Infrastructure.Persistence.Configurations;

public sealed class ServicePlanConfiguration : IEntityTypeConfiguration<ServicePlan>
{
    public void Configure(EntityTypeBuilder<ServicePlan> builder)
    {
        builder.ToTable("services");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasConversion(id => id.Value, value => new ServiceId(value));
        builder.Property(s => s.Name).HasColumnName("name").IsRequired();
        builder.Property(s => s.ServiceDate).HasColumnName("service_date");

        // The aggregate exposes IReadOnlyList and owns renumbering, so EF reads
        // and writes the backing field rather than going through the property.
        builder.Metadata
            .FindNavigation(nameof(ServicePlan.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(s => s.Items, items =>
        {
            items.ToTable("service_items");
            items.WithOwner().HasForeignKey("service_id");
            items.HasKey(i => i.Id);
            items.Property(i => i.Id).HasColumnName("id").HasConversion(id => id.Value, value => new ItemId(value));
            items.Property(i => i.Kind).HasColumnName("kind").IsRequired();
            items.Property(i => i.Label).HasColumnName("label").IsRequired();
            items.Property(i => i.Position).HasColumnName("position");
            items.Property(i => i.Ref)
                .HasColumnName("ref_json")
                .IsRequired()
                .HasConversion(
                    reference => JsonSerializer.Serialize(reference, ItemRefJson.Options),
                    json => JsonSerializer.Deserialize<ItemRef>(json, ItemRefJson.Options)!,
                    new ValueComparer<ItemRef>(
                        (left, right) => left == right,
                        reference => reference.GetHashCode(),
                        reference => reference with { }));
        });

        builder.Navigation(s => s.Items).AutoInclude();
    }
}

internal static class ItemRefJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class SettingConfiguration : IEntityTypeConfiguration<SettingRow>
{
    public void Configure(EntityTypeBuilder<SettingRow> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasColumnName("key");
        builder.Property(s => s.Value).HasColumnName("value").IsRequired();
    }
}

public sealed class LiveStateConfiguration : IEntityTypeConfiguration<LiveStateRow>
{
    public void Configure(EntityTypeBuilder<LiveStateRow> builder)
    {
        builder.ToTable("live_state");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.ServiceId).HasColumnName("service_id");
        builder.Property(l => l.LiveItemId).HasColumnName("live_item_id");
        builder.Property(l => l.LivePageIndex).HasColumnName("live_page_index");
        builder.Property(l => l.LiveMediaAvailable).HasColumnName("live_media_available");
        builder.Property(l => l.PreviewItemId).HasColumnName("preview_item_id");
        builder.Property(l => l.PreviewPageIndex).HasColumnName("preview_page_index");
        builder.Property(l => l.PreviewMediaAvailable).HasColumnName("preview_media_available");
        builder.Property(l => l.Blackout).HasColumnName("blackout");
        builder.Property(l => l.SkippedJson).HasColumnName("skipped_json").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
    }
}
```

- [ ] **Step 5: Write the design-time factory**

`dotnet ef` needs a way to build the context before the Api host composes one.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>Exists only so `dotnet ef migrations` can run without the host.</summary>
public sealed class ProjectionDbContextFactory : IDesignTimeDbContextFactory<ProjectionDbContext>
{
    public ProjectionDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ProjectionDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options);
}
```

- [ ] **Step 6: Generate the relational migration**

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial \
  --project src/ChurchProjection.Infrastructure \
  --output-dir Persistence/Migrations
```

Read the generated `Up()` before moving on. Every table above should be there; if `song_pages` or `service_items` is missing, the `OwnsMany` configuration did not take.

- [ ] **Step 7: Add the FTS5 migration by hand**

```bash
dotnet ef migrations add Fts5Search \
  --project src/ChurchProjection.Infrastructure \
  --output-dir Persistence/Migrations
```

EF sees no model change, so it generates an empty migration. Fill it in:

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchProjection.Infrastructure.Persistence.Migrations;

/// <summary>
/// FTS5 virtual tables and the triggers that keep them in step. EF cannot model
/// a virtual table, so this migration is raw SQL and stays raw SQL. Nothing
/// outside VerseRepository.SearchAsync and SongRepository.SearchAsync may touch
/// these tables.
/// </summary>
public partial class Fts5Search : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE VIRTUAL TABLE verses_fts USING fts5(
                text,
                content='verses',
                content_rowid='id',
                tokenize='unicode61 remove_diacritics 2');
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER verses_fts_ai AFTER INSERT ON verses BEGIN
                INSERT INTO verses_fts(rowid, text) VALUES (new.id, new.text);
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER verses_fts_ad AFTER DELETE ON verses BEGIN
                INSERT INTO verses_fts(verses_fts, rowid, text) VALUES ('delete', old.id, old.text);
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER verses_fts_au AFTER UPDATE ON verses BEGIN
                INSERT INTO verses_fts(verses_fts, rowid, text) VALUES ('delete', old.id, old.text);
                INSERT INTO verses_fts(rowid, text) VALUES (new.id, new.text);
            END;
            """);

        // songs_fts is a plain (not external-content) table because one row has
        // to carry a title from one table and lyrics from another.
        migrationBuilder.Sql("""
            CREATE VIRTUAL TABLE songs_fts USING fts5(
                song_id UNINDEXED,
                title,
                text,
                tokenize='unicode61 remove_diacritics 2');
            """);

        foreach (var (name, table, timing, id) in new[]
        {
            ("songs_fts_ai", "songs", "AFTER INSERT", "new.id"),
            ("songs_fts_au", "songs", "AFTER UPDATE", "new.id"),
            ("song_pages_fts_ai", "song_pages", "AFTER INSERT", "new.song_id"),
            ("song_pages_fts_au", "song_pages", "AFTER UPDATE", "new.song_id"),
            ("song_pages_fts_ad", "song_pages", "AFTER DELETE", "old.song_id"),
        })
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER {name} {timing} ON {table} BEGIN
                    DELETE FROM songs_fts WHERE song_id = {id};
                    INSERT INTO songs_fts(song_id, title, text)
                        SELECT s.id,
                               s.title,
                               COALESCE((SELECT group_concat(p.text, ' ')
                                         FROM song_pages p WHERE p.song_id = s.id), '')
                        FROM songs s WHERE s.id = {id};
                END;
                """);
        }

        migrationBuilder.Sql("""
            CREATE TRIGGER songs_fts_ad AFTER DELETE ON songs BEGIN
                DELETE FROM songs_fts WHERE song_id = old.id;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var trigger in new[]
        {
            "verses_fts_ai", "verses_fts_ad", "verses_fts_au",
            "songs_fts_ai", "songs_fts_au", "songs_fts_ad",
            "song_pages_fts_ai", "song_pages_fts_au", "song_pages_fts_ad",
        })
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");
        }

        migrationBuilder.Sql("DROP TABLE IF EXISTS verses_fts;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS songs_fts;");
    }
}
```

- [ ] **Step 8: Write the failing schema test**

`ProjectionAppFactory` uses a real SQLite file rather than the in-memory provider precisely because FTS5 behaves differently. This test proves the schema, not the host.

```csharp
// tests/ChurchProjection.Api.Tests/PersistenceTests.cs
//
// INT-15: the schema applies and the FTS5 index stays in step with the tables
// it indexes. A search index that silently stops updating is invisible until a
// Sunday when the song the operator searched for is not there.

using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Api.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"projection-{Guid.NewGuid():n}.db");

    private ProjectionDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ProjectionDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options);

    [Fact]
    public async Task INT_15_the_migrations_create_the_full_text_indexes()
    {
        await using var db = CreateContext();
        await db.ApplyMigrationsAsync(TestContext.Current.CancellationToken);

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("verses_fts", tables);
        Assert.Contains("songs_fts", tables);
    }

    [Fact]
    public async Task INT_15_inserting_a_verse_makes_it_findable()
    {
        await using var db = CreateContext();
        await db.ApplyMigrationsAsync(TestContext.Current.CancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO translations(id, abbrev, name, language) VALUES ('tb', 'TB', 'Terjemahan Baru', 'id')",
            TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO verses(translation_id, book_id, chapter, verse, text) " +
            "VALUES ('tb', 43, 3, 16, 'Karena begitu besar kasih Allah akan dunia ini')",
            TestContext.Current.CancellationToken);

        var hits = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM verses_fts WHERE verses_fts MATCH 'kasih'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, hits[0]);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 9: Run it**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter INT_15`
Expected: PASS, 2 tests.

If `verses_fts MATCH` returns 0 rows, the insert trigger did not fire — check that the `verses` primary key is a plain `INTEGER PRIMARY KEY` in the generated migration, because an external-content FTS5 table joins on the rowid.

- [ ] **Step 10: Record the new case**

Add to `docs/testing/TEST-CASES.md` §7, after the INT-14 row:

```markdown
| INT-15 | FR-LIB-05, FR-LIB-13 | **C** | **When** the migrations are applied and a verse row is inserted, **then** the FTS5 index contains it. The index is only ever wrong against a real database, which is why this runs against a file rather than a mock. |
```

- [ ] **Step 11: Commit**

```bash
git add src/ChurchProjection.Infrastructure tests/ChurchProjection.Api.Tests/PersistenceTests.cs docs/testing/TEST-CASES.md
git commit -m "feat: add the SQLite schema, migrations, and FTS5 search indexes"
```

---

### Task 8: Infrastructure — repository adapters

The queries. TEST-PLAN §3.1 refuses to unit-test these against a mocked `DbContext`, so they are proven here against a real file.

**Files:**
- Create: `src/ChurchProjection.Infrastructure/Persistence/UnitOfWork.cs`
- Create: `src/ChurchProjection.Infrastructure/Repositories/TranslationRepository.cs`, `VerseRepository.cs`, `SongRepository.cs`, `MediaRepository.cs`, `ServiceRepository.cs`, `LiveStateRepository.cs`, `SettingsRepository.cs`
- Modify: `tests/ChurchProjection.Api.Tests/PersistenceTests.cs`
- Modify: `docs/testing/TEST-CASES.md` §7

**Interfaces:**
- Consumes: every port from Task 5, `ProjectionDbContext` from Task 7.
- Produces: one public class per port, each named `<Port without I>`, registered in Task 10's DI composition.

- [ ] **Step 1: Write the unit of work**

```csharp
using ChurchProjection.Application.Ports;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Persistence;

public sealed class UnitOfWork(ProjectionDbContext db) : IUnitOfWork
{
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var result = await work(ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return result;
    }
}
```

Disposing an uncommitted transaction rolls it back, so a throw from `work` leaves nothing behind. That is the whole of FR-IMP-05's enforcement.

- [ ] **Step 2: Write the simple adapters**

```csharp
// src/ChurchProjection.Infrastructure/Repositories/TranslationRepository.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class TranslationRepository(ProjectionDbContext db) : ITranslationRepository
{
    public async Task<IReadOnlyList<Translation>> ListAsync(CancellationToken ct) =>
        await db.Translations.AsNoTracking().OrderBy(t => t.Abbrev).ToListAsync(ct);

    public Task<Translation?> FindAsync(TranslationId id, CancellationToken ct) =>
        db.Translations.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
}
```

```csharp
// src/ChurchProjection.Infrastructure/Repositories/MediaRepository.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class MediaRepository(ProjectionDbContext db) : IMediaRepository
{
    public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct) =>
        await db.Media.AsNoTracking().OrderBy(m => m.Filename).ToListAsync(ct);

    public Task<MediaItem?> FindAsync(MediaId id, CancellationToken ct) =>
        db.Media.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);

    public async Task<MediaId> AddAsync(MediaItem item, CancellationToken ct)
    {
        db.Media.Add(item);
        await db.SaveChangesAsync(ct);

        return item.Id;
    }

    public async Task RemoveAsync(MediaId id, CancellationToken ct)
    {
        await db.Media.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
    }
}
```

```csharp
// src/ChurchProjection.Infrastructure/Repositories/SettingsRepository.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class SettingsRepository(ProjectionDbContext db) : ISettingsRepository
{
    public async Task<string?> GetAsync(string key, CancellationToken ct) =>
        (await db.Settings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == key, ct))?.Value;

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var existing = await db.Settings.SingleOrDefaultAsync(s => s.Key == key, ct);

        if (existing is null)
        {
            db.Settings.Add(new SettingRow { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }

        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Write the verse repository, including the only two places FTS5 is reachable from**

```csharp
using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class VerseRepository(ProjectionDbContext db) : IVerseRepository
{
    public async Task<Passage?> GetAsync(TranslationId translation, BibleReference reference, CancellationToken ct)
    {
        var verseEnd = reference.VerseEnd ?? int.MaxValue;

        var verses = await db.Verses.AsNoTracking()
            .Where(v => v.TranslationId == translation
                        && v.BookId == reference.BookId
                        && v.Chapter == reference.Chapter
                        && v.Number >= reference.VerseStart
                        && v.Number <= verseEnd)
            .OrderBy(v => v.Number)
            .ToListAsync(ct);

        if (verses.Count == 0)
        {
            return null;
        }

        var bookName = await db.BookNames.AsNoTracking()
            .Where(b => b.TranslationId == translation.Value && b.BookId == reference.BookId)
            .Select(b => b.Name)
            .SingleOrDefaultAsync(ct);

        return new Passage(
            translation,
            reference.BookId,
            bookName ?? BookNames.Name(reference.BookId) ?? $"Book {reference.BookId}",
            reference.Chapter,
            verses);
    }

    public async Task<IReadOnlyList<VerseHit>> SearchAsync(
        TranslationId? translation, string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        const string sql = """
            SELECT v.translation_id AS TranslationId,
                   v.book_id        AS BookId,
                   COALESCE(b.name, '') AS BookName,
                   v.chapter        AS Chapter,
                   v.verse          AS Verse,
                   v.text           AS Text
            FROM verses_fts f
            JOIN verses v ON v.id = f.rowid
            LEFT JOIN book_names b
                   ON b.translation_id = v.translation_id AND b.book_id = v.book_id
            WHERE verses_fts MATCH @query
              AND (@translation IS NULL OR v.translation_id = @translation)
            ORDER BY rank
            LIMIT @limit
            """;

        return await db.Database.SqlQueryRaw<VerseHit>(
                sql,
                new SqliteParameter("@query", AsPhrase(query)),
                new SqliteParameter("@translation", (object?)translation?.Value ?? DBNull.Value),
                new SqliteParameter("@limit", limit))
            .ToListAsync(ct);
    }

    public async Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct)
    {
        var translation = payload.Translation
            ?? throw new InvalidOperationException("A Bible payload must carry its translation.");

        // Replace, never merge. A partially overwritten translation is a Bible
        // with two editions of the same verse in it.
        await db.Verses.Where(v => v.TranslationId == new TranslationId(translation.Id)).ExecuteDeleteAsync(ct);
        await db.BookNames.Where(b => b.TranslationId == translation.Id).ExecuteDeleteAsync(ct);

        if (await db.Translations.FindAsync([new TranslationId(translation.Id)], ct) is null)
        {
            db.Translations.Add(new Translation
            {
                Id = translation.Id,
                Abbrev = translation.Abbrev,
                Name = translation.Name,
                Language = translation.Language,
            });
        }

        db.BookNames.AddRange(translation.Books.Select(book => new BookNameRow
        {
            TranslationId = translation.Id,
            BookId = book.BookId,
            Name = book.Name,
            Abbrev = book.Abbrev,
        }));

        db.Verses.AddRange(payload.Verses.Select(verse => new Verse
        {
            TranslationId = translation.Id,
            BookId = verse.BookId,
            Chapter = verse.Chapter,
            Number = verse.Verse,
            Text = verse.Text,
        }));

        await db.SaveChangesAsync(ct);

        return payload.Verses.Count;
    }

    /// <summary>
    /// Wraps the operator's words in an FTS5 phrase. The value is still a bound
    /// parameter — this is about FTS5's own query grammar, not about SQL: an
    /// apostrophe in "Allah's" is a syntax error to a bare MATCH.
    /// </summary>
    internal static string AsPhrase(string query) => $"\"{query.Trim().Replace("\"", "\"\"")}\"";
}
```

- [ ] **Step 4: Write the song repository**

```csharp
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class SongRepository(ProjectionDbContext db) : ISongRepository
{
    public async Task<Song?> FindAsync(SongId id, CancellationToken ct)
    {
        var song = await db.Songs.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);

        song?.Pages.Sort((left, right) => left.Position.CompareTo(right.Position));

        return song;
    }

    public Task<Song?> FindByTitleAsync(string title, CancellationToken ct) =>
        db.Songs.SingleOrDefaultAsync(s => s.Title.ToLower() == title.ToLower(), ct);

    public async Task<IReadOnlyList<SongHit>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            // An empty search lists the library. The operator opening the song
            // panel has not typed anything yet and still wants to see songs.
            return await db.Songs.AsNoTracking()
                .OrderBy(s => s.Title)
                .Take(limit)
                .Select(s => new SongHit(s.Id.Value, s.Title, s.Author, s.Language))
                .ToListAsync(ct);
        }

        const string sql = """
            SELECT s.id       AS Id,
                   s.title    AS Title,
                   s.author   AS Author,
                   s.language AS Language
            FROM songs_fts f
            JOIN songs s ON s.id = f.song_id
            WHERE songs_fts MATCH @query
            ORDER BY rank
            LIMIT @limit
            """;

        return await db.Database.SqlQueryRaw<SongHit>(
                sql,
                new SqliteParameter("@query", VerseRepository.AsPhrase(query)),
                new SqliteParameter("@limit", limit))
            .ToListAsync(ct);
    }

    public async Task<SongId> UpsertAsync(Song song, CancellationToken ct)
    {
        if (db.Entry(song).State == EntityState.Detached)
        {
            db.Songs.Add(song);
        }

        await db.SaveChangesAsync(ct);

        return song.Id;
    }
}
```

- [ ] **Step 5: Write the service and live-state repositories**

```csharp
// src/ChurchProjection.Infrastructure/Repositories/ServiceRepository.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class ServiceRepository(ProjectionDbContext db) : IServiceRepository
{
    public Task<ServicePlan?> FindAsync(ServiceId id, CancellationToken ct) =>
        db.Services.SingleOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken ct) =>
        await db.Services.AsNoTracking()
            .OrderByDescending(s => s.ServiceDate)
            .Select(s => new ServiceSummary(s.Id.Value, s.Name, s.ServiceDate, s.Items.Count))
            .ToListAsync(ct);

    public async Task SaveAsync(ServicePlan plan, CancellationToken ct)
    {
        if (db.Entry(plan).State == EntityState.Detached)
        {
            db.Services.Add(plan);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(ServiceId id, CancellationToken ct)
    {
        var plan = await db.Services.SingleOrDefaultAsync(s => s.Id == id, ct);

        if (plan is null)
        {
            return;
        }

        // Deleting a service deletes its items and nothing else. The song it
        // pointed at stays in the library (FR-SVC-07).
        db.Services.Remove(plan);
        await db.SaveChangesAsync(ct);
    }
}
```

```csharp
// src/ChurchProjection.Infrastructure/Repositories/LiveStateRepository.cs
using System.Text.Json;

using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Live;
using ChurchProjection.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Repositories;

public sealed class LiveStateRepository(ProjectionDbContext db) : ILiveStateRepository
{
    private const int SingleRowId = 1;

    public async Task<LiveSnapshot?> LoadAsync(CancellationToken ct)
    {
        var row = await db.LiveState.AsNoTracking().SingleOrDefaultAsync(l => l.Id == SingleRowId, ct);

        if (row is null)
        {
            return null;
        }

        var skipped = JsonSerializer.Deserialize<List<string>>(row.SkippedJson) ?? [];

        return new LiveSnapshot(
            row.LiveItemId is null ? null : new Slot(row.LiveItemId, row.LivePageIndex, row.LiveMediaAvailable),
            row.PreviewItemId is null ? null : new Slot(row.PreviewItemId, row.PreviewPageIndex, row.PreviewMediaAvailable),
            row.Blackout,
            [.. skipped.Select(id => new ItemId(id))],
            row.ServiceId);
    }

    public async Task SaveAsync(LiveSnapshot snapshot, CancellationToken ct)
    {
        var row = await db.LiveState.SingleOrDefaultAsync(l => l.Id == SingleRowId, ct);

        if (row is null)
        {
            row = new LiveStateRow { Id = SingleRowId, SkippedJson = "[]" };
            db.LiveState.Add(row);
        }

        row.ServiceId = snapshot.ServiceId;
        row.LiveItemId = snapshot.Live?.ItemId.Value;
        row.LivePageIndex = snapshot.Live?.PageIndex ?? 0;
        row.LiveMediaAvailable = snapshot.Live?.MediaAvailable ?? false;
        row.PreviewItemId = snapshot.Preview?.ItemId.Value;
        row.PreviewPageIndex = snapshot.Preview?.PageIndex ?? 0;
        row.PreviewMediaAvailable = snapshot.Preview?.MediaAvailable ?? false;
        row.Blackout = snapshot.Blackout;
        row.SkippedJson = JsonSerializer.Serialize(snapshot.Skipped.Select(id => id.Value));
        row.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 6: Extend the persistence test**

Append to `tests/ChurchProjection.Api.Tests/PersistenceTests.cs`:

```csharp
    [Fact]
    public async Task INT_16_a_service_round_trips_with_its_items_in_order()
    {
        await using var db = CreateContext();
        await db.ApplyMigrationsAsync(TestContext.Current.CancellationToken);

        var repository = new ServiceRepository(db);
        var plan = new ServicePlan
        {
            Id = "svc_1",
            Name = "Kebaktian Minggu",
            ServiceDate = new DateOnly(2026, 8, 23),
        };

        plan.Append(new ServiceItem
        {
            Id = "itm_1",
            Kind = "song",
            Label = "Pujian",
            Ref = new ItemRef { SongId = "song_1" },
        });
        plan.Append(new ServiceItem
        {
            Id = "itm_2",
            Kind = "bible",
            Label = "Pembacaan",
            Ref = new ItemRef { TranslationId = "tb", BookId = 43, Chapter = 3, VerseStart = 16, VerseEnd = 16 },
        });

        await repository.SaveAsync(plan, TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var reloaded = await repository.FindAsync("svc_1", TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal(new[] { "itm_1", "itm_2" }, reloaded.Items.Select(item => item.Id));
        Assert.Equal(new[] { 0, 1 }, reloaded.Items.Select(item => item.Position));
        Assert.Equal(43, reloaded.Items[1].Ref.BookId);

        Assert.True(reloaded.Reorder(["itm_2", "itm_1"]));
        Assert.False(reloaded.Reorder(["itm_2"]));
        Assert.Equal(new[] { "itm_2", "itm_1" }, reloaded.Items.Select(item => item.Id));
    }
```

Add the matching `using ChurchProjection.Domain.Services;`, `using ChurchProjection.Domain.Library;`, and `using ChurchProjection.Infrastructure.Repositories;` to the top of the file.

- [ ] **Step 7: Run it**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter "INT_15|INT_16"`
Expected: PASS, 3 tests.

- [ ] **Step 8: Record the new case**

Add to `docs/testing/TEST-CASES.md` §7:

```markdown
| INT-16 | FR-SVC-02, FR-SVC-04 | | **When** a service with items is saved and reloaded, **then** the items come back in position order with their kind-specific `ref` intact, and a reorder that is not a permutation is refused. |
```

- [ ] **Step 9: Commit**

```bash
git add src/ChurchProjection.Infrastructure tests/ChurchProjection.Api.Tests/PersistenceTests.cs docs/testing/TEST-CASES.md
git commit -m "feat: add EF repository adapters for every port"
```

---

### Task 9: Infrastructure — the cache decorator

NFR-REL-09: the cache may never be a precondition. This task exists to make that
provable rather than promised. INT-14 is specified in `TEST-CASES.md` but was
never written; it is written here.

**Files:**
- Create: `src/ChurchProjection.Infrastructure/Caching/CacheGeneration.cs`
- Create: `src/ChurchProjection.Infrastructure/Caching/CachedVerseRepository.cs`
- Create: `tests/ChurchProjection.Api.Tests/CacheFallbackTests.cs`
- Modify: `src/ChurchProjection.Infrastructure/ChurchProjection.Infrastructure.csproj`

**Interfaces:**
- Consumes: `IVerseRepository` (Task 5), `VerseRepository` (Task 8).
- Produces:
  - `CachedVerseRepository(IVerseRepository inner, IDistributedCache cache, CacheGeneration generation, ILogger<CachedVerseRepository> logger) : IVerseRepository`
  - `CacheGeneration` — singleton, `int Current`, `void Bump()`

- [ ] **Step 1: Reference the caching abstractions**

```xml
<!-- src/ChurchProjection.Infrastructure/ChurchProjection.Infrastructure.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Caching.Abstractions" />
  <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
</ItemGroup>
```

The decorator talks to `IDistributedCache`, not to StackExchange.Redis directly.
That is what lets the host swap in the in-memory implementation when no
connection string is configured, with no second code path to keep honest.

- [ ] **Step 2: Write the failing test**

```csharp
// tests/ChurchProjection.Api.Tests/CacheFallbackTests.cs
//
// INT-14: a configured but unreachable cache degrades to the database. Every
// cache call is wrapped, not just the read — a Redis that dies between the miss
// and the write-back would otherwise throw on the way out, after the work was
// already done.

using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;
using ChurchProjection.Infrastructure.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ChurchProjection.Api.Tests;

public class CacheFallbackTests
{
    [Fact]
    public async Task INT_14_an_unreachable_cache_still_serves_the_passage()
    {
        var logger = new CollectingLogger();
        var repository = new CachedVerseRepository(
            new StubVerseRepository(), new BrokenCache(), new CacheGeneration(), logger);

        var reference = new BibleReference(43, 3, 16, 16);

        var first = await repository.GetAsync(
            new TranslationId("tb"), reference, TestContext.Current.CancellationToken);
        var second = await repository.GetAsync(
            new TranslationId("tb"), reference, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Karena begitu besar kasih Allah", first.Verses[0].Text);

        // One warning, not one per request: a dead Redis must not fill the disk
        // with log during a service.
        Assert.Equal(1, logger.Warnings);
    }

    private sealed class BrokenCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("cache is down");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");

        public void Refresh(string key) => throw new InvalidOperationException("cache is down");

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");

        public void Remove(string key) => throw new InvalidOperationException("cache is down");

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("cache is down");

        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("cache is down");
    }

    private sealed class StubVerseRepository : IVerseRepository
    {
        public Task<Passage?> GetAsync(TranslationId translation, BibleReference reference, CancellationToken ct) =>
            Task.FromResult<Passage?>(new Passage(
                translation,
                reference.BookId,
                "Yohanes",
                reference.Chapter,
                [new Verse
                {
                    TranslationId = translation,
                    BookId = reference.BookId,
                    Chapter = reference.Chapter,
                    Number = reference.VerseStart,
                    Text = "Karena begitu besar kasih Allah",
                }]));

        public Task<IReadOnlyList<VerseHit>> SearchAsync(
            TranslationId? translation, string query, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<VerseHit>>([]);

        public Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct) =>
            Task.FromResult(0);
    }

    private sealed class CollectingLogger : ILogger<CachedVerseRepository>
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings++;
            }
        }
    }
}
```

- [ ] **Step 3: Run it and watch it fail**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter INT_14`
Expected: FAIL to build — `CachedVerseRepository` does not exist.

- [ ] **Step 4: Write the generation counter**

```csharp
namespace ChurchProjection.Infrastructure.Caching;

/// <summary>
/// Bumped when a translation is re-imported, and folded into every cache key so
/// the old entries are unreachable rather than deleted — IDistributedCache has
/// no way to drop a prefix.
///
/// ponytail: in-process counter, correct because the booth runs exactly one
/// server. If a second process ever shares the Redis, move this to a counter
/// stored in the cache itself.
/// </summary>
public sealed class CacheGeneration
{
    private int _current;

    public int Current => Volatile.Read(ref _current);

    public void Bump() => Interlocked.Increment(ref _current);
}
```

- [ ] **Step 5: Write the decorator**

```csharp
using System.Text.Json;

using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ChurchProjection.Infrastructure.Caching;

/// <summary>
/// Caches passage reads. A use case cannot tell this apart from the EF
/// repository, which is the point: the cache is an implementation detail of
/// reading verses, not a thing the application knows about (NFR-REL-09).
///
/// Search is deliberately not cached. The query space is whatever the operator
/// types, so the hit rate would be near zero and every miss would pay for a
/// round trip it did not need.
/// </summary>
public sealed class CachedVerseRepository(
    IVerseRepository inner,
    IDistributedCache cache,
    CacheGeneration generation,
    ILogger<CachedVerseRepository> logger) : IVerseRepository
{
    private static readonly DistributedCacheEntryOptions Options = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
    };

    private static bool _reported;

    public async Task<Passage?> GetAsync(
        TranslationId translation, BibleReference reference, CancellationToken ct)
    {
        var key = $"passage:v{generation.Current}:{translation.Value}:" +
                  $"{reference.BookId}:{reference.Chapter}:{reference.VerseStart}:{reference.VerseEnd}";

        if (await TryReadAsync(key, ct) is { } cached)
        {
            return cached;
        }

        var passage = await inner.GetAsync(translation, reference, ct);

        if (passage is not null)
        {
            await TryWriteAsync(key, passage, ct);
        }

        return passage;
    }

    public Task<IReadOnlyList<VerseHit>> SearchAsync(
        TranslationId? translation, string query, int limit, CancellationToken ct) =>
        inner.SearchAsync(translation, query, limit, ct);

    public async Task<int> ReplaceTranslationAsync(ImportPayload payload, CancellationToken ct)
    {
        var written = await inner.ReplaceTranslationAsync(payload, ct);

        // After the write, so a failed import leaves the cache alone.
        generation.Bump();

        return written;
    }

    private async Task<Passage?> TryReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);

            return bytes is null ? null : JsonSerializer.Deserialize<Passage>(bytes);
        }
        catch (Exception ex)
        {
            ReportOnce(ex);

            return null;
        }
    }

    private async Task TryWriteAsync(string key, Passage passage, CancellationToken ct)
    {
        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(passage), Options, ct);
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private void ReportOnce(Exception ex)
    {
        if (_reported)
        {
            return;
        }

        _reported = true;
        logger.LogWarning(ex, "Cache unavailable; serving verses from the database.");
    }
}
```

`_reported` is static on purpose: the repository is registered per request, so an
instance field would log once per request, which is the noise NFR-REL-09's test
is checking for.

- [ ] **Step 6: Run it**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter INT_14`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ChurchProjection.Infrastructure/Caching tests/ChurchProjection.Api.Tests/CacheFallbackTests.cs
git commit -m "feat: cache verse reads behind a decorator that degrades to the database"
```

---

### Task 10: Api — composition root, configuration, and the error envelope

The first task that turns the four libraries into a running server. Everything
after it adds endpoints to a host that already boots.

**Files:**
- Create: `src/ChurchProjection.Api/Options/StorageOptions.cs`, `CacheOptions.cs`, `AccessOptions.cs`
- Create: `src/ChurchProjection.Api/ApiError.cs`
- Create: `src/ChurchProjection.Api/CompositionRoot.cs`
- Create: `src/ChurchProjection.Infrastructure/Persistence/DevSeed.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`
- Create: `src/ChurchProjection.Api/appsettings.json`, `appsettings.Testing.json`
- Modify: `src/ChurchProjection.Api/ChurchProjection.Api.csproj`

**Interfaces:**
- Consumes: every repository from Tasks 8–9, `ImportService` and `ImportLibrary` from Task 6.
- Produces:
  - `ApiError.Result(int status, string code, string message) -> IResult`
  - `CompositionRoot.AddProjection(this IHostApplicationBuilder builder)`
  - `WebApplication.PrepareDatabaseAsync()`

- [ ] **Step 1: Write the options**

```csharp
// src/ChurchProjection.Api/Options/StorageOptions.cs
namespace ChurchProjection.Api.Options;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Absolute or relative path to the SQLite file. Created on start.</summary>
    public string DatabasePath { get; set; } = "data/projection.db";

    /// <summary>The only directory media is ever read from or written to.</summary>
    public string MediaRoot { get; set; } = "data/media";
}
```

```csharp
// src/ChurchProjection.Api/Options/CacheOptions.cs
namespace ChurchProjection.Api.Options;

public sealed class CacheOptions
{
    public const string Section = "Cache";

    public RedisOptions Redis { get; set; } = new();

    public sealed class RedisOptions
    {
        /// <summary>Null or empty selects the in-process cache (NFR-REL-09).</summary>
        public string? ConnectionString { get; set; }
    }
}
```

```csharp
// src/ChurchProjection.Api/Options/AccessOptions.cs
namespace ChurchProjection.Api.Options;

public sealed class AccessOptions
{
    public const string Section = "Access";

    /// <summary>
    /// Test-only. Pins the PIN so the API suite does not have to read it from
    /// the loopback-only endpoint. Refused in Production.
    /// </summary>
    public string? TestPin { get; set; }

    /// <summary>
    /// Test-only. Switches off the loopback exemption so the pair gate can be
    /// observed to reject. Refused in Production.
    /// </summary>
    public bool RequirePairingFromLoopback { get; set; }

    /// <summary>Pair attempts allowed per remote address per window (NFR-SEC-05).</summary>
    public int PairAttemptsPerWindow { get; set; } = 5;

    public TimeSpan PairWindow { get; set; } = TimeSpan.FromMinutes(1);
}
```

`PairAttemptsPerWindow` defaults to 5 because `tests/api/09-limits/rate-limit-pairing.bru`
sends ten wrong PINs before asserting 429. Any value below ten makes that test
meaningful; raising it above ten silently turns the test green for the wrong
reason.

- [ ] **Step 2: Write the error envelope**

```csharp
// src/ChurchProjection.Api/ApiError.cs
namespace ChurchProjection.Api;

/// <summary>
/// The one shape every non-2xx response takes (API-CONTRACT "Errors"). Messages
/// are written for the volunteer running the service, and never carry a stack
/// trace.
/// </summary>
public sealed record ApiError(ApiError.Body Error)
{
    public sealed record Body(string Code, string Message);

    public static IResult Result(int status, string code, string message) =>
        Results.Json(new ApiError(new Body(code, message)), statusCode: status);

    public static IResult BadRequest(string code, string message) => Result(400, code, message);

    public static IResult NotFound(string code, string message) => Result(404, code, message);
}
```

- [ ] **Step 3: Write the seed**

TEST-PLAN §4.1 requires a database with known content before the Bruno suite
runs, and `local.bru` starts the server with `--environment Testing` rather than
calling a seed script. So the seed belongs to the host, gated on the
environment.

```csharp
// src/ChurchProjection.Infrastructure/Persistence/DevSeed.cs
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Infrastructure.Persistence;

/// <summary>
/// The fixed content the API suite asserts against. Runs only in the Testing
/// environment and only into an empty database, so re-running the suite does
/// not stack duplicates.
/// </summary>
public static class DevSeed
{
    public static async Task ApplyAsync(ProjectionDbContext db, CancellationToken ct)
    {
        if (await db.Translations.AnyAsync(ct))
        {
            return;
        }

        db.Translations.AddRange(
            new Translation { Id = "tb", Abbrev = "TB", Name = "Terjemahan Baru", Language = "id" },
            new Translation { Id = "tl", Abbrev = "TL", Name = "Terjemahan Lama", Language = "id" });

        db.BookNames.AddRange(
            new BookNameRow { TranslationId = "tb", BookId = 1, Name = "Kejadian", Abbrev = "Kej" },
            new BookNameRow { TranslationId = "tl", BookId = 1, Name = "Kejadian", Abbrev = "Kej" },
            new BookNameRow { TranslationId = "tb", BookId = 43, Name = "Yohanes", Abbrev = "Yoh" },
            new BookNameRow { TranslationId = "tl", BookId = 43, Name = "Yahya", Abbrev = "Yah" });

        // Genesis 1:1-3 in both translations, because SYS-BIB-02 and SYS-BIB-04
        // ask for exactly that reference and then require the words to differ.
        // The word "terang" is here because SYS-BIB-05 searches for it.
        db.Verses.AddRange(
            new Verse { TranslationId = "tb", BookId = 1, Chapter = 1, Number = 1, Text = "Pada mulanya Allah menciptakan langit dan bumi" },
            new Verse { TranslationId = "tb", BookId = 1, Chapter = 1, Number = 2, Text = "Bumi belum berbentuk dan kosong, gelap gulita menutupi samudera raya" },
            new Verse { TranslationId = "tb", BookId = 1, Chapter = 1, Number = 3, Text = "Berfirmanlah Allah: Jadilah terang. Lalu terang itu jadi" },
            new Verse { TranslationId = "tl", BookId = 1, Chapter = 1, Number = 1, Text = "Bahwa pada mula pertama dijadikan Allah akan langit dan bumi" },
            new Verse { TranslationId = "tl", BookId = 1, Chapter = 1, Number = 2, Text = "Maka bumi itu lagi campur baur adanya, sunyi senyap" },
            new Verse { TranslationId = "tl", BookId = 1, Chapter = 1, Number = 3, Text = "Maka firman Allah: Hendaklah ada terang, lalu terang itu pun jadilah" },
            new Verse { TranslationId = "tb", BookId = 43, Chapter = 3, Number = 16, Text = "Karena begitu besar kasih Allah akan dunia ini, sehingga Ia telah mengaruniakan Anak-Nya yang tunggal" },
            new Verse { TranslationId = "tb", BookId = 43, Chapter = 3, Number = 17, Text = "Sebab Allah mengutus Anak-Nya ke dalam dunia bukan untuk menghakimi dunia" },
            new Verse { TranslationId = "tl", BookId = 43, Chapter = 3, Number = 16, Text = "Karena demikianlah Allah mengasihi isi dunia ini" });

        // SYS-SNG-01 searches the title for "Kasih"; SYS-SNG-02 searches for
        // "berkesudahan", which appears in the lyrics and in no title — that is
        // what proves the index covers more than titles. SYS-SNG-03 wants a
        // page labelled "Reff" and an author and CCLI number on the song.
        var song = new Song
        {
            Id = "song_seed",
            Title = "Kasih Setia-Mu",
            Author = "Tim Pujian",
            Ccli = "1234567",
            Language = "id",
        };
        song.Pages.Add(new SongPage { Position = 0, SectionLabel = null, Text = "Kasih setia-Mu tak pernah berubah" });
        song.Pages.Add(new SongPage { Position = 1, SectionLabel = "Reff", Text = "Rahmat-Nya tidak berkesudahan, selalu baru setiap pagi" });
        song.Pages.Add(new SongPage { Position = 2, SectionLabel = "Bait 2", Text = "Setiap pagi baru kurasakan" });
        db.Songs.Add(song);

        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Write the composition root**

Every port is bound here, and only here. This file is the whole of the
application's knowledge that EF, Redis, and the file system exist.

```csharp
// src/ChurchProjection.Api/CompositionRoot.cs
using ChurchProjection.Api.Options;
using ChurchProjection.Application.Import;
using ChurchProjection.Application.Ports;
using ChurchProjection.Infrastructure.Caching;
using ChurchProjection.Infrastructure.Import;
using ChurchProjection.Infrastructure.Persistence;
using ChurchProjection.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace ChurchProjection.Api;

public static class CompositionRoot
{
    public static void AddProjection(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        builder.Services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.Section));
        builder.Services.Configure<AccessOptions>(configuration.GetSection(AccessOptions.Section));

        var storage = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
        var cache = configuration.GetSection(CacheOptions.Section).Get<CacheOptions>() ?? new CacheOptions();
        var access = configuration.GetSection(AccessOptions.Section).Get<AccessOptions>() ?? new AccessOptions();

        RefuseTestSettingsInProduction(builder.Environment, access);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(storage.DatabasePath))!);
        Directory.CreateDirectory(storage.MediaRoot);

        builder.Services.AddDbContext<ProjectionDbContext>(options =>
            options.UseSqlite($"Data Source={storage.DatabasePath}"));

        if (string.IsNullOrWhiteSpace(cache.Redis.ConnectionString))
        {
            // INT-13: no cache configured is a supported configuration, not a
            // degraded one. The booth normally runs exactly like this.
            builder.Services.AddDistributedMemoryCache();
        }
        else
        {
            builder.Services.AddStackExchangeRedisCache(options =>
                options.Configuration = cache.Redis.ConnectionString);
        }

        builder.Services.AddSingleton<CacheGeneration>();

        builder.Services.AddScoped<ITranslationRepository, TranslationRepository>();
        builder.Services.AddScoped<ISongRepository, SongRepository>();
        builder.Services.AddScoped<IMediaRepository, MediaRepository>();
        builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
        builder.Services.AddScoped<ILiveStateRepository, LiveStateRepository>();
        builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        // The decorator, not the EF repository, is what the application resolves.
        builder.Services.AddScoped<VerseRepository>();
        builder.Services.AddScoped<IVerseRepository>(provider => new CachedVerseRepository(
            provider.GetRequiredService<VerseRepository>(),
            provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
            provider.GetRequiredService<CacheGeneration>(),
            provider.GetRequiredService<ILogger<CachedVerseRepository>>()));

        builder.Services.AddSingleton<IImportReader>(_ => ImportService.WithDefaultParsers());
        builder.Services.AddScoped<ImportLibrary>();

        // Ticket cookies must survive a restart, or every restart un-pairs the
        // whole team mid-service.
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storage.DatabasePath))!, "keys")));
    }

    /// <summary>
    /// A test convenience that survives into a real start is not a test
    /// convenience, it is a hole. Refusing at composition time means the server
    /// will not start rather than starting wrong.
    /// </summary>
    private static void RefuseTestSettingsInProduction(IHostEnvironment environment, AccessOptions access)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(access.TestPin))
        {
            throw new InvalidOperationException(
                "Access:TestPin is a test-only setting and is refused in Production.");
        }

        if (access.RequirePairingFromLoopback)
        {
            throw new InvalidOperationException(
                "Access:RequirePairingFromLoopback is a test-only setting and is refused in Production.");
        }
    }

    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();

        await db.ApplyMigrationsAsync(CancellationToken.None);

        if (app.Environment.EnvironmentName == "Testing")
        {
            await DevSeed.ApplyAsync(db, CancellationToken.None);
        }
    }
}
```

- [ ] **Step 5: Reference what the Api project now needs**

```xml
<!-- src/ChurchProjection.Api/ChurchProjection.Api.csproj -->
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

The `Microsoft.NET.Sdk.Web` SDK already brings Data Protection and the Redis
cache extension in transitively through Infrastructure; no further package
references belong here.

- [ ] **Step 6: Rewrite Program.cs**

```csharp
using ChurchProjection.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddProjection();

var app = builder.Build();

await app.PrepareDatabaseAsync();

app.MapGet("/healthz", () => Results.Json(new
{
    ok = true,
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
}));

app.Run();

public partial class Program;
```

- [ ] **Step 7: Write the settings files**

```json
// src/ChurchProjection.Api/appsettings.json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "Storage": { "DatabasePath": "data/projection.db", "MediaRoot": "data/media" },
  "Cache": { "Redis": { "ConnectionString": null } },
  "Access": { "PairAttemptsPerWindow": 5, "PairWindow": "00:01:00" }
}
```

```json
// src/ChurchProjection.Api/appsettings.Testing.json
{
  "Logging": { "LogLevel": { "Default": "Warning" } }
}
```

- [ ] **Step 8: Run the host tests**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter "INT_13|INT_14|INT_15|INT_16"`
Expected: PASS.

Then start it by hand once and confirm the seed landed:

```bash
dotnet run --project src/ChurchProjection.Api --environment Testing \
  --Access:TestPin=123456 --Access:RequirePairingFromLoopback=true
```

- [ ] **Step 9: Commit**

```bash
git add src/ChurchProjection.Api src/ChurchProjection.Infrastructure/Persistence/DevSeed.cs
git commit -m "feat: compose the host, bind configuration, and seed the test database"
```

---

### Task 11: Api — pairing, PIN rotation, and the rate limit

**Files:**
- Create: `src/ChurchProjection.Api/Access/PinService.cs`
- Create: `src/ChurchProjection.Api/Access/PairTicket.cs`
- Create: `src/ChurchProjection.Api/Access/PairGate.cs`
- Create: `src/ChurchProjection.Api/Endpoints/AccessEndpoints.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`, `CompositionRoot.cs`
- Create: `tests/ChurchProjection.Api.Tests/AccessTests.cs`

**Interfaces:**
- Consumes: `ISettingsRepository`, `Pin`, `PinRotation` (Tasks 3, 5).
- Produces:
  - `PinService.CurrentAsync(CancellationToken) -> Task<(string Pin, DateTime RotatedAt)>`
  - `PairTicket.Issue(HttpContext, DateTime rotatedAt)` / `PairTicket.IsValid(HttpContext, DateTime rotatedAt)`
  - `RouteHandlerBuilder.RequirePair()` — the extension every later endpoint hangs off.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ChurchProjection.Api.Tests/AccessTests.cs
//
// SYS-SEC-01/02/03 and INT-09/10. The pair gate is the only thing between the
// sanctuary Wi-Fi and the screen behind the pulpit, so it is tested from the
// outside, over HTTP, the way an attacker would meet it.

using System.Net;
using System.Net.Http.Json;

namespace ChurchProjection.Api.Tests;

public class AccessTests(ProjectionAppFactory factory) : IClassFixture<ProjectionAppFactory>
{
    [Fact]
    public async Task SYS_SEC_01_an_unpaired_request_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/translations", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SYS_SEC_02_the_right_pin_opens_the_gate()
    {
        var client = factory.CreateClient();

        var paired = await client.PostAsJsonAsync(
            "/api/pair",
            new { pin = ProjectionAppFactory.TestPin },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, paired.StatusCode);
        Assert.Contains(paired.Headers.GetValues("set-cookie"), value => value.StartsWith("pair="));

        var cookie = paired.Headers.GetValues("set-cookie").First().Split(';')[0];
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/translations");
        request.Headers.Add("Cookie", cookie);

        var listed = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
    }

    [Fact]
    public async Task SYS_SEC_03_the_wrong_pin_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/pair", new { pin = "000000" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(response.Headers, header => header.Key.Equals("set-cookie", StringComparison.OrdinalIgnoreCase));
    }
}
```

`ProjectionAppFactory` sets `Access:RequirePairingFromLoopback=true`, which is
what makes the first test meaningful — every test request arrives from loopback
and would otherwise be exempt.

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter SYS_SEC`
Expected: FAIL — 404 rather than 401, because no route exists yet.

- [ ] **Step 3: Write the PIN service**

```csharp
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Access;

namespace ChurchProjection.Api.Access;

/// <summary>
/// Reads the shared PIN, rotating it lazily when the stored timestamp is older
/// than the most recent Saturday midnight (FR-SEC-03). Lazily, because a weekly
/// scheduler on a machine that is switched off six days a week rotates nothing.
/// </summary>
public sealed class PinService(ISettingsRepository settings, Microsoft.Extensions.Options.IOptions<Options.AccessOptions> access)
{
    private const string PinKey = "pin";
    private const string RotatedAtKey = "pin_rotated_at";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<(string Pin, DateTime RotatedAt)> CurrentAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);

        try
        {
            var stored = await settings.GetAsync(PinKey, ct);
            var rotatedRaw = await settings.GetAsync(RotatedAtKey, ct);
            var rotatedAt = rotatedRaw is null
                ? DateTime.MinValue
                : DateTime.Parse(rotatedRaw, null, System.Globalization.DateTimeStyles.None);

            var now = DateTime.Now;

            if (stored is null || PinRotation.ShouldRotate(rotatedAt, now))
            {
                stored = access.Value.TestPin ?? Pin.Generate();
                rotatedAt = now;

                await settings.SetAsync(PinKey, stored, ct);
                await settings.SetAsync(RotatedAtKey, rotatedAt.ToString("o"), ct);
            }

            return (stored, rotatedAt);
        }
        finally
        {
            Gate.Release();
        }
    }
}
```

`DateTime.Now`, not `UtcNow`: FR-SEC-03 says Saturday, and Saturday is a fact
about the wall clock in the room, not about UTC.

`Access:TestPin` replaces the generated value rather than bypassing rotation, so
the test host exercises the same storage path the booth does.

- [ ] **Step 4: Write the ticket and the gate**

```csharp
// src/ChurchProjection.Api/Access/PairTicket.cs
using Microsoft.AspNetCore.DataProtection;

namespace ChurchProjection.Api.Access;

/// <summary>
/// The pair cookie. Its payload is the PIN's rotation timestamp, so rotating the
/// PIN invalidates every ticket issued before it (FR-SEC-06) without keeping a
/// server-side session list.
/// </summary>
public static class PairTicket
{
    public const string CookieName = "pair";

    private const string Purpose = "church-projection.pair.v1";

    public static void Issue(HttpContext context, DateTime rotatedAt)
    {
        var protector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

        context.Response.Cookies.Append(CookieName, protector.Protect(rotatedAt.ToString("o")), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,

            // Not Secure: this is plain HTTP on the LAN by design. See the
            // accepted risk in the design document.
            Secure = false,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(7),
        });
    }

    public static bool IsValid(HttpContext context, DateTime rotatedAt)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) || string.IsNullOrEmpty(cookie))
        {
            return false;
        }

        var protector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

        try
        {
            return DateTime.Parse(
                protector.Unprotect(cookie), null, System.Globalization.DateTimeStyles.None) == rotatedAt;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}
```

```csharp
// src/ChurchProjection.Api/Access/PairGate.cs
using ChurchProjection.Api.Options;

using Microsoft.Extensions.Options;

namespace ChurchProjection.Api.Access;

public static class PairGate
{
    /// <summary>
    /// Applied to every route except health, pair, and the output hub role.
    /// A filter rather than middleware so the exemptions are visible at the
    /// route that has them, instead of in a path list somewhere else.
    /// </summary>
    public static TBuilder RequirePair<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;

            if (await IsPairedAsync(http))
            {
                return await next(context);
            }

            return ApiError.Result(401, "NOT_PAIRED", "Enter the PIN shown on the booth screen.");
        });

        return builder;
    }

    public static async Task<bool> IsPairedAsync(HttpContext http)
    {
        var access = http.RequestServices.GetRequiredService<IOptions<AccessOptions>>().Value;

        // FR-SEC-08: the booth's own browser never types the PIN. The test
        // suite switches this off so the gate can be observed to reject.
        if (!access.RequirePairingFromLoopback && IsLoopback(http))
        {
            return true;
        }

        var pin = http.RequestServices.GetRequiredService<PinService>();
        var (_, rotatedAt) = await pin.CurrentAsync(http.RequestAborted);

        return PairTicket.IsValid(http, rotatedAt);
    }

    public static bool IsLoopback(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);
}
```

- [ ] **Step 5: Write the endpoints**

```csharp
// src/ChurchProjection.Api/Endpoints/AccessEndpoints.cs
using ChurchProjection.Api.Access;

namespace ChurchProjection.Api.Endpoints;

public static class AccessEndpoints
{
    public sealed record PairRequest(string? Pin);

    public static void MapAccess(this WebApplication app)
    {
        app.MapPost("/api/pair", async (PairRequest body, HttpContext http, PinService pins, CancellationToken ct) =>
        {
            var (pin, rotatedAt) = await pins.CurrentAsync(ct);

            if (string.IsNullOrWhiteSpace(body.Pin) || !FixedTimeEquals(body.Pin, pin))
            {
                return ApiError.Result(401, "BAD_PIN", "That PIN is not the one on the booth screen.");
            }

            PairTicket.Issue(http, rotatedAt);

            return Results.NoContent();
        })
        .RequireRateLimiting("pair");

        app.MapGet("/api/pin", async (HttpContext http, PinService pins, CancellationToken ct) =>
        {
            // FR-SEC-09. The PIN is readable only by someone already at the
            // booth machine; from anywhere else this route does not exist as
            // far as the caller is concerned.
            if (!PairGate.IsLoopback(http))
            {
                return ApiError.Result(403, "LOOPBACK_ONLY", "The PIN can only be read on the booth machine.");
            }

            var (pin, rotatedAt) = await pins.CurrentAsync(ct);

            return Results.Json(new { pin, rotatedAt });
        });
    }

    /// <summary>
    /// Constant-time on purpose. The PIN is six digits and the attacker is on
    /// the same LAN; there is no reason to hand them a timing signal as well.
    /// </summary>
    private static bool FixedTimeEquals(string left, string right) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(left), System.Text.Encoding.UTF8.GetBytes(right));
}
```

- [ ] **Step 6: Register the service, the limiter, and the routes**

In `CompositionRoot.AddProjection`, after the repository registrations:

```csharp
        builder.Services.AddScoped<Access.PinService>();

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.AddFixedWindowLimiter("pair", window =>
            {
                window.PermitLimit = access.PairAttemptsPerWindow;
                window.Window = access.PairWindow;
                window.QueueLimit = 0;
            });

            limiter.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)access.PairWindow.TotalSeconds).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ApiError(new ApiError.Body("TOO_MANY_ATTEMPTS", "Too many PIN attempts. Wait a minute.")),
                    ct);
            };
        });
```

`Retry-After` is set explicitly because `rate-limit-pairing.bru` asserts the
header is present and is a string; the framework does not add it for a fixed
window on its own.

The limiter partitions by remote address by default for a named policy attached
to a route, which is what NFR-SEC-05 asks for: one phone guessing must not lock
out the operator's tablet.

In `Program.cs`, between `PrepareDatabaseAsync` and `MapGet("/healthz")`:

```csharp
app.UseRateLimiter();
app.MapAccess();
```

with `using ChurchProjection.Api.Endpoints;` at the top.

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/ChurchProjection.Api.Tests --filter SYS_SEC`
Expected: PASS, 3 tests.

Then the Bruno slice that only needs pairing:

```bash
npx @usebruno/cli run tests/api/02-access --env local
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/ChurchProjection.Api tests/ChurchProjection.Api.Tests/AccessTests.cs
git commit -m "feat: gate the API behind a rotating shared PIN"
```

---

### Task 12: Api — Bible endpoints

The first read surface. From here on, each task's gate is a Bruno folder, because
these routes exist to be called by a browser and Bruno calls them the same way.

**Files:**
- Create: `src/ChurchProjection.Api/Endpoints/BibleEndpoints.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`

**Interfaces:**
- Consumes: `ITranslationRepository`, `IVerseRepository`, `BibleReference.TryParse`.
- Produces: `WebApplication.MapBible()`.

- [ ] **Step 1: Write the endpoints**

```csharp
using ChurchProjection.Api.Access;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Api.Endpoints;

public static class BibleEndpoints
{
    private const int SearchCap = 100;

    public static void MapBible(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequirePair();

        group.MapGet("/translations", async (ITranslationRepository translations, CancellationToken ct) =>
        {
            var all = await translations.ListAsync(ct);

            return Results.Json(all.Select(t => new
            {
                id = t.Id.Value,
                abbrev = t.Abbrev,
                name = t.Name,
                language = t.Language,
            }));
        });

        group.MapGet("/bible/reference", (string? q) =>
        {
            // 404 rather than 400: the operator types into this field one
            // character at a time and most prefixes are not references yet.
            // A 400 storm in the log hides the failures that matter.
            if (BibleReference.TryParse(q ?? string.Empty) is not { } reference)
            {
                return ApiError.NotFound(
                    "UNPARSEABLE_REFERENCE", $"'{q}' is not a book, chapter and verse.");
            }

            return Results.Json(new
            {
                bookId = reference.BookId,
                chapter = reference.Chapter,
                verseStart = reference.VerseStart,
                verseEnd = reference.VerseEnd,
            });
        });

        group.MapGet("/bible/passage", async (
            string translationId,
            int bookId,
            int chapter,
            int? verseStart,
            int? verseEnd,
            IVerseRepository verses,
            CancellationToken ct) =>
        {
            var reference = new BibleReference(bookId, chapter, verseStart ?? 1, verseEnd);

            var passage = await verses.GetAsync(new TranslationId(translationId), reference, ct);

            if (passage is null)
            {
                return ApiError.NotFound(
                    "PASSAGE_NOT_FOUND", "That passage is not in this translation.");
            }

            return Results.Json(new
            {
                translationId = passage.TranslationId.Value,
                bookId = passage.BookId,
                bookName = passage.BookName,
                chapter = passage.Chapter,
                verses = passage.Verses.Select(v => new { verse = v.Number, text = v.Text }),
            });
        });

        group.MapGet("/bible/search", async (
            string? q, string? translationId, IVerseRepository verses, CancellationToken ct) =>
        {
            var translation = string.IsNullOrWhiteSpace(translationId)
                ? (TranslationId?)null
                : new TranslationId(translationId);

            var results = await verses.SearchAsync(translation, q ?? string.Empty, SearchCap, ct);

            return Results.Json(new { results });
        });
    }
}
```

`VerseHit` is already `(string TranslationId, int BookId, string BookName, int Chapter, int Verse, string Text)`,
which is the response shape SYS-BIB-05 asserts, so it is returned directly rather
than re-projected into an anonymous type that could drift from it.

- [ ] **Step 2: Map it**

In `Program.cs`, after `app.MapAccess();`:

```csharp
app.MapBible();
```

- [ ] **Step 3: Run the Bruno folder**

```bash
dotnet run --project src/ChurchProjection.Api --environment Testing \
  --Access:TestPin=123456 --Access:RequirePairingFromLoopback=true &
npx @usebruno/cli run tests/api/02-access tests/api/03-bible --env local
```

Expected: PASS. The access folder runs first because `03-bible` needs the cookie
it sets.

If SYS-BIB-04 fails on identical text between translations, the seed is wrong,
not the endpoint — `tl` and `tb` must not share wording for Genesis 1:1-3.

- [ ] **Step 4: Commit**

```bash
git add src/ChurchProjection.Api
git commit -m "feat: serve translations, references, passages, and verse search"
```

---

### Task 13: Api — song endpoints

**Files:**
- Create: `src/ChurchProjection.Api/Endpoints/SongEndpoints.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`

**Interfaces:**
- Consumes: `ISongRepository`.
- Produces: `WebApplication.MapSongs()`.

- [ ] **Step 1: Write the endpoints**

```csharp
using ChurchProjection.Api.Access;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;

namespace ChurchProjection.Api.Endpoints;

public static class SongEndpoints
{
    private const int SearchCap = 100;

    public static void MapSongs(this WebApplication app)
    {
        var group = app.MapGroup("/api/songs").RequirePair();

        group.MapGet("/", async (string? q, ISongRepository songs, CancellationToken ct) =>
        {
            var results = await songs.SearchAsync(q ?? string.Empty, SearchCap, ct);

            return Results.Json(new { results });
        });

        group.MapGet("/{id}", async (string id, ISongRepository songs, CancellationToken ct) =>
        {
            var song = await songs.FindAsync(new SongId(id), ct);

            if (song is null)
            {
                return ApiError.NotFound("SONG_NOT_FOUND", "That song is not in the library.");
            }

            return Results.Json(new
            {
                id = song.Id.Value,
                title = song.Title,
                author = song.Author,
                ccli = song.Ccli,
                language = song.Language,
                pages = song.Pages
                    .OrderBy(page => page.Position)
                    .Select(page => new { position = page.Position, sectionLabel = page.SectionLabel, text = page.Text }),
            });
        });
    }
}
```

`author` and `ccli` are serialised even when null, because SYS-SNG-03 asserts the
keys are present. The default web JSON options keep nulls, so nothing further is
needed — but do not add `DefaultIgnoreCondition = WhenWritingNull` to the host's
JSON options or that test starts failing for a reason nobody will guess.

- [ ] **Step 2: Map it**

```csharp
app.MapSongs();
```

- [ ] **Step 3: Run the Bruno folder**

```bash
npx @usebruno/cli run tests/api/02-access tests/api/04-songs --env local
```

Expected: PASS. SYS-SNG-02 searching for `berkesudahan` is the one that proves
`songs_fts` indexes lyrics and not only titles.

- [ ] **Step 4: Commit**

```bash
git add src/ChurchProjection.Api
git commit -m "feat: serve song search and song pages"
```

---

### Task 14: Api — the import endpoint

FR-IMP-05 through FR-IMP-07: a rejected file changes nothing. The Bruno folder
snapshots the library, posts a malformed file, and compares the library to the
snapshot — so this task is judged on what it does not do.

**Files:**
- Create: `src/ChurchProjection.Api/Endpoints/ImportEndpoints.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`

**Interfaces:**
- Consumes: `ImportLibrary.ExecuteAsync(Stream, string fileName, CancellationToken) -> Task<ImportOutcome>`, `ImportException`.
- Produces: `WebApplication.MapImport()`.

- [ ] **Step 1: Write the endpoint**

```csharp
using ChurchProjection.Api.Access;
using ChurchProjection.Application.Import;

namespace ChurchProjection.Api.Endpoints;

public static class ImportEndpoints
{
    /// <summary>NFR-SEC-06. A Bible is a few megabytes; a hundred is an attack.</summary>
    private const long MaxUploadBytes = 100L * 1024 * 1024;

    public static void MapImport(this WebApplication app)
    {
        app.MapPost("/api/import", async (HttpRequest request, ImportLibrary import, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return ApiError.BadRequest("NOT_MULTIPART", "Send the file as multipart/form-data.");
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
            {
                return ApiError.BadRequest("NO_FILE", "No file was attached to the import.");
            }

            if (file.Length > MaxUploadBytes)
            {
                return ApiError.Result(413, "FILE_TOO_LARGE", "That file is larger than the import limit.");
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var outcome = await import.ExecuteAsync(stream, file.FileName, ct);

                return Results.Json(new
                {
                    kind = outcome.Kind,
                    imported = outcome.Imported,
                    updated = outcome.Updated,
                });
            }
            catch (ImportException ex)
            {
                // The message names the record that failed, because "Import
                // failed" is not something to hand a volunteer at nine o'clock
                // on a Saturday night (FR-ADM-02).
                return ApiError.Result(422, "IMPORT_REJECTED", ex.Detail);
            }
        })
        .RequirePair()
        .DisableAntiforgery();
    }
}
```

`ImportLibrary` parses before it opens the transaction, so an `ImportException`
is thrown with nothing written — the atomicity SYS-IMP-02 checks for is a
property of that ordering, not of this catch block.

- [ ] **Step 2: Map it**

```csharp
app.MapImport();
```

- [ ] **Step 3: Run the Bruno folder**

```bash
npx @usebruno/cli run tests/api/02-access tests/api/05-import --env local
```

Expected: PASS, including SYS-IMP-03 — importing `song-openlyrics.xml` a second
time must report `updated: 1, imported: 0`, which is `ImportLibrary`'s
match-by-title upsert doing its job.

If the second import reports `imported: 1`, `FindByTitleAsync` is comparing case
sensitively or the first import never committed.

- [ ] **Step 4: Commit**

```bash
git add src/ChurchProjection.Api
git commit -m "feat: accept library imports and reject malformed files whole"
```

---

### Task 15: Api — service order endpoints

**Files:**
- Create: `src/ChurchProjection.Api/Endpoints/ServiceEndpoints.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`

**Interfaces:**
- Consumes: `IServiceRepository`, `ServicePlan`, `ServiceItem`, `ItemRef`.
- Produces: `WebApplication.MapServices()`, `ServiceEndpoints.ItemDto` used by Task 17's live payload resolver.

- [ ] **Step 1: Write the endpoints**

```csharp
using ChurchProjection.Api.Access;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Api.Endpoints;

public static class ServiceEndpoints
{
    public sealed record CreateServiceRequest(string Name, DateOnly ServiceDate);

    public sealed record PatchServiceRequest(string? Name, DateOnly? ServiceDate);

    public sealed record ItemRequest(string Kind, string Label, ItemRef Ref);

    public sealed record PatchItemRequest(string? Label, ItemRef? Ref);

    public sealed record ReorderRequest(IReadOnlyList<string> ItemIds);

    private static readonly string[] Kinds = ["bible", "song", "slide", "media", "countdown"];

    public static void MapServices(this WebApplication app)
    {
        var group = app.MapGroup("/api/services").RequirePair();

        group.MapGet("/", async (IServiceRepository services, CancellationToken ct) =>
            Results.Json(await services.ListAsync(ct)));

        group.MapPost("/", async (
            CreateServiceRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
            {
                return ApiError.BadRequest("NAME_REQUIRED", "Give the service a name.");
            }

            var plan = new ServicePlan
            {
                Id = $"svc_{Guid.NewGuid():n}"[..12],
                Name = body.Name,
                ServiceDate = body.ServiceDate,
            };

            await services.SaveAsync(plan, ct);

            return Results.Created($"/api/services/{plan.Id.Value}", Describe(plan));
        });

        group.MapGet("/{id}", async (string id, IServiceRepository services, CancellationToken ct) =>
            await services.FindAsync(new ServiceId(id), ct) is { } plan
                ? Results.Json(Describe(plan))
                : NoSuchService());

        group.MapPatch("/{id}", async (
            string id, PatchServiceRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            plan.Rename(body.Name ?? plan.Name, body.ServiceDate ?? plan.ServiceDate);
            await services.SaveAsync(plan, ct);

            return Results.Json(Describe(plan));
        });

        group.MapDelete("/{id}", async (string id, IServiceRepository services, CancellationToken ct) =>
        {
            await services.RemoveAsync(new ServiceId(id), ct);

            return Results.NoContent();
        });

        group.MapPost("/{id}/items", async (
            string id, ItemRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            if (!Kinds.Contains(body.Kind))
            {
                return ApiError.BadRequest("UNKNOWN_KIND", $"'{body.Kind}' is not a kind of service item.");
            }

            var item = new ServiceItem
            {
                Id = $"itm_{Guid.NewGuid():n}"[..12],
                Kind = body.Kind,
                Label = body.Label,
                Ref = body.Ref,
            };

            plan.Append(item);
            await services.SaveAsync(plan, ct);

            return Results.Created($"/api/services/{id}/items/{item.Id}", Describe(item));
        });

        group.MapPatch("/{id}/items/{itemId}", async (
            string id, string itemId, PatchItemRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            if (plan.Find(itemId) is not { } item)
            {
                return ApiError.NotFound("UNKNOWN_ITEM", "That item is not in this service.");
            }

            item.Update(body.Label ?? item.Label, body.Ref ?? item.Ref);
            await services.SaveAsync(plan, ct);

            return Results.Json(Describe(item));
        });

        group.MapDelete("/{id}/items/{itemId}", async (
            string id, string itemId, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            // FR-SVC-07: removing an item removes the item. The song it points
            // at stays in the library, which is what song-still-exists.bru
            // checks immediately afterwards.
            plan.Remove(itemId);
            await services.SaveAsync(plan, ct);

            return Results.NoContent();
        });

        group.MapPost("/{id}/items/reorder", async (
            string id, ReorderRequest body, IServiceRepository services, CancellationToken ct) =>
        {
            if (await services.FindAsync(new ServiceId(id), ct) is not { } plan)
            {
                return NoSuchService();
            }

            if (!plan.Reorder(body.ItemIds))
            {
                // The aggregate refused and changed nothing, so there is
                // nothing to roll back here.
                return ApiError.BadRequest(
                    "INCOMPLETE_ORDER", "The new order must list every item in the service exactly once.");
            }

            await services.SaveAsync(plan, ct);

            return Results.Json(Describe(plan));
        });
    }

    private static IResult NoSuchService() =>
        ApiError.NotFound("SERVICE_NOT_FOUND", "That service is not saved on this machine.");

    private static object Describe(ServicePlan plan) => new
    {
        id = plan.Id.Value,
        name = plan.Name,
        serviceDate = plan.ServiceDate,
        items = plan.Items.OrderBy(item => item.Position).Select(Describe),
    };

    private static object Describe(ServiceItem item) => new
    {
        id = item.Id.Value,
        kind = item.Kind,
        label = item.Label,
        position = item.Position,
        @ref = item.Ref,
    };
}
```

`Describe(ServiceItem)` emits `position`, which `add-verse-item.bru` asserts is
`1` for the second item added — positions are zero-based and assigned by
`ServicePlan.Append`, never by the caller.

- [ ] **Step 2: Add the two aggregate methods this task assumes**

`ServicePlan.Rename` and `ServiceItem.Update` were not written in Task 5. Add
them beside their siblings:

```csharp
// src/ChurchProjection.Domain/Services/ServicePlan.cs
public void Rename(string name, DateOnly serviceDate)
{
    Name = name;
    ServiceDate = serviceDate;
}
```

```csharp
// src/ChurchProjection.Domain/Services/ServiceItem.cs
public void Update(string label, ItemRef reference)
{
    Label = label;
    Ref = reference;
}
```

Both properties need `private set` rather than `init` for this to compile.

- [ ] **Step 3: Map it**

```csharp
app.MapServices();
```

- [ ] **Step 4: Run the Bruno folder**

```bash
npx @usebruno/cli run tests/api/02-access tests/api/06-services --env local
```

Expected: PASS, including `reorder-incomplete.bru` (400 `INCOMPLETE_ORDER`) and
`order-unchanged-after-rejection.bru`, which re-reads the service to prove the
refusal wrote nothing.

- [ ] **Step 5: Commit**

```bash
git add src/ChurchProjection.Api src/ChurchProjection.Domain
git commit -m "feat: build and reorder service orders"
```

---

### Task 16: Api — media endpoints

**Files:**
- Create: `src/ChurchProjection.Api/Endpoints/MediaEndpoints.cs`
- Create: `src/ChurchProjection.Api/Media/MediaPaths.cs`
- Modify: `src/ChurchProjection.Infrastructure/Persistence/DevSeed.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`
- Modify: `docs/requirements/API-CONTRACT.md`

**Interfaces:**
- Consumes: `IMediaRepository`, `StorageOptions.MediaRoot`.
- Produces: `WebApplication.MapMedia()`, `MediaPaths.Resolve(string mediaRoot, string filename) -> string?`.

- [ ] **Step 1: Correct the contract**

`API-CONTRACT.md` describes `GET /api/media/:id/file` returning a bare array from
`GET /api/media`. The Bruno suite calls `/api/media/:id/stream`, expects
`{ results: [...] }`, and also calls `GET /api/media/:id` for a single item.
Three tests against one table: the table is what is wrong. Replace the Media rows
in `API-CONTRACT.md` with:

```markdown
| GET | `/api/media` | paired | `{ results: [{ id, kind, filename, durationMs, width, height, available }] }`. `available` is false when the file is missing or unreadable (`FR-LIB-23`). |
| GET | `/api/media/:id` | paired | One media item in the same shape. 404 `MEDIA_NOT_FOUND`. |
| GET | `/api/media/:id/stream` | paired | The binary. Supports `Range` (`IF-SW-03`). 404 `MEDIA_FILE_MISSING` when the row exists but the file does not. |
| POST | `/api/media` | paired | `multipart/form-data`, field `file`. Filename sanitised; paths resolving outside the media directory are rejected 400 `BAD_FILENAME` (`NFR-SEC-04`). |
```

- [ ] **Step 2: Write the path guard**

```csharp
// src/ChurchProjection.Api/Media/MediaPaths.cs
namespace ChurchProjection.Api.Media;

public static class MediaPaths
{
    /// <summary>
    /// Resolves a stored filename inside the media root, or null if the result
    /// escapes it. Containment is checked on the fully resolved path rather than
    /// by looking for "..", because there are more ways out of a directory than
    /// two dots — a symlink and an absolute path are two of them.
    /// </summary>
    public static string? Resolve(string mediaRoot, string filename)
    {
        var root = Path.GetFullPath(mediaRoot);
        var full = Path.GetFullPath(Path.Combine(root, filename));

        var rooted = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        return full.StartsWith(rooted, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    /// <summary>Strips every directory component from an uploaded name.</summary>
    public static string Sanitise(string filename)
    {
        var bare = Path.GetFileName(filename.Replace('\\', '/'));

        return string.Join('_', bare.Split(Path.GetInvalidFileNameChars()));
    }
}
```

- [ ] **Step 3: Write the endpoints**

```csharp
using ChurchProjection.Api.Access;
using ChurchProjection.Api.Media;
using ChurchProjection.Api.Options;
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Library;

using Microsoft.Extensions.Options;

namespace ChurchProjection.Api.Endpoints;

public static class MediaEndpoints
{
    private const long MaxUploadBytes = 500L * 1024 * 1024;

    public static void MapMedia(this WebApplication app)
    {
        var group = app.MapGroup("/api/media").RequirePair();

        group.MapGet("/", async (
            IMediaRepository media, IOptions<StorageOptions> storage, CancellationToken ct) =>
        {
            var all = await media.ListAsync(ct);

            return Results.Json(new { results = all.Select(item => Describe(item, storage.Value.MediaRoot)) });
        });

        group.MapGet("/{id}", async (
            string id, IMediaRepository media, IOptions<StorageOptions> storage, CancellationToken ct) =>
        {
            var item = await media.FindAsync(new MediaId(id), ct);

            return item is null
                ? ApiError.NotFound("MEDIA_NOT_FOUND", "That media item is not in the library.")
                : Results.Json(Describe(item, storage.Value.MediaRoot));
        });

        group.MapGet("/{id}/stream", async (
            string id, IMediaRepository media, IOptions<StorageOptions> storage, CancellationToken ct) =>
        {
            var item = await media.FindAsync(new MediaId(id), ct);

            if (item is null)
            {
                return ApiError.NotFound("MEDIA_NOT_FOUND", "That media item is not in the library.");
            }

            // The caller names a database id, never a path. Resolve is a second
            // line of defence in case a row's stored filename is ever wrong.
            if (MediaPaths.Resolve(storage.Value.MediaRoot, item.Filename) is not { } path || !File.Exists(path))
            {
                return ApiError.NotFound("MEDIA_FILE_MISSING", $"'{item.Filename}' is not in the media folder.");
            }

            // enableRangeProcessing is what makes the projector's video element
            // able to seek instead of buffering the whole file first.
            return Results.File(path, item.Kind, enableRangeProcessing: true);
        });

        group.MapPost("/", async (
            HttpRequest request,
            IMediaRepository media,
            IOptions<StorageOptions> storage,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return ApiError.BadRequest("NOT_MULTIPART", "Send the file as multipart/form-data.");
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
            {
                return ApiError.BadRequest("NO_FILE", "No file was attached.");
            }

            if (file.Length > MaxUploadBytes)
            {
                return ApiError.Result(413, "FILE_TOO_LARGE", "That file is larger than the upload limit.");
            }

            var name = MediaPaths.Sanitise(file.FileName);

            if (string.IsNullOrWhiteSpace(name) ||
                MediaPaths.Resolve(storage.Value.MediaRoot, name) is not { } destination)
            {
                return ApiError.BadRequest("BAD_FILENAME", "That filename cannot be stored.");
            }

            try
            {
                await using (var target = File.Create(destination))
                {
                    await file.CopyToAsync(target, ct);
                }
            }
            catch (IOException)
            {
                // INT-05: a full disk must not leave a half file behind with a
                // row pointing at it.
                File.Delete(destination);

                return ApiError.Result(507, "STORAGE_FULL", "There is not enough space to store that file.");
            }

            var item = new MediaItem
            {
                Id = $"med_{Guid.NewGuid():n}"[..12],
                Kind = file.ContentType,
                Filename = name,
                Path = destination,
            };

            await media.AddAsync(item, ct);

            return Results.Created($"/api/media/{item.Id.Value}", Describe(item, storage.Value.MediaRoot));
        })
        .DisableAntiforgery();
    }

    private static object Describe(MediaItem item, string mediaRoot) => new
    {
        id = item.Id.Value,
        kind = item.Kind,
        filename = item.Filename,
        durationMs = item.DurationMs,
        width = item.Width,
        height = item.Height,

        // Checked on every read rather than stored, because the failure this
        // guards against is someone tidying the media folder between Saturday
        // and Sunday (FR-LIB-23).
        available = MediaPaths.Resolve(mediaRoot, item.Filename) is { } path && File.Exists(path),
    };
}
```

- [ ] **Step 4: Seed the two media rows the suite needs**

`list-media.bru` requires one row whose file exists and one whose file does not.
Append to `DevSeed.ApplyAsync`, after the song:

```csharp
        // SYS-MED-00 through SYS-MED-03 need both halves of the media failure:
        // a file that is there, and a row whose file was moved. The present file
        // is written here so the fixtures folder does not need a binary, and it
        // is deliberately larger than the 1 KiB range SYS-MED-01 asks for.
        var mediaRoot = Path.GetDirectoryName(db.Database.GetDbConnection().DataSource)!;
        mediaRoot = Path.Combine(mediaRoot, "media");
        Directory.CreateDirectory(mediaRoot);

        var presentPath = Path.Combine(mediaRoot, "seed-clip.bin");
        await File.WriteAllBytesAsync(presentPath, new byte[64 * 1024], ct);

        db.Media.AddRange(
            new MediaItem
            {
                Id = "med_present",
                Kind = "video/mp4",
                Filename = "seed-clip.bin",
                Path = presentPath,
                DurationMs = 12_000,
                Width = 1920,
                Height = 1080,
            },
            new MediaItem
            {
                Id = "med_missing",
                Kind = "image/jpeg",
                Filename = "moved-by-someone.jpg",
                Path = Path.Combine(mediaRoot, "moved-by-someone.jpg"),
            });
```

Add `using Microsoft.EntityFrameworkCore;` if it is not already there — the seed
derives the media folder from the database file's own location so it works both
under `ProjectionAppFactory`'s temp directory and under a plain `dotnet run`.

- [ ] **Step 5: Map it**

```csharp
app.MapMedia();
```

- [ ] **Step 6: Run the Bruno folder**

```bash
npx @usebruno/cli run tests/api/02-access tests/api/08-media --env local
```

Expected: PASS. SYS-MED-01 asserts `content-length` is exactly 1024 for
`Range: bytes=0-1023`; SYS-MED-02 sends `..%2f..%2f..%2fetc%2fpasswd` as the id
and must get 400 or 404 with no file content in the body — it will 404 at the
repository lookup, before any path is built, which is the behaviour that
requirement wants.

- [ ] **Step 7: Commit**

```bash
git add src/ChurchProjection.Api src/ChurchProjection.Infrastructure docs/requirements/API-CONTRACT.md
git commit -m "feat: list, stream, and upload media with the folder as a hard boundary"
```

---

### Task 17: Application and Api — the live channel

The reason the rest exists. The server is the single authority on what is on the
screen: clients send commands and receive whole states, and a client that
disagrees with the server is wrong.

**Files:**
- Create: `src/ChurchProjection.Application/Live/LiveCommand.cs`
- Create: `src/ChurchProjection.Application/Live/ServiceOrderView.cs`
- Create: `src/ChurchProjection.Application/Live/EmptyOrder.cs`
- Create: `src/ChurchProjection.Application/Live/ContentResolver.cs`
- Create: `src/ChurchProjection.Application/Live/LiveCommandHandler.cs`
- Create: `src/ChurchProjection.Api/Live/LiveHub.cs`
- Create: `src/ChurchProjection.Api/Live/OutputCounter.cs`
- Create: `src/ChurchProjection.Api/Live/LiveStateDto.cs`
- Create: `src/ChurchProjection.Api/Endpoints/LiveEndpoints.cs`
- Modify: `src/ChurchProjection.Application/Ports/IServiceRepository.cs`, `IMediaRepository.cs`
- Modify: `src/ChurchProjection.Infrastructure/Repositories/ServiceRepository.cs`, `MediaRepository.cs`
- Modify: `src/ChurchProjection.Api/Program.cs`, `CompositionRoot.cs`

**Interfaces:**
- Consumes: `LiveSession`, `LiveSnapshot`, `LiveResult`, `RefusalCode`, `Slot`, `IServiceOrder` (Task 4); every read port.
- Produces:
  - `LiveCommandHandler.CurrentAsync(CancellationToken) -> Task<LiveView>`
  - `LiveCommandHandler.ExecuteAsync(LiveCommand, CancellationToken) -> Task<(LiveResult Result, LiveView View)>`
  - `LiveView(LiveSnapshot Snapshot, object? LiveContent, object? PreviewContent)`

- [ ] **Step 1: Add the two port methods this needs**

```csharp
// src/ChurchProjection.Application/Ports/IServiceRepository.cs — add:

/// <summary>
/// The service that holds this item. The live state attaches itself to a
/// service when the operator previews something in it, so there is no separate
/// "open the service" step to forget.
/// </summary>
Task<ServicePlan?> FindByItemAsync(ItemId itemId, CancellationToken ct);
```

```csharp
// src/ChurchProjection.Infrastructure/Repositories/ServiceRepository.cs — add:

public Task<ServicePlan?> FindByItemAsync(ItemId itemId, CancellationToken ct) =>
    db.Services.SingleOrDefaultAsync(s => s.Items.Any(item => item.Id == itemId), ct);
```

```csharp
// src/ChurchProjection.Application/Ports/IMediaRepository.cs — add:

/// <summary>Whether the file behind this row is on disk right now (FR-LIV-17).</summary>
Task<bool> IsAvailableAsync(MediaId id, CancellationToken ct);
```

```csharp
// src/ChurchProjection.Infrastructure/Repositories/MediaRepository.cs — add:

public async Task<bool> IsAvailableAsync(MediaId id, CancellationToken ct) =>
    await FindAsync(id, ct) is { } item && File.Exists(item.Path);
```

- [ ] **Step 2: Write the command**

```csharp
// src/ChurchProjection.Application/Live/LiveCommand.cs
namespace ChurchProjection.Application.Live;

/// <summary>
/// One operator action. The same object arrives over the hub and over HTTP —
/// the control view uses the socket, the tests and a recovering client use the
/// endpoint, and neither gets a different set of rules.
/// </summary>
public sealed record LiveCommand(string? Type, string? ItemId, int? PageIndex, bool? On);
```

- [ ] **Step 3: Write the service-order view**

```csharp
// src/ChurchProjection.Application/Live/ServiceOrderView.cs
using ChurchProjection.Domain.Live;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Live;

/// <summary>
/// The Domain's read-only window onto a saved service. LiveSession asks it three
/// questions and nothing more, which is what keeps the aggregate testable
/// without a database (UNT-LIV-19 enforces the shape by reflection).
/// </summary>
public sealed class ServiceOrderView(ServicePlan plan, IReadOnlyDictionary<string, int> pageCounts) : IServiceOrder
{
    public bool Contains(ItemId itemId) => plan.Find(itemId.Value) is not null;

    public bool MediaAvailable(ItemId itemId) => !Unavailable.Contains(itemId.Value);

    public int PageCount(ItemId itemId) =>
        pageCounts.TryGetValue(itemId.Value, out var count) ? count : 1;

    /// <summary>Item ids whose media file was missing when the order was built.</summary>
    public required IReadOnlySet<string> Unavailable { get; init; }

    public ServicePlan Plan => plan;
}
```

- [ ] **Step 4: Write the content resolver**

```csharp
// src/ChurchProjection.Application/Live/ContentResolver.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Bible;
using ChurchProjection.Domain.Library;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Live;

/// <summary>
/// Turns an item and a page number into the words the output view paints. This
/// travels inside the state so the projector never has to ask a second question
/// — one round trip, and no window where the state says one thing and the
/// screen shows another (FR-LIV-11).
/// </summary>
public sealed class ContentResolver(
    ISongRepository songs, IVerseRepository verses, IMediaRepository media)
{
    public async Task<int> PageCountAsync(ServiceItem item, CancellationToken ct) => item.Kind switch
    {
        "song" when item.Ref.SongId is { } id =>
            await songs.FindAsync(new SongId(id), ct) is { } song ? Math.Max(song.Pages.Count, 1) : 1,
        _ => 1,
    };

    public async Task<object?> ResolveAsync(ServiceItem item, int pageIndex, CancellationToken ct)
    {
        switch (item.Kind)
        {
            case "song" when item.Ref.SongId is { } songId:
            {
                var song = await songs.FindAsync(new SongId(songId), ct);
                var page = song?.Pages.OrderBy(p => p.Position).ElementAtOrDefault(pageIndex);

                return page is null
                    ? null
                    : new { kind = "song", title = song!.Title, sectionLabel = page.SectionLabel, text = page.Text };
            }

            case "bible" when item.Ref.TranslationId is { } translationId
                              && item.Ref.BookId is { } bookId
                              && item.Ref.Chapter is { } chapter:
            {
                var reference = new BibleReference(
                    bookId, chapter, item.Ref.VerseStart ?? 1, item.Ref.VerseEnd);
                var passage = await verses.GetAsync(new TranslationId(translationId), reference, ct);

                return passage is null
                    ? null
                    : new
                    {
                        kind = "bible",
                        reference = $"{passage.BookName} {passage.Chapter}:{item.Ref.VerseStart}",
                        translationId = passage.TranslationId.Value,
                        verses = passage.Verses.Select(v => new { verse = v.Number, text = v.Text }),
                    };
            }

            case "slide":
                return new { kind = "slide", text = item.Ref.Text ?? string.Empty };

            case "media" when item.Ref.MediaId is { } mediaId:
            {
                var found = await media.FindAsync(new MediaId(mediaId), ct);

                return found is null
                    ? null
                    : new
                    {
                        kind = "media",
                        mediaKind = found.Kind,
                        url = $"/api/media/{found.Id.Value}/stream",
                        durationMs = found.DurationMs,
                    };
            }

            case "countdown":
                return new { kind = "countdown", targetTime = item.Ref.TargetTime };

            default:
                return null;
        }
    }

    public async Task<IReadOnlySet<string>> UnavailableAsync(ServicePlan plan, CancellationToken ct)
    {
        var unavailable = new HashSet<string>();

        foreach (var item in plan.Items.Where(i => i.Kind == "media" && i.Ref.MediaId is not null))
        {
            if (!await media.IsAvailableAsync(new MediaId(item.Ref.MediaId!), ct))
            {
                unavailable.Add(item.Id.Value);
            }
        }

        return unavailable;
    }
}
```

- [ ] **Step 5: Write the handler**

```csharp
// src/ChurchProjection.Application/Live/LiveCommandHandler.cs
using ChurchProjection.Application.Ports;
using ChurchProjection.Domain.Live;
using ChurchProjection.Domain.Services;

namespace ChurchProjection.Application.Live;

public sealed record LiveView(LiveSnapshot Snapshot, object? LiveContent, object? PreviewContent);

/// <summary>
/// Loads the session, applies one command, persists the result. Every command
/// path goes through here, so "the server decides" is a single method rather
/// than a rule the transports are trusted to follow.
/// </summary>
public sealed class LiveCommandHandler(
    ILiveStateRepository state,
    IServiceRepository services,
    ContentResolver content)
{
    public async Task<LiveView> CurrentAsync(CancellationToken ct)
    {
        var snapshot = await state.LoadAsync(ct) ?? LiveSession.New().Snapshot();

        return await DescribeAsync(snapshot, ct);
    }

    public async Task<(LiveResult Result, LiveView View)> ExecuteAsync(LiveCommand command, CancellationToken ct)
    {
        var snapshot = await state.LoadAsync(ct) ?? LiveSession.New().Snapshot();
        var session = LiveSession.Restore(snapshot);

        // preview is the only command that names a new item, so it is also the
        // only one that can change which service is live. Attaching here means
        // the operator never has to "open" a service first.
        var order = await LoadOrderAsync(session, command, ct);

        var result = command.Type switch
        {
            "preview" when command.ItemId is { } id =>
                session.PreviewItem(new ItemId(id), command.PageIndex ?? 0, order),
            "go" => session.Go(),
            "advance" => session.Advance(order),
            "back" => session.Back(),
            "blackout" => session.SetBlackout(command.On ?? true),
            "clear" => session.Clear(),
            "skip" when command.ItemId is { } id => session.Skip(new ItemId(id), order),
            "unskip" when command.ItemId is { } id => session.Unskip(new ItemId(id)),
            _ => LiveResult.Refuse(RefusalCode.UnknownCommand),
        };

        if (result.IsOk)
        {
            await state.SaveAsync(session.Snapshot(), ct);
        }

        return (result, await DescribeAsync(session.Snapshot(), ct));
    }

    private async Task<IServiceOrder> LoadOrderAsync(
        LiveSession session, LiveCommand command, CancellationToken ct)
    {
        ServicePlan? plan = null;

        if (command.Type == "preview" && command.ItemId is { } itemId)
        {
            plan = await services.FindByItemAsync(new ItemId(itemId), ct);

            if (plan is not null)
            {
                session.AttachService(plan.Id.Value);
            }
        }

        plan ??= session.Snapshot().ServiceId is { } serviceId
            ? await services.FindAsync(new ServiceId(serviceId), ct)
            : null;

        if (plan is null)
        {
            return EmptyOrder.Instance;
        }

        var counts = new Dictionary<string, int>();

        foreach (var item in plan.Items)
        {
            counts[item.Id.Value] = await content.PageCountAsync(item, ct);
        }

        return new ServiceOrderView(plan, counts)
        {
            Unavailable = await content.UnavailableAsync(plan, ct),
        };
    }

    private async Task<LiveView> DescribeAsync(LiveSnapshot snapshot, CancellationToken ct)
    {
        var plan = snapshot.ServiceId is { } serviceId
            ? await services.FindAsync(new ServiceId(serviceId), ct)
            : null;

        return new LiveView(
            snapshot,
            await ResolveAsync(plan, snapshot.Live, ct),
            await ResolveAsync(plan, snapshot.Preview, ct));
    }

    private async Task<object?> ResolveAsync(ServicePlan? plan, Slot? slot, CancellationToken ct) =>
        plan is null || slot is null || plan.Find(slot.ItemId.Value) is not { } item
            ? null
            : await content.ResolveAsync(item, slot.PageIndex, ct);
}
```

- [ ] **Step 5b: Write the empty order**

`IServiceOrder` is not nullable, and "no service is attached yet" is a state the
server is in every time it starts. An empty order answers the three questions
truthfully rather than forcing a null check into every command path.

```csharp
// src/ChurchProjection.Application/Live/EmptyOrder.cs
using ChurchProjection.Domain.Live;

namespace ChurchProjection.Application.Live;

/// <summary>
/// The order when nothing is attached. Contains nothing, so preview and skip
/// refuse with UnknownItem; PageCount is 1, so advance holds where it is.
/// </summary>
public sealed class EmptyOrder : IServiceOrder
{
    public static readonly EmptyOrder Instance = new();

    private EmptyOrder()
    {
    }

    public bool Contains(ItemId id) => false;

    public int PageCount(ItemId id) => 1;

    public bool MediaAvailable(ItemId id) => true;
}
```

- [ ] **Step 6: Add the refusal code the handler introduced**

`RefusalCode.UnknownCommand` was not in Task 4's enum. Add it, and give it a 400
rather than a 409 at the transport, because a command the server has never heard
of is a malformed request, not a conflict — `SYS_LIV_13` in
`LiveBroadcastTests.cs` asserts exactly that.

```csharp
// src/ChurchProjection.Domain/Live/RefusalCode.cs — add:
UnknownCommand,
```

- [ ] **Step 7: Write the transport types**

```csharp
// src/ChurchProjection.Api/Live/OutputCounter.cs
namespace ChurchProjection.Api.Live;

/// <summary>
/// How many projector windows are connected. The control view shows this so the
/// operator knows the screen is alive before the service starts, rather than
/// finding out during the first hymn (FR-LIV-02).
/// </summary>
public sealed class OutputCounter
{
    private int _count;

    public int Current => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);

    public void Decrement() => Interlocked.Decrement(ref _count);
}
```

```csharp
// src/ChurchProjection.Api/Live/LiveStateDto.cs
using ChurchProjection.Application.Live;

namespace ChurchProjection.Api.Live;

public sealed record SlotDto(string ItemId, int PageIndex, object? Content);

public sealed record LiveStateDto(
    string? ServiceId,
    SlotDto? Live,
    SlotDto? Preview,
    bool Blackout,
    IReadOnlyList<string> Skipped,
    int OutputsConnected)
{
    public static LiveStateDto From(LiveView view, int outputsConnected) => new(
        view.Snapshot.ServiceId,
        view.Snapshot.Live is { } live ? new SlotDto(live.ItemId.Value, live.PageIndex, view.LiveContent) : null,
        view.Snapshot.Preview is { } preview
            ? new SlotDto(preview.ItemId.Value, preview.PageIndex, view.PreviewContent)
            : null,
        view.Snapshot.Blackout,
        [.. view.Snapshot.Skipped.Select(id => id.Value)],
        outputsConnected);
}
```

- [ ] **Step 8: Write the hub**

```csharp
// src/ChurchProjection.Api/Live/LiveHub.cs
using ChurchProjection.Api.Access;
using ChurchProjection.Application.Live;

using Microsoft.AspNetCore.SignalR;

namespace ChurchProjection.Api.Live;

public sealed class LiveHub(
    LiveCommandHandler handler, OutputCounter outputs) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        var role = http.Request.Query["role"].ToString();

        if (role is not ("control" or "output" or "remote"))
        {
            Context.Abort();

            return;
        }

        // FR-SEC-10: the projector is a screen in a locked booth with no
        // controls; making a volunteer type a PIN into it before the service
        // starts buys nothing. Everything that can change the screen still pairs.
        if (role != "output" && !await PairGate.IsPairedAsync(http))
        {
            Context.Abort();

            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, role);

        if (role == "output")
        {
            outputs.Increment();
        }

        // FR-LIV-12: full state first, before anything else, so a client that
        // joins mid-service is correct immediately rather than after the next
        // command.
        await Clients.Caller.SendAsync("StateChanged", await StateAsync());

        if (role == "output")
        {
            await BroadcastAsync();
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.GetHttpContext()?.Request.Query["role"].ToString() == "output")
        {
            outputs.Decrement();
            await BroadcastAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendCommand(LiveCommand command)
    {
        _ = await handler.ExecuteAsync(command, Context.ConnectionAborted);

        // Refusals are not thrown at the caller here: the broadcast that follows
        // carries the unchanged state, which is what a stale control view needs
        // in order to correct itself.
        await BroadcastAsync();
    }

    private async Task<LiveStateDto> StateAsync() =>
        LiveStateDto.From(await handler.CurrentAsync(Context.ConnectionAborted), outputs.Current);

    private async Task BroadcastAsync() =>
        await Clients.All.SendAsync("StateChanged", await StateAsync());
}
```

- [ ] **Step 9: Write the REST routes**

```csharp
// src/ChurchProjection.Api/Endpoints/LiveEndpoints.cs
using ChurchProjection.Api.Access;
using ChurchProjection.Api.Live;
using ChurchProjection.Application.Live;
using ChurchProjection.Domain.Live;

using Microsoft.AspNetCore.SignalR;

namespace ChurchProjection.Api.Endpoints;

public static class LiveEndpoints
{
    public static void MapLive(this WebApplication app)
    {
        var group = app.MapGroup("/api/live").RequirePair();

        group.MapGet("/", async (LiveCommandHandler handler, OutputCounter outputs, CancellationToken ct) =>
            Results.Json(LiveStateDto.From(await handler.CurrentAsync(ct), outputs.Current)));

        group.MapPost("/command", async (
            LiveCommand command,
            LiveCommandHandler handler,
            OutputCounter outputs,
            IHubContext<LiveHub> hub,
            CancellationToken ct) =>
        {
            var (result, view) = await handler.ExecuteAsync(command, ct);
            var state = LiveStateDto.From(view, outputs.Current);

            if (result.IsOk)
            {
                // The socket clients hear about an HTTP command exactly as they
                // hear about a hub command. One authority, one broadcast.
                await hub.Clients.All.SendAsync("StateChanged", state, ct);

                return Results.Json(state);
            }

            if (result.Refusal == RefusalCode.UnknownCommand)
            {
                return ApiError.BadRequest("UNKNOWN_COMMAND", $"'{command.Type}' is not a live command.");
            }

            // 409 carrying the unchanged state, so a control screen that issued
            // a stale command resyncs from the refusal itself.
            return Results.Json(
                new
                {
                    error = new { code = Code(result.Refusal), message = Message(result.Refusal) },
                    state,
                },
                statusCode: 409);
        });
    }

    private static string Code(RefusalCode refusal) => refusal switch
    {
        RefusalCode.NoPreview => "NO_PREVIEW",
        RefusalCode.NoLiveItem => "NO_LIVE_ITEM",
        RefusalCode.MediaUnavailable => "MEDIA_UNAVAILABLE",
        RefusalCode.UnknownItem => "UNKNOWN_ITEM",
        RefusalCode.PageOutOfRange => "PAGE_OUT_OF_RANGE",
        _ => "REFUSED",
    };

    private static string Message(RefusalCode refusal) => refusal switch
    {
        RefusalCode.NoPreview => "Nothing is staged, so there is nothing to send to the screen.",
        RefusalCode.NoLiveItem => "Nothing is live yet.",
        RefusalCode.MediaUnavailable => "That media file is not in the media folder.",
        RefusalCode.UnknownItem => "That item is not in the service that is running.",
        RefusalCode.PageOutOfRange => "That page is no longer part of the item.",
        _ => "That command was refused.",
    };
}
```

- [ ] **Step 10: Register and map**

In `CompositionRoot.AddProjection`:

```csharp
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<Live.OutputCounter>();
        builder.Services.AddScoped<ContentResolver>();
        builder.Services.AddScoped<LiveCommandHandler>();
```

In `Program.cs`, after `app.MapMedia();`:

```csharp
app.MapLive();
app.MapHub<LiveHub>("/hub/live");
```

- [ ] **Step 11: Run everything**

```bash
dotnet test
```

Expected: PASS across all four test projects, including `LiveBroadcastTests`
INT-07, INT-08 and SYS-LIV-13, which have been red since the first task.

```bash
npx @usebruno/cli run tests/api --env local
```

Expected: PASS, all nine folders, in folder order — the suite is a single
narrative and `07-live` depends on variables `06-services` set.

- [ ] **Step 12: Commit**

```bash
git add src/ChurchProjection.Application src/ChurchProjection.Api src/ChurchProjection.Domain src/ChurchProjection.Infrastructure
git commit -m "feat: drive the output from a single server-side live session"
```

---

### Task 18: Docker for development, and the publish the booth actually runs

Two different things that are easy to confuse. Docker is for development and CI.
The booth runs a self-contained Windows publish with no runtime installed and no
container engine, because a volunteer cannot be asked to keep Docker Desktop
alive on a Sunday morning.

**Files:**
- Create: `Dockerfile`
- Create: `compose.yaml`
- Create: `.dockerignore`
- Create: `docs/operations/RUNBOOK.md`
- Modify: `README.md`

- [ ] **Step 1: Write the Dockerfile**

```dockerfile
# Development and CI only. See docs/operations/RUNBOOK.md for what runs in the
# booth, which is not this.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ChurchProjection.slnx ./
COPY src/ src/
RUN dotnet publish src/ChurchProjection.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:5000
ENV Storage__DatabasePath=/data/projection.db
ENV Storage__MediaRoot=/data/media
VOLUME /data
EXPOSE 5000

ENTRYPOINT ["dotnet", "ChurchProjection.Api.dll"]
```

- [ ] **Step 2: Write the compose file**

```yaml
services:
  api:
    build: .
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Testing
      Access__TestPin: "123456"
      Access__RequirePairingFromLoopback: "true"
      Cache__Redis__ConnectionString: "cache:6379"
    volumes:
      - projection-data:/data
    depends_on:
      - cache

  # Present so the Redis path is exercised somewhere other than production.
  # The booth runs without this container and must keep working (INT-13).
  cache:
    image: redis:7-alpine
    command: ["redis-server", "--save", "", "--appendonly", "no"]

volumes:
  projection-data:
```

The cache is configured to persist nothing. A cache that survives a restart is a
second database with none of the guarantees.

- [ ] **Step 3: Write .dockerignore**

```
bin/
obj/
tests/
docs/
data/
.git/
```

- [ ] **Step 4: Prove the container path**

```bash
docker compose up --build -d
curl -s http://localhost:5000/healthz
docker compose down -v
```

Expected: `{"ok":true,...}`. This is the one run that exercises the real Redis
adapter; `CacheFallbackTests` covers the failure, and this covers the success.

- [ ] **Step 5: Publish what the booth runs**

```bash
dotnet publish src/ChurchProjection.Api \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/booth
```

Expected: `publish/booth/ChurchProjection.Api.exe` runs on a machine with no
.NET installed.

- [ ] **Step 6: Write the runbook**

```markdown
# Booth Runbook

## What the booth runs

`publish/booth/ChurchProjection.Api.exe`, installed as a Windows Service so it
starts before anyone logs in:

    sc.exe create ChurchProjection binPath= "C:\ChurchProjection\ChurchProjection.Api.exe" start= auto
    sc.exe description ChurchProjection "Church service projection server"
    sc.exe start ChurchProjection

No Docker. No Redis. No .NET runtime install. If the machine boots, the server is
up.

## Where the data is

`C:\ChurchProjection\data\` — `projection.db`, `media\`, and `keys\`.
Backing up means copying that folder while the service is stopped. A backup is
proven by restoring it onto another machine and starting the server there
(INT-12), never by the copy succeeding.

## The PIN

Shown at `http://localhost:5000/api/pin`, readable only from the booth machine.
It rotates on the first request after Saturday midnight. Rotation signs everyone
out, which is the point.

## When something is wrong on a Sunday

1. `http://localhost:5000/healthz` — if this answers, the server is fine and the
   problem is a client or the network.
2. Restart the service. Live state is stored, so the screen comes back where it
   was.
3. If the database will not open, stop the service, restore the last backup
   folder, start it again. The prepared order is in that folder.
```

- [ ] **Step 7: Point the README at it**

Add to `README.md`:

```markdown
## Running

- Development: `docker compose up --build`, then `http://localhost:5000`.
- Tests: `dotnet test`, then `npx @usebruno/cli run tests/api --env local`.
- The booth: see [docs/operations/RUNBOOK.md](docs/operations/RUNBOOK.md).
```

- [ ] **Step 8: Commit**

```bash
git add Dockerfile compose.yaml .dockerignore docs/operations/RUNBOOK.md README.md
git commit -m "chore: add the dev container and document the booth install"
```
