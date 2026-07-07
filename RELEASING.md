# Releasing MCEC

**Releases are cut from `main`, never from `develop`.**

`develop` is the integration branch (all PRs merge here). `main` is the release branch, and GitHub
Pages also publishes the docs site from `main/docs`. Cutting releases from `main` keeps the shipped
build and the published docs in lockstep: a release can never go out ahead of the docs.

## Cutting a release

1. Make sure `develop` is green and has everything you want to ship.
2. Bring `main` up to date with `develop`:
   ```sh
   git checkout main
   git pull
   git merge --no-ff origin/develop
   git push origin main
   ```
   This also republishes the docs site (Pages builds from `main/docs`).
3. Tag the **`main`** commit with the next `vX.Y.Z` and push the tag:
   ```sh
   git tag -a v3.0.27 -m "MCEC v3.0.27"
   git push origin v3.0.27
   ```
4. The `Release` workflow (`.github/workflows/release.yml`) builds the self-contained installer, signs
   it with Azure Trusted Signing, publishes the GitHub Release, and (for stable versions) submits to
   winget-pkgs. The version is taken from the tag — nothing in the repo needs a manual version bump.

## Guardrail

The release workflow's **"Enforce release comes from main"** step refuses to build any tag whose commit
is not an ancestor of `origin/main`. If you accidentally tag a `develop` (or feature-branch) commit, the
release fails fast with a message telling you to merge `develop → main` and tag the `main` commit
instead. This is what keeps releases — and the published docs — from drifting off `main`.
