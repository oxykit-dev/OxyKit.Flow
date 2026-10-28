# Suite compose bundle (CI)

Mirror of the **suite-root** Compose golden path so Flow CI can assemble sibling checkouts of Render + Mail + Flow.

| Local suite checkout | This folder (Flow repo) |
|----------------------|-------------------------|
| `docker-compose.yml` | `docker-compose.yml` |
| `docker/*.Dockerfile` | `docker/` |
| `scripts/compose-e2e.sh` | `compose-e2e.sh` |
| `.env.example` | `.env.example` |

Keep in sync when suite-root Compose files change. Workflow: [`.github/workflows/compose-e2e.yml`](../../.github/workflows/compose-e2e.yml).

Sibling repos are **public** — workflow checkouts use `github.token`. Optional secret `SUITE_CHECKOUT_PAT` remains as a fallback only.
