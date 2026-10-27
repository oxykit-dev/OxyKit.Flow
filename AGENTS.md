# OxyKit.Flow - agent guide

Token-efficient map. Prefer this over exploring the tree.

## What it is

**OxyKit** suite core. **MIT** · v0.1 — host releases as app zips only (no Flow NuGet). See `docs/RELEASE.md`.

.NET 10 **Web host**: accepts **jobs** (render + deliver), persists them (**SQLite** or **PostgreSQL**), processes via a background queue. **Does not generate documents** in-process.

**Host pattern (DX):** FastEndpoints + ErrorOr + `FlowEndpoint` + `SendOnErrorAsync` — same family as **OxyKit.Mail** (not Minimal APIs).

Companion engine: HTTP to OxyKit.Render via named `IDocumentRenderer` (default `oxykit-render` uses **`OxyKit.Render.Client` NuGet** — never `OxyKit.Render.Core`). Third-party engines: `OxyKit.Flow.Abstractions` + `Kind=assembly` config (no host fork). Mail delivery is HTTP only.

## Open these first

| Task | Read |
|------|------|
| Vision | `src/OxyKit.Flow/PHILOSOPHY.md` |
| DI / startup | `src/OxyKit.Flow/Program.cs`, `Common/Extensions/` |
| ErrorOr → HTTP | `src/OxyKit.Flow/Common/FlowEndpoint.cs` |
| Auth (API key) | `src/OxyKit.Flow/Security/ApiKeyAuthHandler.cs` |
| Create / get jobs | `src/OxyKit.Flow/Features/Jobs/` |
| Job service | `src/OxyKit.Flow/Features/Jobs/Services/JobService.cs` |
| Execute + queue | `src/OxyKit.Flow/Services/JobExecutor.cs`, `JobQueueProcessor.cs` |
| DB / claim | `src/OxyKit.Flow.Infrastructure/` + `Infrastructure.{Sqlite,Postgres}/` (`IJobClaimStore`) |
| Migrations | `scripts/AddEfMigration.*.ps1`, provider `Migrations/` |
| Abstractions | `src/OxyKit.Flow.Abstractions/` (`IDocumentRenderer`, `Artifact`) |
| Render engines | `src/OxyKit.Flow/Renderers/` + `AddDocumentRenderers` (`Render:Engines`) |
| Outputs | `src/OxyKit.Flow/Outputs/` |
| OxyKit.Render API | `../OxyKit.Render/AGENTS.md` |
| Tests | `tests/OxyKit.Flow.Tests/` |
| Hoppscotch | `hoppscotch/` (do not paste JSON into chat) |
| Compose E2E | `deploy/compose/` + `.github/workflows/compose-e2e.yml` (suite-root `scripts/compose-e2e.sh`) |

Ignore `bin/`, `obj/`, `data/`, runtime `output/`.

## Layout

```text
OxyKit.Flow.slnx
src/OxyKit.Flow.Abstractions/              IDocumentRenderer, RenderRequest, Artifact (packable)
src/OxyKit.Flow.Infrastructure/            Job, JobDelivery, FlowDbContext, IJobClaimStore
src/OxyKit.Flow.Infrastructure.Sqlite/     UseSqlite + transaction claim + Migrations
src/OxyKit.Flow.Infrastructure.Postgres/   UseNpgsql + SKIP LOCKED claim + Migrations
src/OxyKit.Flow/
  Features/Jobs/           Create/Get endpoints, DTOs, validator, JobService (ErrorOr)
  Common/                  FlowEndpoint, AuthSchemes, Extensions (AddOxyKitFlowDatabase)
  Security/                ApiKeyAuthHandler
  Services/                JobExecutor, JobQueueProcessor, options, serializers
  Renderers/               OxyKitRenderEngine, assembly loader, validator
  Outputs/                 IOutputProvider, File, Mail, Ftp, Sftp
  PHILOSOPHY.md
  appsettings.json
  data/                    SQLite file (gitignored)
tests/OxyKit.Flow.Tests/
hoppscotch/
```

## Pipeline

`POST /api/v1/jobs` (+ optional `Idempotency-Key`) -> persist `Job` + `JobDelivery` rows (Queued) ->
`IJobClaimStore` claims (SQLite tx / Postgres `SKIP LOCKED`) -> `JobExecutor` (resolve `IDocumentRenderer` by `render.engine`) ->
each pending `IOutputProvider` (skip succeeded; Mail sends `Idempotency-Key`) -> `Completed` / re-queue / `DeadLettered`.

| Config | Values |
|--------|--------|
| `DatabaseProvider` | `SQLite` (default) \| `PostgreSQL` |
| `ConnectionStrings:SQLiteConnection` | e.g. `Data Source=data/oxykit-flow.db` |
| `ConnectionStrings:PostgreSQLConnection` | e.g. `Host=…;Database=oxykit_flow;…` |

Use **PostgreSQL** when running multiple Flow workers against one store.

Default engine name: **`oxykit-render`**. Config under `Render:Engines:{name}`:

