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
| `X-OxyKit-Flow-Key: dev-flow-key` | Dev API key |
| `GET /health` | Health |
| `/scalar`, `/openapi/v1.json` | OpenAPI UI + JSON (Development only) |
| `POST /api/v1/jobs` (+ optional `Idempotency-Key`) | Create job |
| `GET /api/v1/jobs/{id}` | Poll status |

```bash
dotnet test tests/OxyKit.Flow.Tests/OxyKit.Flow.Tests.csproj
```

**Smoke:** import [`hoppscotch/`](./hoppscotch/) collection + env → **OxyKit.Flow Local** → run folders in order (Render required for job completion).

Outputs: `file`, `mail` (HTTP to OxyKit.Mail), `ftp` / `sftp` (named profiles). Layout and constraints: [AGENTS.md](./AGENTS.md).

## Docs

| Doc | Role |
|-----|------|
| [AGENTS.md](./AGENTS.md) | Layout, pipeline, constraints |
| [docs/RELEASE.md](./docs/RELEASE.md) | Tag → GitHub Release app zips |
| [oxykit.dev/flow](https://oxykit.dev/flow/) | Product docs |
| [src/OxyKit.Flow/PHILOSOPHY.md](./src/OxyKit.Flow/PHILOSOPHY.md) | Product vision |
| [hoppscotch/](./hoppscotch/) | Manual HTTP smoke |
| [CHANGELOG.md](./CHANGELOG.md) | Keep a Changelog |
| `/scalar` · `/openapi/v1.json` | Live API shapes (Development) |
| [LICENSE](./LICENSE) | MIT |
