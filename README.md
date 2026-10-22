# OxyKit.Flow

Job **orchestrator** for the [OxyKit](https://oxykit.dev) suite: accept render + deliver jobs, persist them (SQLite), process asynchronously via OxyKit.Render and output providers.

**License:** MIT · **Version:** 0.1.0 (pre-1.0) · **Org:** [oxykit-dev](https://github.com/oxykit-dev)  
**Host:** FastEndpoints + ErrorOr (Mail-aligned DX)  
**Depends on:** [`OxyKit.Render.Client`](https://www.nuget.org/packages/OxyKit.Render.Client) (NuGet) · HTTP to [OxyKit.Mail](https://github.com/oxykit-dev/OxyKit.Mail) for `mail` output  
**Releases:** runnable app zips only (no Flow NuGet) — see [docs/RELEASE.md](./docs/RELEASE.md)

## Quick start

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). Run a render engine first (Local API or a release zip from [OxyKit.Render](https://github.com/oxykit-dev/OxyKit.Render)).

```bash
# Terminal A — Render (:5066)
dotnet run --project ../OxyKit.Render/src/OxyKit.Render.Api

# Terminal B — Flow (from this folder)
dotnet run --project src/OxyKit.Flow/OxyKit.Flow.csproj
```

Development uses `appsettings.Development.json` (`X-OxyKit-Flow-Key: dev-flow-key`). Production / release zips require `Security__ApiKey` (and Render engine address/key) via environment or config.

| URL / header | Purpose |
|--------------|---------|
| `http://localhost:5077` | API |
| `X-OxyKit-Flow-Key: dev-flow-key` | Dev API key |
| `/scalar`, `/openapi/v1.json` | OpenAPI UI + JSON (Development) |
| `POST /api/v1/jobs` (+ optional `Idempotency-Key`) | Create job |
| `GET /api/v1/jobs/{id}` | Poll status |
| `GET /health` | Health |

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
| [src/OxyKit.Flow/PHILOSOPHY.md](./src/OxyKit.Flow/PHILOSOPHY.md) | Product vision |
| [hoppscotch/](./hoppscotch/) | Manual HTTP smoke |
| [CHANGELOG.md](./CHANGELOG.md) | Keep a Changelog |
| [LICENSE](./LICENSE) | MIT |
| `/scalar` · `/openapi/v1.json` | Live API shapes (Development) |
