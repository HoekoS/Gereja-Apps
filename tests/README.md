# Tests

Four levels. Nothing here passes yet — see *Red phase* at the bottom.

| Level | Where | Runner | Needs a server |
|---|---|---|---|
| Domain unit | `ChurchProjection.Domain.Tests/` | `dotnet test` | no |
| Application unit | `ChurchProjection.Application.Tests/` | `dotnet test` | no |
| Integration | `ChurchProjection.Api.Tests/` | `dotnet test` | starts its own |
| API, black box | `api/` | Bruno CLI | yes, running separately |

## Unit and integration

```bash
dotnet test
```

Runs all three .NET projects. They need no database, no Redis, and no
configuration: the domain tests have no dependencies at all, and the integration
tests build their own SQLite file under the temp directory and delete it after.
The integration tests use the real EF repositories against that file rather than
fakes, because the queries are the part worth testing.

A single project:

```bash
dotnet test tests/ChurchProjection.Domain.Tests
```

## API tests

These run against a *running* server, over real HTTP, and know nothing about the
implementation behind it. That is deliberate: they are written against
`docs/requirements/API-CONTRACT.md` and survived the backend being changed from
Node to .NET without a single edit to an assertion.

Install the runner once:

```bash
npm i -D @usebruno/cli
```

Start the server in test configuration, in one terminal:

```bash
dotnet run --project src/ChurchProjection.Api \
  --environment Testing \
  --Access:TestPin=123456 \
  --Access:RequirePairingFromLoopback=true
```

Then, in another:

```bash
npm run test:api
```

### The two test-only settings

`Access:TestPin` fixes the PIN. Without it the suite would have to read
`GET /api/pin`, which is loopback-only *and* behind the pair gate — a
chicken-and-egg the suite cannot solve from outside.

`Access:RequirePairingFromLoopback` switches off the loopback exemption
(FR-SEC-08). The suite runs from loopback, so without this flag every request is
exempt from pairing and SYS-SEC-03 passes for the wrong reason.

Both are refused when the environment is Production. That refusal is itself worth
a test before release; it is currently listed as an open gap in
`docs/testing/TEST-CASES.md` §12.

### Run order matters

Folders run in name order and requests run by `seq`. Three dependencies are real:

- `02-access` pairs. Its first request runs *before* pairing on purpose, so the
  cookie jar is empty when SYS-SEC-03 checks that a write is rejected.
- `06-services` and `07-live` chain through variables (`serviceId`, `songId`,
  `liveSongItemId`). Running one file alone will fail on an empty variable.
- `09-limits` runs last because it deliberately trips the pairing rate limiter,
  which locks this address out for the cooldown window.

## Docker

Development and CI only. The booth machine runs a self-contained publish as a
Windows Service; it does not run containers.

```bash
docker compose up --build
```

Brings up the API and a Redis for the cache path. The app starts fine without
Redis — the cache is optional by design — so compose is a convenience, not a
requirement.

## Red phase

`src/` does not exist. Every test here fails, and that is the expected state:

- .NET tests fail at **build**, on the unresolved `ProjectReference`.
- Bruno tests fail on **connection refused**.

They were written first, from the URS and SRS, so they define the contract rather
than describe an implementation. The cost of that is real: if the spec changes,
these change with it. The spec is not approved yet.
