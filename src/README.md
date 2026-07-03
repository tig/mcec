# Build & Deploy Info

## Pre-requisites

* .NET 10.0 SDK or greater
* Visual Studio 2022 or greater (optional - can build from command line with `dotnet build`)
* NSIS 3.x - `winget install nsis` (for creating the installer)

## Building

From the command line:
```bash
cd src
dotnet restore
dotnet build
```

Or open `src/MCEControl.sln` in Visual Studio 2022 and build from there.

## Versions & Updates

Dev/CI builds are versioned by [GitVersion](https://gitversion.net/) (`GitVersion.yml`, mode `ContinuousDelivery`) via the `GitVersion.MsBuild` package: it derives a version from the nearest tag plus branch/commit info and stamps the assembly automatically. A release build (`Release.ps1`) instead passes `-p:DisableGitVersionTask=true -p:Version=<tag>` to get a clean, deterministic version with no git suffix. There is no build-time file bump and no T4 template involved.

Releases are published at https://github.com/tig/mcec/releases

Debug builds check for pre-releases and there should ALWAYS be a fake, NEWER pre-release like https://github.com/tig/mcec/releases/tag/v2.3.6.0 to force testing of the update system.

## Installer

* `Installer\mcec.Setup.exe`
* Built using NSIS. `Release.ps1` runs `makensis.exe` against `Installer/MCEController.nsi`.

## How to Release a new version

Releases are cut by pushing a `v<major>.<minor>.<patch>` tag; **do not** hand-build or hand-upload the installer, since that bypasses the release pipeline's Authenticode signing (see [`docs/code-signing.md`](../docs/code-signing.md)).

1. Bump `next-version` in `GitVersion.yml` on `develop` (`release: set GitVersion next-version to vX.Y.Z ...`) and merge/fast-forward it to `main`.
2. `git tag -a vX.Y.Z <commit> -m "MCEC vX.Y.Z"` on that commit, then `git push origin vX.Y.Z`.
3. Pushing the tag triggers [`.github/workflows/release.yml`](../.github/workflows/release.yml): it builds via `Release.ps1`, signs `mcec.Setup.exe` with Azure Trusted Signing (a hard gate: the job fails rather than publish an unsigned installer if signing fails or can't be verified), and publishes the GitHub Release with the signed installer attached.
4. On a stable (non-prerelease) tag, a follow-up job attempts to submit the new version to `winget-pkgs`.

To build a local, **unsigned** installer for testing only: `pwsh ./Release.ps1 -Version X.Y.Z` (produces `src/bin/mcec.Setup.exe`; Windows SmartScreen will warn on it).

## Unit Tests

* uses xUnit; run with `dotnet test tests/MCEControl.xUnit/MCEControl.xUnit.csproj`


