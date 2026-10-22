# Release (runnable app zips only)

Flow does **not** publish NuGet packages. Tag pushes build framework-dependent app zips for GitHub Releases.

Workflow: [`.github/workflows/release.yml`](../.github/workflows/release.yml)

## Assets

| Asset | Notes |
|-------|--------|
| `OxyKit.Flow-{version}-linux-x64.zip` | Needs .NET 10 runtime |
| `OxyKit.Flow-{version}-win-x64.zip` | Needs .NET 10 runtime |
| `OxyKit.Flow-{version}-osx-arm64.zip` | Needs .NET 10 runtime (Apple Silicon) |

Depends on a running OxyKit.Render engine (and Mail if using `mail` output). Client SDK: [`OxyKit.Render.Client`](https://www.nuget.org/packages/OxyKit.Render.Client) on nuget.org.

## Release steps

1. Bump `<Version>` in `src/OxyKit.Flow/OxyKit.Flow.csproj` (must match the tag).
2. Cut `CHANGELOG.md`.
3. Commit and push to default branch.
4. Tag and push (or create the tag from the GitHub Release UI on that commit):

```bash
git tag v0.1.0
git push origin v0.1.0
```

Watch **Actions → Release**. First public tag: ensure `LICENSE`, CI, and this workflow are on the tagged commit.
