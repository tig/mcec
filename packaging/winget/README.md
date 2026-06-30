# winget packaging

Makes `winget install Kindel.mcec` work by publishing manifests to the
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) community repository
(winget's default source). The files here are the manifest **templates** and the source of
truth for the one-time bootstrap; ongoing version updates are automated by CI.

## How it works

| Stage | Who | What |
|-------|-----|------|
| First version | **manual, once** | Submit the initial manifests to winget-pkgs (it requires a package to already exist before it can be auto-updated). Done for 2.4.1 — see below. |
| Every later release | **automated** | The `winget` job in `.github/workflows/release.yml` runs `winget-releaser` on each **stable** tag, opening a winget-pkgs PR with the new version + signed installer. |

The installer is Authenticode-signed (Azure Trusted Signing — see `../../docs/code-signing.md`),
which smooths winget validation (sandbox install/uninstall + SmartScreen).

## One-time setup

1. **Create the token.** A classic GitHub **PAT** with **`public_repo`** scope (fine-grained
   PATs are *not* supported by winget-releaser). Save it as the repo secret **`WINGET_TOKEN`**.
   The PAT's account must have a fork of `microsoft/winget-pkgs` (tig already does). Until this
   secret exists, the `winget` CI job is skipped (gated on `HAS_WINGET`).

2. **Bootstrap the first version** into winget-pkgs. This was done for **2.4.1** as
   [microsoft/winget-pkgs#394798](https://github.com/microsoft/winget-pkgs/pull/394798)
   (manifests under `manifests/k/Kindel/mcec/2.4.1/`). First submissions for a new
   package are **manually reviewed** by winget-pkgs moderators.

   To bootstrap another version by hand: render the templates here (replace `{{version}}`,
   `{{installerUrl}}`, `{{installerSha256}}`, `{{releaseDate}}`), `winget validate --manifest .`,
   and open a PR under `manifests/k/Kindel/mcec/<version>/`. `komac` automates this:
   ```bash
   komac update Kindel.mcec --version <v> \
     --urls https://github.com/tig/mcec/releases/download/v<v>/mcec.Setup.exe \
     --token <PAT> --submit
   ```

After the package exists in winget-pkgs and `WINGET_TOKEN` is set, **no further manual steps
are needed** — each stable release auto-submits its update.

## Values for a bootstrap submission (fill from the release)

```
PackageVersion:  2.4.1
InstallerUrl:    https://github.com/tig/mcec/releases/download/v2.4.1/MCEController.Setup.exe
InstallerSha256: 393B4066AFF168482038A7C61916B5D526617CFCED73F5D832315B3100338652
ReleaseDate:     2026-06-29
```

## Notes / things to validate on a real install

- **Installer type `nullsoft`** (NSIS), **`Scope: machine`** — installs to
  `C:\Program Files\Kindel Systems\MCEC` and requires elevation. winget's silent mode
  and the winget-pkgs sandbox use the NSIS `/S` switch.
- **`winget upgrade` correlation:** the manifests intentionally omit `AppsAndFeaturesEntries`
  for now. The NSIS installer registers its Add/Remove Programs `DisplayName` as
  "MCEC `<version>`" (includes the version). Before adding `AppsAndFeaturesEntries`,
  standardize the installer `DisplayName` to just "MCEC" (drop the version, keep
  `DisplayVersion`) so upgrade detection is stable across versions.
- Only the **x64** installer is published; add more `Installers` entries if other arches ship.
