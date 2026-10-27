# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added

- Multi-provider job store: `OxyKit.Flow.Infrastructure` + `Infrastructure.Sqlite` / `Infrastructure.Postgres` (Mail-aligned `DatabaseProvider` switch)
- Atomic job claim: SQLite write transaction; PostgreSQL `FOR UPDATE SKIP LOCKED` (multi-worker safe)
- EF migrations per provider (`scripts/AddEfMigration.*.ps1`); startup applies `Migrate()` instead of `EnsureCreated`

### Changed

- Connection strings: `SQLiteConnection` / `PostgreSQLConnection` (replaces `ConnectionStrings:Flow`)
- Default remains **SQLite** for local / Compose; set `DatabaseProvider=PostgreSQL` for shared durable store
- Local DBs created with `EnsureCreated` are not migration-aware — delete `data/oxykit-flow.db` (or Compose `flow-data` volume) once after upgrading

### Planned

- Per-output **zip packaging** (optional `zip` on a delivery): wrap the single render `Artifact` via `System.IO.Compression` (`ZipArchive`) before `file` / `mail` / `ftp` / `sftp` — not a separate output provider. Password-protected zip and multi-entry archives deferred.
- Per-output delivery state + Mail idempotency keys (safe retries under distribution)

## [0.1.0] - 2026-10-22

### Added

- `OxyKit.Flow.Abstractions` — shared `IDocumentRenderer` / `RenderRequest` / `Artifact` for host and plugins
- Pluggable render engines: `Render:Engines` with `Kind=oxykit|assembly`; optional `render.engine` on jobs (default `oxykit-render`)
- `Kind=assembly` loads a third-party DLL + type (no host code changes)
- Automated tests (`tests/OxyKit.Flow.Tests`)
- Hoppscotch smoke (`hoppscotch/`)
- FTP / SFTP output providers (named transfer profiles)
- GitHub Release workflow: framework-dependent app zips (`linux-x64`, `win-x64`, `osx-arm64`)

### Changed

- Default engine depends on **`OxyKit.Render.Client` NuGet** (`0.1.2`) instead of a sibling `ProjectReference`
- Built-in and third-party engines share `IDocumentRenderer`; `JobExecutor` resolves by name
- Config prefers `Render:Engines:{name}`; legacy `OxyKit.Render` still seeds the default engine
- Host pattern: **FastEndpoints + ErrorOr** + `FlowEndpoint` / `SendOnErrorAsync` (Mail-aligned DX); Jobs under `Features/Jobs/`
- API-key auth via authentication scheme (replaces middleware); Scalar + OpenAPI in Development (`/openapi/v1.json`, `ShortSchemaNames`)
- Repo layout: host under `src/OxyKit.Flow/` (suite-aligned `*.slnx`)
- Tests: **xunit.v3.mtp-v2** (`OutputType=Exe`, `global.json` → Microsoft.Testing.Platform)
- Dev API keys live in `appsettings.Development.json` only (base `appsettings.json` ships without secrets)
