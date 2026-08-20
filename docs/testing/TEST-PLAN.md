# Test Plan

**System:** Church Service Projection Application
**Version:** 0.1 (draft)
**Date:** 2026-08-20
**Status:** Draft — awaiting review

---

## 1. Purpose

This plan states how the system is verified against the [URS](../requirements/URS.md) and the [SRS](../requirements/SRS.md), what is automated, what cannot be, and when testing is finished.

The individual cases are in [TEST-CASES.md](TEST-CASES.md).

## 2. What this system's testing is actually for

Ordinary software fails and someone retries. This one fails in front of a congregation, mid-sentence, with no undo and no second attempt. That shapes the whole plan:

- **The failure tests are not an afterthought.** Killing the control view, killing the server, and unplugging the output are first-class cases, not edge cases. They are the requirements the product exists to satisfy (URS-AVL-02, URS-AVL-03).
- **The output view is tested for what it must *never* show**, not only for what it shows. A test that proves the congregation screen renders a verse is worth less than one proving it renders black rather than a stack trace (URS-AVL-05).
- **Offline is proven by disconnection, not by inspection.** Reading the code for `fetch` calls is not the test. Physically unplugging the uplink is (NFR-REL-01).

## 3. Test levels

| Level | What it covers | Tool | Automated |
|---|---|---|---|
| **Unit** | Pure logic with no I/O: the live aggregate, PIN rotation boundaries, reference parsing. | xUnit, no host, no database | Yes |
| **Unit, application** | The import parsers, against fixture files including a truncated one. | xUnit | Yes |
| **API / system** | Every HTTP endpoint in the [API contract](../requirements/API-CONTRACT.md), against a running server with a seeded database. | Bruno + `bru` CLI | Yes |
| **Integration / failure** | The composed pipeline, the SignalR broadcast, process kills, disconnections, restarts, recovery. | xUnit + `WebApplicationFactory` for what can be hosted; manual for what needs a wall socket | Partly |
| **Manual** | Physical properties no harness can observe: legibility from the back row, screen brightness in a dim booth, whether an untrained volunteer can run a service. | Human, in the sanctuary | No |
| **Acceptance** | One full service, end to end, on the real machines, with the internet physically disconnected. | Human, in the sanctuary | No |

### 3.1 Why unit tests stop where they do

Four modules carry real branching logic and no I/O, and they get real unit tests. Everything else is either a repository adapter or a React component, and unit-testing those buys coverage numbers rather than confidence. A repository test against a mocked `DbContext` proves only that the mock was configured to agree with the code; the queries it is meant to protect — FTS5 search, the service-order reconcile — are only wrong against a real database. Repositories are therefore covered at the integration and API levels, against a real SQLite file, where a failure means something actually broke.

This is a deliberate ceiling. When a bug escapes to production that a component test would have caught, add component tests then — not before.

## 4. Environments

| Environment | Description |
|---|---|
| **dev** | Developer machine. Unit tests and API tests run here against a throwaway database. |
| **booth** | The real booth machine with real displays and the real sanctuary network. Integration, manual, and acceptance testing only. |

There is no staging environment and no need for one. The booth machine *is* production, and it is available every day of the week except Sunday morning.

### 4.1 Test data

API tests run against a seeded database, created fresh for each run and discarded afterwards. The seed contains:

- Two Bible translations with a small verse set — enough to test lookup, cross-translation switching, and search, not the full canon.
- Three songs, one of which carries an Indonesian section label (`Reff`) to prove FR-LIB-12.
- One image, one video, and one deliberately missing media file whose row exists but whose file does not — the fixture for FR-LIB-23 and FR-LIV-17.
- One service of six items, one of each kind.

Capacity requirements (SRS §5.6) are not tested against the seed. They need a full-canon load and are covered by their own case (see PERF-02 in TEST-CASES).

## 5. Entry criteria

Testing of a level begins when:

