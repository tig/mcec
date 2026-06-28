# Local build helper for MCE Controller (MCEC).
# If a command-line build fails, open a Visual Studio Developer prompt first:
#   cmd.exe "/K" '"C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\Tools\VsDevCmd.bat" && pwsh -noexit'

param(
    [string]$Configuration = "Debug",
    [string]$Platform = "AnyCPU"
)

$ErrorActionPreference = "Stop"

Write-Host "Restoring..."
dotnet restore MCEControl.slnx

Write-Host "Building ($Configuration / $Platform)..."
dotnet build MCEControl.slnx --configuration $Configuration --no-restore

Write-Host "Testing..."
dotnet test MCEControl.slnx --configuration $Configuration --no-build
