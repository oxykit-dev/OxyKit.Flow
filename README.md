# OxyKit.Flow

[![CI](https://github.com/oxykit-dev/OxyKit.Flow/actions/workflows/ci.yml/badge.svg)](https://github.com/oxykit-dev/OxyKit.Flow/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/download)

Job **orchestrator** for the [OxyKit](https://oxykit.dev) suite: accept render + deliver jobs, persist them (SQLite or PostgreSQL), process asynchronously via OxyKit.Render and output providers.

**License:** MIT · **Org:** [oxykit-dev](https://github.com/oxykit-dev) · **Site:** [oxykit.dev](https://oxykit.dev)  
**Releases:** [app zips](https://github.com/oxykit-dev/OxyKit.Flow/releases) (no Flow NuGet) · **Uses:** [`OxyKit.Render.Client`](https://www.nuget.org/packages/OxyKit.Render.Client)  
**Related:** HTTP to [OxyKit.Mail](https://github.com/oxykit-dev/OxyKit.Mail) for `mail` output · host DX matches Mail (FastEndpoints + ErrorOr)

## Quick start

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). Run a render engine first (Local API or a [Render release zip](https://github.com/oxykit-dev/OxyKit.Render/releases)).

```bash
# Terminal A — Render (:5066)
dotnet run --project ../OxyKit.Render/src/OxyKit.Render.Api

# Terminal B — Flow (from this folder)
dotnet run --project src/OxyKit.Flow/OxyKit.Flow.csproj
```

Development uses `appsettings.Development.json` (`X-OxyKit-Flow-Key: dev-flow-key`) and **SQLite** by default. For a shared store / multiple workers set `DatabaseProvider=PostgreSQL` and `ConnectionStrings__PostgreSQLConnection`. Production / release zips require `Security__ApiKey` (and Render engine address/key) via environment or config.

| URL / header | Purpose |
|--------------|---------|
| `http://localhost:5077` | API |
| `X-OxyKit-Flow-Key: dev-flow-key` | Dev API key (Admin). A non-empty key header is the only credential checked |
| `Authorization: Bearer` | Mail JWT (`Security:Jwt`). Admin creates and retries; Admin\|User reads; Sender has no job access |
| `GET /health` | Health |
| `X-Correlation-Id` | Optional; echoed; propagated to Render/Mail on job work |
| `/scalar`, `/openapi/v1.json` | OpenAPI UI + JSON (Development only) |
| `POST /api/v1/jobs` (+ optional `Idempotency-Key`, `webhookUrl`) | Create job |
| `GET /api/v1/jobs` (`status`, `limit`) | List jobs (newest first) |
| `GET /api/v1/jobs/{id}` | Poll status |
| `POST /api/v1/jobs/{id}/retry` | Requeue DeadLettered job |

```bash
dotnet test tests/OxyKit.Flow.Tests/OxyKit.Flow.Tests.csproj
```

**Smoke:** import [`hoppscotch/`](./hoppscotch/) collection + env → **OxyKit.Flow Local** → **Jobs — file** (Render), optional **Jobs — mail**, then **Jobs — admin** (list / retry).

Outputs: `file`, `mail` (HTTP to OxyKit.Mail), `ftp` / `sftp` (named profiles). A `mail` delivery may pass `templateId` + JSON object `data` through to Mail; the render artifact stays attached. Optional per-output `"zip": true`. `file` / `ftp` / `sftp` write via `*.partial` then rename (overwrite). Render artifacts cached under `Artifacts:Root` for delivery retries; optional terminal webhooks. Layout and constraints: [AGENTS.md](./AGENTS.md).

Observability: NuGet [`OxyKit.Observability`](https://www.nuget.org/packages/OxyKit.Observability) + [`OxyKit.OpenTelemetry`](https://www.nuget.org/packages/OxyKit.OpenTelemetry) (JSON stdout; correlation propagated to Render/Mail; optional OTLP). Docs: [oxykit.dev/guide/observability](https://oxykit.dev/guide/observability).

## Docs

| Doc | Role |
|-----|------|
| [AGENTS.md](./AGENTS.md) | Layout, pipeline, constraints |
| [docs/RELEASE.md](./docs/RELEASE.md) | Tag → GitHub Release app zips |
| [oxykit.dev/flow](https://oxykit.dev/flow/) | Product docs |
| [oxykit.dev/guide/iis](https://oxykit.dev/guide/iis) | Windows / IIS |
| [src/OxyKit.Flow/PHILOSOPHY.md](./src/OxyKit.Flow/PHILOSOPHY.md) | Product vision |
| [hoppscotch/](./hoppscotch/) | Manual HTTP smoke |
| [CHANGELOG.md](./CHANGELOG.md) | Keep a Changelog |
| `/scalar` · `/openapi/v1.json` | Live API shapes (Development) |
| [LICENSE](./LICENSE) | MIT |
