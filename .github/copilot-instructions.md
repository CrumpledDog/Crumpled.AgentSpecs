# Copilot / Agent Instructions — Crumpled.AgentSpecs

TODO: describe what this package does. See [README.md](../README.md) for the feature overview and
[CONTRIBUTING.md](../CONTRIBUTING.md) for the full branching strategy.

## Cross-Branch Workflow (v1 → v2) — Check This Every Time

Once a `develop/v2` line exists: **whenever you commit and push a change to `develop/v1`, before considering the
task complete, stop and decide whether it also belongs on `develop/v2`.** The default answer is yes — nearly
everything is shared code, docs, workflows and tests. Skip only for something genuinely Umbraco-17-only.
Concretely: `cd` into the `develop/v2` worktree, `git merge develop/v1` (never cherry-pick), rebuild if the merge
touched `Client/src/**` (`npm run generate-client && npm run build` against a running v2 DemoSite), push
`develop/v2`, wait for CI, then fast-forward `release/v2` only when actually cutting a v2 release.

Always push `develop/v1` before `develop/v2`, and validate any edited workflow YAML with a parser before pushing —
a malformed `.github/workflows/*.yml` is rejected outright and silently skips CI on every branch it reaches.

Commit messages drive the version. **Never start a line of a commit body with the words `BREAKING CHANGE`** unless
the change is genuinely breaking — the parser treats any such line (colon or not) as a breaking note, which forces a
major release on that branch and can only be undone by rewording and force-pushing. After the first `develop/v2`
push, check each CI run's *Semantic Release* log: v2 must compute `2.0.0-alpha.N` and v1 must stay `1.x`.

## Repo shape

```
src/
  Crumpled.AgentSpecs/            - the whole package: Management API, Composers, and (if generated
                                          with a backoffice UI) the embedded Client/ (Lit/TS)
  Crumpled.AgentSpecs.DemoSite/   - minimal Umbraco 17 site, CI-only fixture (no starter kit)
tests/
  Crumpled.AgentSpecs.Tests/
```

Single packable project, generated from Umbraco's own `umbraco-extension` dotnet template (not a
hand-rolled Core/Client split) - the Client folder lives inside the same `.csproj`, not a separate one.

## Two Umbraco majors, one repo

`develop/v1`/`release/v1` (Umbraco 17) is where all development happens. `develop/v2`/`release/v2`
(Umbraco 18, once created) only ever receives merges *from* v1 — never branch features directly against v2,
never cherry-pick. Work on v2 in its own git worktree, not by switching branches in the v1 clone, so
`node_modules`, `bin`/`obj` and the local SQLite database never mix between majors. Run
`git config merge.ours.driver true` once per clone/worktree. See CONTRIBUTING.md's "Umbraco 17/18 branch lines"
section for the full workflow, the checklist for creating `develop/v2`, and which files are marked `merge=ours`
in `.gitattributes`.

`.github/workflows/*.yml` are **shared**, not `merge=ours` — change them on `develop/v1` and merge forward, and
keep anything that touches an Umbraco-version-specific URL working on both majors (e.g. the health check polls
both `/umbraco/swagger/<name>/swagger.json` and `/umbraco/openapi/<name>.json`).

The one genuine code-level difference between majors (Swashbuckle vs native OpenAPI document registration)
lives in `Composers/OpenApiRegistration.Umbraco17.cs`/`.Umbraco18.cs`, conditionally compiled via
`$(UmbracoTargetMajor)` from the repo-root `Directory.Build.props`. Everything else in the Composers/
Controllers is version-agnostic and merges cleanly across the two branches.

## Formatting

`dotnet format --verify-no-changes` must pass in CI. Run `dotnet format` before committing.

## API changes & codegen

If you change any Management API controller (routes, request/response models), regenerate the TypeScript
client: start `Crumpled.AgentSpecs.DemoSite` (`dotnet run`, `https://localhost:44336`), then
`cd src/Crumpled.AgentSpecs/Client && npm run generate-client`. Never hand-edit `src/api/*.gen.ts` -
it's gitignored and CI always regenerates it. Never use raw `fetch()` in Client code - always the generated
SDK functions from `src/api/index.js`.

## Versioning

No hardcoded versions in the `.csproj` — semantic-release computes the version from Conventional Commits
and injects it at pack time via `/p:PackageVersion=`. Don't hand-edit version numbers anywhere.
