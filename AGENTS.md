# Gereja Apps: rules for agents

## Architecture migration

This repository is migrating from a .NET backend (`src/`) onto the stack and
conventions of the `HoekoS/Template` ("Matsu") starter: Go API under `api/`
(`net/http`, pgx on Postgres, sqitch migrations) and, later, a React
frontend under `app/`. `src/` keeps running during the migration; nothing
there is deleted as part of porting a slice.

Read the matching skill before touching either new tree, same as Matsu
itself requires:

- `api/`: follow Go conventions ported from Matsu's `matsu-go` skill —
  package-per-context under `internal/`, pure domain functions free of
  `net/http`/pgx, the `{"error":{"code":"...","message":"..."}}` envelope
  this project's own `docs/requirements/API-CONTRACT.md` already fixes
  (kept as-is; it is more specific than Matsu's own generic envelope and
  the API contract governs), sqitch migrations under `api/db`.
- `app/` (once it exists): follow conventions ported from Matsu's
  `matsu-react` skill — shared design-system tokens and components, one
  tool per feature.

## Markdown

No new Markdown under `api/` or `app/` — these are the ported trees, and
Matsu's own rule (no Markdown outside `AGENTS.md`, `CLAUDE.md` and
`.claude/skills/**`) applies to them. Put rationale in commit messages or
code comments instead, same as Matsu does.

This does **not** apply to the rest of the repository: `README.md`,
`PRODUCT.md` and everything under `docs/` and `tests/` predate the
migration, carry load-bearing requirement traceability (SRS/URS/test-case
IDs referenced throughout the code and tests), and stay exactly where they
are.

A pre-commit hook (`.githooks/pre-commit`) and a CI job block a staged or
tracked `.md` file under `api/` or `app/`. Enable the hook once per clone:

```
git config core.hooksPath .githooks
```
