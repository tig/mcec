<#
.SYNOPSIS
    Builds a release of MCEC: a versioned, self-contained publish plus
    the NSIS installer (mcec.Setup.exe); in one command.

.EXAMPLE
    ./Release.ps1 -Version 2.5.0

.NOTES
    Produces a self-contained build (the .NET runtime is bundled, so end users need
    no .NET install). The version you pass is stamped into the assembly, so the app
    reports it correctly and the in-app updater works.

    After running, create the GitHub release and upload the installer, e.g.:
      git tag -a v2.5.0 -m "MCEC v2.5.0"; git push origin v2.5.0
      gh release create v2.5.0 --title "MCEC v2.5.0" --notes-file notes.md `
        --latest src/bin/mcec.Setup.exe
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src\MCEControl.csproj'
$publish = Join-Path $root 'src\bin\publish'
$setup = Join-Path $root 'src\bin\mcec.Setup.exe'
$assemblyVersion = "$Version.0"   # AssemblyVersion/FileVersion want 4 parts

Write-Host "==> Publishing self-contained $Runtime build, version $Version ..." -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
# DisableGitVersionTask + explicit version => deterministic, clean version (no git suffix),
# which the updater requires (Application.ProductVersion must parse as a Version).
dotnet publish $proj -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=false `
    -p:DisableGitVersionTask=true `
    -p:Version=$Version `
    -p:AssemblyVersion=$assemblyVersion `
    -p:FileVersion=$assemblyVersion `
    -p:InformationalVersion=$Version `
    -p:IncludeSourceRevisionInInformationalVersion=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# Locate makensis (NSIS)
$makensis = (Get-Command makensis.exe -ErrorAction SilentlyContinue).Source
if (-not $makensis) {
    foreach ($p in @("${env:ProgramFiles(x86)}\NSIS\makensis.exe", "$env:ProgramFiles\NSIS\makensis.exe")) {
        if (Test-Path $p) { $makensis = $p; break }
    }
}
if (-not $makensis) { throw "makensis.exe not found. Install NSIS (https://nsis.sourceforge.io) or add it to PATH." }

Write-Host "==> Building installer with $makensis ..." -ForegroundColor Cyan
if (Test-Path $setup) { Remove-Item -Force $setup }
$nsi = Join-Path $root 'Installer\MCEController.nsi'
& $makensis "/DVERSION=$Version" "/DPUBLISHDIR=$publish" "/DOUTFILE=$setup" $nsi
if ($LASTEXITCODE -ne 0) { throw "makensis failed ($LASTEXITCODE)" }

$sizeMB = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "==> Done. Installer: $setup ($sizeMB MB), version $Version" -ForegroundColor Green
