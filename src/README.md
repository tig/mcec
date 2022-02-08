# Build & Deploy Info

## Pre-requisites

* Visual Studio 2022 or greater
* NSIS 3.x - `winget install nsis`

## Versions & Updates

Upon build

* the build (major.minor.rev.build) in `Installer/version.txt` is bumped.
* `src/AssemblyFileVersion.tt` is processed by the T4 compiler. This generates `src/AssemblyFileVersion.cs` and updates `Installer/version.txt`.

Releases are published at https://github.com/tig/mcec/releases

Debug builds check for pre-releases and there should ALWAYS be a fake, NEWER pre-release like https://github.com/tig/mcec/releases/tag/v2.3.6.0 to force testing of the update system.

## Installer

* `Installer\MCEController Setup.exe`
* Built using NSIS. A post-build event runs `makensis.exe` against `Installer/Installer.nsi`

## How to Release a new version

1. Rebuild All for Release
1. `cat Installer/version.txt`
    * E.g. `2.2.10.2`
1. `git tag -a -m "Release v2.2.10" v2.2.10.2`
1. `git add .`
1. `git commit -n "Release v2.2.10"`
1. `git push --tags --all`
1. On [Releases page](https://github.com/tig/mcec/releases) "Draft New Release"
   * Title of form "MCE Controller Version 2.2.10"
   * Auto generate release notes and edit as needed.
   * Add the following to end:

````
To install, copy and paste the following command into a PowerShell command window.


```powershell
$mcecv="v2.2.10.2"; $mcec="MCEController.Setup.exe"; iwr https://github.com/tig/mcec/releases/download/$mcecv/$mcec -outfile "$env:temp\$mcec"; start "$env:temp\$mcec"
```
````


## Unit Tests

* uses xUnit


