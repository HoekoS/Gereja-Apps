# Church Projection

An offline LAN backend for running a church service's on-screen projection —
songs, Bible passages, and media — from a booth machine with no internet
dependency.

## Running

- Development: `docker compose up --build`, then `http://localhost:5000`.
- Tests: `dotnet test`, and against a running API: `npm run test:api` (the whole
  Bruno collection under `tests/api/`; `npm run test:api:report` also writes
  `reports/api-results.json`).
- Booth: see [docs/operations/RUNBOOK.md](docs/operations/RUNBOOK.md).