- **Unit** — the module under test exists and exports its specified interface.
- **API** — the server starts, `GET /healthz` returns 200, and the seed loads.
- **Integration** — all API tests pass.
- **Acceptance** — all automated tests pass, and every manual case has been executed once.

## 6. Exit criteria

The system is releasable to a live service when all of the following hold:

1. Every automated test passes.
2. Every requirement in the coverage matrix (TEST-CASES §11) is either covered by a passing test or listed with a stated reason for having none.
3. Every case marked **critical** passes. There is no waiver for a critical case — these are the ones whose failure is visible to the congregation.
4. The acceptance run (UAT-01) completes with no operator intervention outside the system.
5. No open defect of severity 1 or 2.

## 7. Defect severity

| Severity | Definition |
|---|---|
| **1 — Service-stopping** | The congregation sees something wrong, or the operator cannot put up what is needed. Blocks release. |
| **2 — Recovery failure** | The system does not return to a working state after a fault the requirements say it must survive. Blocks release. |
| **3 — Operator friction** | Correct output, but the operator is slowed or has to work around something. Does not block. |
| **4 — Cosmetic** | Visible only to the operator, no effect on output or speed. Does not block. |

Anything the congregation can see is severity 1 by definition, regardless of how small it looks in the booth.

## 8. Automation

| Command | Runs |
|---|---|
| `dotnet test` | Domain, application, and integration tests |
| `dotnet test tests/ChurchProjection.Domain.Tests` | One project |
| `npm run test:api` | Bruno collection against a separately running server |

`package.json` exists only to invoke Bruno, which is distributed on npm. It carries no application dependencies.

Both suites exit non-zero on failure and are suitable for a pre-commit hook or CI job. No CI is configured yet — there is no repository. See §11.

The API suite needs the server started with two test-only settings; `tests/README.md` carries the exact command and the reason for each.

Integration cases INT-01 to INT-05 involve killing processes and pulling cables. INT-01 to INT-03 are hostable through `WebApplicationFactory` and are written; INT-04 and INT-05 need a human at the wall socket.

## 9. What is deliberately not tested

| Not tested | Reason |
|---|---|
| Browser engines other than Chromium | Excluded by CON-08. |
| Concurrent editing of the same service by two preparers | Not a requirement. One person prepares. |
| Repository adapters against a mocked `DbContext` | See §3.1. A mock proves the mock, not the query. |
| Load beyond 8 clients | Excluded by NFR-CAP-05. |
| Internet-facing behaviour | Excluded by NFR-SEC-01. The system is never exposed. |
| TLS | None is used. See the accepted risk in SRS §5.3. |
| Requirements marked **(P)** | Proposed, not confirmed. Cases exist but are marked pending. |

## 10. Risks to the test effort

| Risk | Effect | Handling |
|---|---|---|
| No implementation exists yet | Every test in this suite fails at authoring time — unit tests on module resolution, API tests on connection refused. | Accepted and intended. These tests define the contract. Red is the correct starting state. |
| The SRS is not yet approved | A requirement change invalidates the tests written against it. | Test IDs trace to requirement IDs, so the blast radius of any change is readable from the coverage matrix. |
| The booth machine is the only realistic environment | Integration and acceptance testing compete with actual church use. | Test on weekdays. Never on Sunday morning. |
| Performance thresholds are unconfirmed | Cases PERF-01 to PERF-08 may be testing the wrong numbers. | Marked as targets, not gates, until confirmed (SRS-OPN-03). |

## 11. Open items

| ID | Item |
|---|---|
| TP-OPN-01 | No git repository exists, so no CI is configured. Once one exists, both suites should run on every commit. |
| TP-OPN-02 | Whether the seed database should be committed as a fixture file or built by a script on each run. Currently assumed: built by a script. |
| TP-OPN-03 | Who performs the volunteer trial for NFR-USE-06, and when. It needs a person who has genuinely not seen the system. |
| TP-OPN-04 | Whether the acceptance run happens once before first use, or is repeated after significant changes. Recommended: repeated. |
