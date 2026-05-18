# Releasing FamilyNido

Single-maintainer flow for cutting a SemVer release. The
[`.github/workflows/release.yml`](.github/workflows/release.yml) workflow
takes over once a `v*.*.*` tag is pushed to `origin/main`.

## Before tagging

1. **Make sure `main` is green.** Every PR is gated on the E2E suite and
   the build pipeline, so this is usually true, but worth a glance at
   the Actions tab.

2. **Update [`CHANGELOG.md`](./CHANGELOG.md):**
   - Rename `## [Unreleased]` to `## [X.Y.Z] - YYYY-MM-DD` with today's
     date.
   - Add a new empty `## [Unreleased]` block at the top.
   - Update the comparison link at the bottom: rename the existing
     `[Unreleased]` link to `[X.Y.Z]` and bump the new `[Unreleased]`
     comparison to `vX.Y.Z...HEAD`.
   - Commit on a branch (e.g. `release/X.Y.Z`), open a PR, merge it.

3. **Choose the version.** Pre-1.0 SemVer:
   - **patch** (`0.1.0` → `0.1.1`): bug fixes only, no API/UX changes.
   - **minor** (`0.1.x` → `0.2.0`): new features OR breaking changes
     (acceptable while on `0.x`; call them out in the changelog).
   - **major** (`0.x` → `1.0.0`): only when the project is ready to
     commit to backwards-compatible minor releases going forward.

## Tagging and pushing

From the freshly merged `main`:

```bash
git checkout main
git pull --ff-only origin main
git tag -a v0.1.0 -m "v0.1.0"
git push origin v0.1.0
```

The release workflow fires immediately. It will:

1. Build + push both images with tags `:0.1.0`, `:0.1` and `:latest` to
   GHCR.
2. Extract the `## [0.1.0]` section from `CHANGELOG.md`.
3. Create a GitHub Release titled `v0.1.0` with that text as the body,
   marked as *Latest*.

A successful run takes ~3 minutes.

## If something goes wrong

- **Workflow failed mid-way (e.g. one image built, the other didn't):**
  trigger it again from Actions → *Release* → *Run workflow* with the
  same tag (e.g. `v0.1.0`). The job is idempotent — overwriting a
  floating tag like `:0.1` or `:latest` is fine, the immutable `:0.1.0`
  is overwritten too.
- **Wrong content tagged:** delete the tag locally and remotely
  (`git tag -d v0.1.0; git push origin :v0.1.0`), and either delete or
  edit the GitHub Release manually. Then start over.
- **Bad release shipped:** publish a patch (`v0.1.1`) with the fix. Do
  not retroactively rewrite a released version.

## What the user sees

- On GHCR, the new tags appear under the package's Versions tab.
- On the home server, `docker compose -f docker-compose.prod.yml pull`
  followed by `up -d` picks up the new `:latest`.
- On the GitHub repo, the Releases tab shows the changelog body and the
  source tarball/zipball.
