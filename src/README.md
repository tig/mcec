# Build & Deploy Info

## Pre-requisites

* Visual Studio 2022 or greater
* NSIS 3.x - `winget install nsis`

## Versions & Updates

Upon build

* the build (major.minor.rev.build) in `Installer/version.txt` is bumped.
* `src/AssemblyFileVersion.tt` is processed by the T4 compiler. This generates `src/AssemblyFileVersion.cs` and updates `Installer/version.txt`.

Releases are publishshed at https://github.com/tig/mcec/releases

Debug builds check for pre-releases and there should ALWAYS be a fake, NEWER pre-release like https://github.com/tig/mcec/releases/tag/v2.3.6.0 to force testing of the update system.


## Installer

* `Installer\MCEController Setup.exe`
* Built using NSIS. A post-build event runs `makensis.exe` against `Installer/Installer.nsi`

## Unit Tests

* uses xUnit


