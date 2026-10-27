# Suite compose bundle (CI)

Mirror of the **suite-root** Compose golden path so Flow CI can assemble sibling checkouts of Render + Mail + Flow.

| Local suite checkout | This folder (Flow repo) |
|----------------------|-------------------------|
| `docker-compose.yml` | `docker-compose.yml` |
| `docker/*.Dockerfile` | `docker/` |
| `scripts/compose-e2e.sh` | `compose-e2e.sh` |
| `.env.example` | `.env.example` |

Keep in sync when suite-root Compose files change. Workflow: [`.github/workflows/compose-e2e.yml`](../../.github/workflows/compose-e2e.yml).

**Private sibling repos:** set repository secret `SUITE_CHECKOUT_PAT` (read access to `OxyKit.Render` + `OxyKit.Mail`). After those repos are public, the default `github.token` is enough.