| Kind | Registers |
|------|-----------|
| `oxykit` (default) | Built-in `OxyKitRenderEngine` + `BaseAddress` / `ApiKey` |
| `assembly` | Load `Assembly` + `Type` implementing `IDocumentRenderer` (config key == `Name`) |

Legacy `OxyKit.Render` still seeds the default. Optional job field `render.engine`.

| Name | Destination | Notes |
|------|-------------|-------|
| `file` | filesystem path | writes bytes |
| `mail` | To address | `POST /api/v1/email/send`; optional `subject` / `body` on the output. Mail owns SMTP; 200/202 + `emailId` + status `sent`/`accepted`/`scheduled` = output done — **do not** re-send on `accepted` |
| `ftp` | remote path | Named profile in `Outputs:Transfer:Profiles` (`Protocol: ftp`); optional FTPS via `UseSsl`. Job requires `profile`. Validated at create time |
| `sftp` | remote path | Named profile (`Protocol: sftp`); password and/or `PrivateKeyPath`. Job requires `profile`. Validated at create time |

**Planned (not shipped):** optional per-delivery `zip` — package the artifact with `System.IO.Compression` then hand to the real provider. Not a fifth `IOutputProvider`. Cloud retain stays a separate module.

## Hard constraints

- **No in-process generators** - render via named HTTP engines (`IDocumentRenderer`)
- Default engine uses **`OxyKit.Render.Client` only** - never Core; Mail is HTTP only
- Third-party engines depend on **`OxyKit.Flow.Abstractions`** only — register via config (`Kind=assembly`), do not fork the host
- Endpoints inherit `FlowEndpoint*` + `SendOnErrorAsync`; services return `ErrorOr` when failure is possible
- Render routing: job `render.engine` == `IDocumentRenderer.Name` (default `oxykit-render`)
- Output routing: job `provider` == `IOutputProvider.Name`
- Client idempotency via unique `Idempotency-Key`; processor claim via `IJobClaimStore` (provider-specific) + status
- Delivery checkpoints: `JobDelivery` per output; retries skip `Succeeded`; Mail key `{jobId:N}:{ordinal}`
- **Mail output:** Flow owns the job; Mail owns delivery. Fail/re-queue the job only when Mail rejects the handoff (non-2xx or missing `emailId`). Requires `OxyKit.Mail:ApiKey` (compose: `FLOW_MAIL_API_KEY` after Mail bootstrap + Flow restart) — empty key → mail deliveries fail
- **file / ftp / sftp:** checkpoint skip on retry; **no sink-level idempotency** — crash after a successful write/upload but before Flow saves `Succeeded` can duplicate on retry (Mail hole closed via `Idempotency-Key`)
- **FTP/SFTP:** in-process providers; credentials only in `Outputs:Transfer:Profiles`; validate profile + protocol at job create (`TransferOutputValidator`)
- No secrets in source - config / user secrets
- `Security:ApiKey` required at startup (ValidateOnStart; not the placeholder)

## Add a route / output / render engine

1. Flat files under `src/OxyKit.Flow/Features/{Feature}/` (subfolders only `Services/` when needed)
2. Inherit `FlowEndpoint*`; `Configure()`: route, `Version(1)`, `AuthSchemes(AuthSchemeNames.ApiKey)`, Summary
3. `SendOnErrorAsync` for failures; DTOs only — never EF entities
4. Validate at create time when possible (e.g. transfer profiles, render engines)
5. **New output (in-repo):** implement `IOutputProvider`, register in `Program.cs`
6. **New oxykit-shaped sidecar:** `Render:Engines:{name}` with `Kind=oxykit` + BaseAddress
7. **Third-party / exotic engine:** class lib → `OxyKit.Flow.Abstractions`; implement `IDocumentRenderer`; drop DLL; `Kind=assembly` + Assembly/Type (config key == `Name`)
8. Tests under `tests/OxyKit.Flow.Tests/` (xUnit v3 / MTP; `dotnet test --project …`); public surface → Hoppscotch
9. Note in `CHANGELOG.md` `[Unreleased]` when behavior changes

## Commands

```bash
cd OxyKit.Flow
# Terminal A — Render
dotnet run --project ../OxyKit.Render/src/OxyKit.Render.Api   # :5066
# Terminal B — Flow
dotnet run --project src/OxyKit.Flow/OxyKit.Flow.csproj        # :5077  Scalar /scalar (Development)
dotnet test tests/OxyKit.Flow.Tests/OxyKit.Flow.Tests.csproj
```

Dev key: `X-OxyKit-Flow-Key: dev-flow-key`.  
Hoppscotch: import `hoppscotch/*`, env **OxyKit.Flow Local**, run folders in order — **Jobs — file** (Render) then optional **Jobs — mail** (Render + Mail + `mailTo` / Flow Mail API key).

## Docs policy

- Suite map: `../AGENTS.md` (Mail+Flow = FE/ErrorOr for DX; Render = Minimal APIs for resources/AOT)
- Prefer editing the smallest owner (new output type ≠ changing executor unless needed)
- Do not dump Hoppscotch JSON into agent context
