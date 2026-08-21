# Church Projection

An offline LAN backend for running a church service's on-screen projection —
songs, Bible passages, and media — from a booth machine with no internet
dependency.

## Running

- Development: `docker compose up --build`, then `http://localhost:5000`.
- Tests: `dotnet test`, and against a running API:
  ```
  cd tests/api
  npx @usebruno/cli run 01-health 02-access 03-bible 04-songs 05-import 06-services 07-live 08-media 09-limits --env local
  ```
- Booth: see [docs/operations/RUNBOOK.md](docs/operations/RUNBOOK.md).
