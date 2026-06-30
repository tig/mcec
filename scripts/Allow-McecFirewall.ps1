<#
.SYNOPSIS
    Add persistent Windows Firewall "allow" rules for mcec.exe so it never prompts on launch.

.DESCRIPTION
    MCEC opens listeners on startup, which makes Windows Defender Firewall pop a
    "Do you want to allow..." prompt the first time each distinct mcec.exe path runs.
    For repeatable / unattended dogfood runs (issues #98, #99) that prompt must not appear.

    This script creates inbound Allow rules (program-scoped, all profiles) for the mcec.exe
    locations we run from:
      - the dogfood runner's stable run dir (%LOCALAPPDATA%\Kindel\mcec-skeleton-run)
      - the Debug build output
      - the Release build output

    It is idempotent (existing rules with the same names are replaced) and self-elevates
    via UAC if not already running as Administrator. Re-run it after changing build paths.

.NOTES
    Program-scoped rules allow mcec.exe to receive inbound traffic on any port — the same
    grant the interactive "Allow access" button would create. Remove with:
        Get-NetFirewallRule -DisplayName 'MCEC (Kindel)*' | Remove-NetFirewallRule
#>
[CmdletBinding()]
param(
    [switch]$Remove
)

$ErrorActionPreference = "Stop"

# --- Self-elevate if needed ---
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)) {
    Write-Host "Administrator rights required — relaunching with UAC..."
    $hostExe = (Get-Process -Id $PID).Path   # the current pwsh/powershell host
    $argList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    if ($Remove) { $argList += "-Remove" }
    Start-Process -FilePath $hostExe -Verb RunAs -ArgumentList $argList
    return
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$targets = [ordered]@{
    "MCEC (Kindel) - Dogfood Run" = Join-Path $env:LOCALAPPDATA "Kindel\mcec-skeleton-run\mcec.exe"
    "MCEC (Kindel) - Debug Build" = Join-Path $repoRoot "src\bin\Debug\net10.0-windows\mcec.exe"
    "MCEC (Kindel) - Release Build" = Join-Path $repoRoot "src\bin\Release\net10.0-windows\mcec.exe"
}

foreach ($name in $targets.Keys) {
    $path = $targets[$name]

    # Idempotent: drop any existing rule with this display name first.
    Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule

    if ($Remove) {
        Write-Host "Removed rule: $name"
        continue
    }

    New-NetFirewallRule `
        -DisplayName $name `
        -Description "Allow mcec.exe inbound so MCEC does not prompt on launch ($path)." `
        -Direction Inbound `
        -Action Allow `
        -Program $path `
        -Profile Any `
        -Enabled True | Out-Null
    Write-Host "Allowed: $name -> $path"
}

Write-Host ""
Write-Host "Done. Current MCEC firewall rules:"
Get-NetFirewallRule -DisplayName 'MCEC (Kindel)*' |
    Select-Object DisplayName, Direction, Action, Enabled | Format-Table -AutoSize
