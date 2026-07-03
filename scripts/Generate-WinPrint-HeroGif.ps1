#requires -version 7
<#
.SYNOPSIS
  Customer 1 (issue #84): MCEC drives WinPrint through a desktop-visible hero GIF.

.DESCRIPTION
  Run from the tig/winprint repo root. Produces docs/hero-gui-win.gif via MCEC MCP tools.

  Prerequisites (operator):
    - MCEC installed (winget install Kindel.mcec when published; setup.exe until then).
      Default install dir: C:\Program Files\Kindel\MCEC
    - WinPrint installed so Start Menu search finds "WinPrint".
    - Interactive, unlocked Windows session.

  Harness-only (not MCP choreography):
    - Copies the installed MCEC into a disposable session dir (issue #138 will replace this
      with provision-session; avoids mutating the core install's mcec.settings/commands).
    - Remove-Item winprintdemo.pdf before record start (until #138 makes demo prep semi-automatic).

.PARAMETER WinPrintRoot
  WinPrint repo root. Defaults to the current directory.

.PARAMETER McecInstallDir
  Core MCEC install folder containing mcec.exe. When omitted, the harness discovers an existing
  install (does not download or install MCEC).
#>
[CmdletBinding()]
param(
    [string]$WinPrintRoot = (Get-Location).Path,
    [string]$McecInstallDir = '',
    [string]$PdfPath = (Join-Path $env:USERPROFILE 'Documents\winprintdemo.pdf'),
    [string]$ArtifactRoot = ''
)

$ErrorActionPreference = 'Stop'

function Resolve-McecInstall {
    param([string]$ExplicitDir)
    if ($ExplicitDir) {
        $exe = Join-Path $ExplicitDir 'mcec.exe'
        if (Test-Path -LiteralPath $exe) {
            return @{ Dir = (Resolve-Path $ExplicitDir).Path; Exe = (Resolve-Path $exe).Path; Source = 'parameter' }
        }
        throw "mcec.exe not found at $exe"
    }

    $candidates = [ordered]@{}
    $candidates['Program Files'] = Join-Path ${env:ProgramFiles} 'Kindel\MCEC'
    if (${env:ProgramFiles(x86)}) {
        $candidates['Program Files (x86)'] = Join-Path ${env:ProgramFiles(x86)} 'Kindel\MCEC'
    }

    $cmd = Get-Command mcec.exe -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        $candidates['PATH'] = Split-Path $cmd.Source -Parent
    }

    try {
        $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
        if ($winget) {
            $list = & winget list --id Kindel.mcec --accept-source-agreements 2>$null
            if ($LASTEXITCODE -eq 0 -and ($list -match 'Kindel\.mcec')) {
                $candidates['winget (expected)'] = Join-Path ${env:ProgramFiles} 'Kindel\MCEC'
            }
        }
    } catch {}

    foreach ($kv in $candidates.GetEnumerator()) {
        $dir = $kv.Value
        $exe = Join-Path $dir 'mcec.exe'
        if ((Test-Path -LiteralPath $dir) -and (Test-Path -LiteralPath $exe)) {
            return @{ Dir = (Resolve-Path $dir).Path; Exe = (Resolve-Path $exe).Path; Source = $kv.Key }
        }
    }

    $searched = ($candidates.Values | Sort-Object -Unique) -join '; '
    throw @"
MCEC is not installed (searched: $searched).

Install it first, then re-run this script:
  winget install Kindel.mcec          # when published to winget
  # or run the signed MCEC setup.exe from the release page until then

Do not build from source for hero runs. Pass -McecInstallDir if you installed to a custom location.
"@
}
$WinPrintRoot = (Resolve-Path $WinPrintRoot).Path
$outGif = Join-Path $WinPrintRoot 'docs\hero-gui-win.gif'
$samplePath = (Join-Path $WinPrintRoot 'src\WinPrint.Core\ViewModels\SheetViewModel.cs')
$readmePath = Join-Path $WinPrintRoot 'README.md'
foreach ($p in @($samplePath, $readmePath)) {
    if (-not (Test-Path $p)) { throw "Required file missing: $p (run from the winprint repo root)" }
}
$samplePath = (Resolve-Path $samplePath).Path
$readmePath = (Resolve-Path $readmePath).Path

$mcecInstall = Resolve-McecInstall -ExplicitDir $McecInstallDir
Write-Host ("MCEC found: {0} (via {1})" -f $mcecInstall.Dir, $mcecInstall.Source)
$McecInstallDir = $mcecInstall.Dir
$coreExe = $mcecInstall.Exe

$mcecRoot = Split-Path $PSScriptRoot -Parent
$artifactRoot = if ($ArtifactRoot) { $ArtifactRoot } else { Join-Path $mcecRoot 'artifacts\customer1' }

# Disposable session copy — do not mutate the core install (see issue #138).
$sessionDir = Join-Path $env:LOCALAPPDATA 'MCEC\sessions\winprint-hero'
if (Test-Path $sessionDir) { Remove-Item $sessionDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $sessionDir | Out-Null
Copy-Item (Join-Path $McecInstallDir '*') $sessionDir -Recurse -Force
$ctrlDir = $sessionDir
$mcecExe = Join-Path $ctrlDir 'mcec.exe'

Import-Module (Join-Path $PSScriptRoot 'McecEvidence.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'McecMcpClient.psm1') -Force

$rx = 0; $ry = 40; $rw = 1180; $rh = 740

$ctrlSettings = Join-Path $ctrlDir 'mcec.settings'
$ctrlCommands = Join-Path $ctrlDir 'mcec.commands'

Set-Content -Encoding UTF8 -Path $ctrlSettings -Value @'
<?xml version="1.0" encoding="utf-8"?>
<AppSettings>
  <AgentCommandsEnabled>true</AgentCommandsEnabled>
  <McpServerEnabled>true</McpServerEnabled>
  <CommandOverlayEnabled>true</CommandOverlayEnabled>
  <CommandOverlayPosition>Left</CommandOverlayPosition>
  <HideOnStartup>true</HideOnStartup>
  <ActAsServer>false</ActAsServer>
</AppSettings>
'@

Set-Content -Encoding UTF8 -Path $ctrlCommands -Value @'
<?xml version="1.0" encoding="utf-8"?>
<MCEController xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" version="3.0.0">
<Commands xmlns="http://www.kindel.com/products/mcecontroller">
  <capture Cmd="capture" Enabled="true" />
  <query   Cmd="query"   Enabled="true" />
  <find    Cmd="find"    Enabled="true" />
  <wait-for Cmd="wait-for" Enabled="true" />
  <invoke  Cmd="invoke"  Enabled="true" />
  <record  Cmd="record"  Enabled="true" />
  <drag    Cmd="drag"    Enabled="true" />
  <click   Cmd="click"   Enabled="true" />
  <clipboard Cmd="clipboard" Enabled="true" />
  <displays Cmd="displays" Enabled="true" />
  <mouse   Cmd="mouse:"  Enabled="true" />
  <chars   Cmd="chars:"  Enabled="true" />
  <sendinput Cmd="winkey" Vk="VK_LWIN" Enabled="true" />
  <sendinput Cmd="enter" Vk="VK_RETURN" Enabled="true" />
  <sendinput Cmd="key_esc" Vk="VK_ESCAPE" Enabled="true" />
  <sendinput Cmd="ctrl-a" Vk="VK_A" Ctrl="true" Enabled="true" />
  <sendinput Cmd="ctrl-v" Vk="VK_V" Ctrl="true" Enabled="true" />
  <sendinput Cmd="key_equals" Vk="0xBB" Enabled="true" />
  <sendinput Cmd="key_0" Vk="0x30" Enabled="true" />
  <sendinput Cmd="key_left" Vk="VK_LEFT" Enabled="true" />
  <sendinput Cmd="key_right" Vk="VK_RIGHT" Enabled="true" />
  <sendinput Cmd="key_up" Vk="VK_UP" Enabled="true" />
  <sendinput Cmd="key_down" Vk="VK_DOWN" Enabled="true" />
  <sendinput Cmd="desktop" Vk="VK_D" Win="true" Enabled="true" />
  <sendinput Cmd="winsearch" Vk="VK_S" Win="true" Enabled="true" />
  <sendinput Cmd="winr" Vk="r" Win="true" Enabled="true" />
  <sendinput Cmd="alt_f4" Vk="VK_F4" Alt="true" Enabled="true" />
</Commands>
</MCEController>
'@

$session = New-McecSession -Scenario 'winprint-hero-gif' -Issue 84 -ArtifactRoot $artifactRoot
$ctrl = $null
$passed = $false

function Step([string]$name, [string]$status, [string]$detail) {
    Add-McecStep -Session $session $name $status $detail
    if ($status -eq 'pass') { $script:session.LastObservation = $detail }
}

function Stop-PdfViewerHoldingFile {
    param([string]$Path)
    $leaf = [System.IO.Path]::GetFileName($Path)
    foreach ($name in @('Acrobat', 'AcroRd32', 'AcrobatDC', 'SumatraPDF', 'FoxitReader', 'PDFXEdit', 'msedge', 'MicrosoftEdge')) {
        foreach ($proc in Get-Process -Name $name -ErrorAction SilentlyContinue) {
            $title = $proc.MainWindowTitle
            if ($name -match 'edge' -and $title -and $title -notmatch 'winprintdemo' -and $title -notmatch [regex]::Escape($leaf)) {
                continue
            }
            try {
                if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
                    $proc.CloseMainWindow() | Out-Null
                    Start-Sleep -Milliseconds 400
                }
                if (-not $proc.HasExited) { $proc.Kill($true) }
            } catch {}
        }
    }
}

try {
    # Harness-only demo prep (not on-screen). Issue #138 provision-session will absorb this.
    if (Test-Path $PdfPath) {
        Remove-Item -LiteralPath $PdfPath -Force
        Step 'cleanup' 'pass' "harness removed prior $PdfPath"
    } else {
        Step 'cleanup' 'pass' "no prior $PdfPath"
    }

    $ctrl = Start-Process -FilePath $mcecExe -WorkingDirectory $ctrlDir -PassThru
    if (-not (Wait-McecMcp)) { throw 'MCEC MCP HTTP did not come up' }
    Step 'controller' 'pass' 'disposable MCEC session up'

    Invoke-McecTool 'record' @{
        action = 'start'; x = $rx; y = $ry; width = $rw; height = $rh; fps = 4; maxWidth = 880
    } -Session $session | Out-Null
    Step 'record-start' 'pass' 'region record started'
    Start-Sleep -Milliseconds 500

    # Show desktop (Win+D) so Start/search keystrokes are not swallowed by an IDE shell.
    Send-McecCommand 'desktop' $session; Start-Sleep -Milliseconds 1000
    Send-McecCommand 'winsearch' $session; Start-Sleep -Milliseconds 1200
    Send-McecCommand 'chars:WinPrint' $session; Start-Sleep -Milliseconds 1200
    Send-McecCommand 'enter' $session
    $hWin = Wait-McecWindow @{ process = 'winprint' } -Attempts 45 -DelayMs 800
    if ($hWin -eq 0) { throw 'WinPrint did not appear (is it installed and Start-searchable?)' }
    Step 'launch' 'pass' "WinPrint up handle=$hWin"
    Start-Sleep -Milliseconds 2000

    $wt = @{ handle = $hWin }

    Invoke-McecClickNameLike $wt '*File*' $session
    Start-Sleep -Milliseconds 1200
    Assert-McecToolOk (Invoke-McecTool 'clipboard' @{ action = 'set'; text = $samplePath } -Session $session) 'clipboard set sample'
    Start-Sleep -Milliseconds 300
    Send-McecCommand 'ctrl-v' $session; Start-Sleep -Milliseconds 400
    Send-McecCommand 'enter' $session; Start-Sleep -Seconds 3
    Step 'open-sample' 'pass' 'SheetViewModel.cs loaded'

    foreach ($label in @('Line Numbers', 'Line Numbers', 'Landscape', 'Landscape')) {
        Invoke-McecClickElement $wt 'name' $label $session
        Start-Sleep -Milliseconds ($(if ($label -eq 'Landscape') { 1500 } else { 1200 }))
    }
    Step 'settings' 'pass' 'line numbers + landscape toggled'

    $tree = Get-McecTree (Invoke-McecTool 'query' ($wt + @{ maxDepth = 6 }) -Session $session)
    $preview = Find-McecNode $tree { param($n) $n.controlType -match 'Pane|Custom|Group' -and $n.width -gt 400 }
    if ($preview) {
        Invoke-McecTool 'click' ($wt + @{
            at = @{
                x = [int]($preview.x + $preview.width * 0.62)
                y = [int]($preview.y + $preview.height * 0.30)
            }
        }) -Session $session | Out-Null
    }
    Start-Sleep -Milliseconds 300
    foreach ($k in 1..4) { Send-McecCommand 'key_equals' $session; Start-Sleep -Milliseconds 120 }
    Send-McecCommand 'key_down' $session; Send-McecCommand 'key_down' $session
    Send-McecCommand 'key_right' $session; Send-McecCommand 'key_right' $session
    Send-McecCommand 'key_up' $session; Send-McecCommand 'key_left' $session; Send-McecCommand 'key_left' $session
    Send-McecCommand 'key_0' $session; Start-Sleep -Milliseconds 500
    Step 'zoom' 'pass' 'zoom pan reset'

    Invoke-McecClickNameLike $wt '*File*' $session; Start-Sleep -Milliseconds 1200
    Assert-McecToolOk (Invoke-McecTool 'clipboard' @{ action = 'set'; text = $readmePath } -Session $session) 'clipboard set readme'
    Send-McecCommand 'ctrl-v' $session; Start-Sleep -Milliseconds 400
    Send-McecCommand 'enter' $session; Start-Sleep -Seconds 3
    Step 'open-readme' 'pass' 'README.md loaded'

    Send-McecCommand 'key_esc' $session; Start-Sleep -Milliseconds 600

    $tree = Get-McecTree (Invoke-McecTool 'query' ($wt + @{ maxDepth = 8 }) -Session $session)
    $printBtn = Find-McecNode $tree {
        param($n)
        $n.controlType -eq 'Button' -and $n.name -match 'Print' -and $n.y -lt 260
    }
    if (-not $printBtn) { throw 'Print toolbar button not found in WinPrint UIA tree' }
    Invoke-McecTool 'click' ($wt + @{
        at = @{
            x = [int]($printBtn.x + $printBtn.width / 2)
            y = [int]($printBtn.y + $printBtn.height / 2)
        }
    }) -Session $session | Out-Null
    Start-Sleep -Milliseconds 2500
    $hSave = Wait-McecWindow @{ window = 'Save Print Output As' } -Attempts 30 -DelayMs 600
    if ($hSave -eq 0) { $hSave = Wait-McecWindow @{ window = 'Save As' } -Attempts 15 -DelayMs 600 }
    $saveTarget = if ($hSave -ne 0) { @{ handle = $hSave } } else { @{ foreground = $true } }
    $saveTree = Get-McecTree (Invoke-McecTool 'query' ($saveTarget + @{ maxDepth = 6 }) -Session $session)
    $fileNameEdit = Find-McecNode $saveTree {
        param($n)
        $n.controlType -eq 'Edit' -and ($n.name -match 'File name' -or $n.automationId -eq '1148')
    }
    if ($fileNameEdit) {
        Invoke-McecTool 'click' ($saveTarget + @{
            at = @{
                x = [int]($fileNameEdit.x + $fileNameEdit.width / 2)
                y = [int]($fileNameEdit.y + $fileNameEdit.height / 2)
            }
        }) -Session $session | Out-Null
        Start-Sleep -Milliseconds 500
    }
    Send-McecCommand 'ctrl-a' $session; Start-Sleep -Milliseconds 200
    Send-McecCommand ("chars:" + $PdfPath.Replace('\', '\\')) $session; Start-Sleep -Milliseconds 800
    Send-McecCommand 'enter' $session; Start-Sleep -Seconds 6
    if (-not (Test-Path -LiteralPath $PdfPath)) { throw "PDF not saved at $PdfPath" }
    Step 'print-pdf' 'pass' "saved $PdfPath"

    Send-McecCommand 'winr' $session; Start-Sleep -Milliseconds 1000
    Send-McecCommand ("chars:" + $PdfPath.Replace('\', '\\')) $session; Start-Sleep -Milliseconds 600
    Send-McecCommand 'enter' $session; Start-Sleep -Seconds 3
    Step 'open-pdf' 'pass' 'PDF viewer foreground'
    Start-Sleep -Milliseconds 1500

    Invoke-McecTool 'record' @{ action = 'stop'; file = $outGif } -Session $session | Out-Null
    Step 'record-stop' 'pass' "wrote $outGif"

    # Dismiss the PDF viewer so the next run's harness-only Remove-Item winprintdemo.pdf can succeed.
    Send-McecCommand 'alt_f4' $session; Start-Sleep -Milliseconds 700
    Step 'close-pdf' 'pass' 'PDF viewer dismissed'

    $passed = $true
}
catch {
    Step 'failure' 'fail' $_.Exception.Message
    try {
        Invoke-McecTool 'capture' @{
            foreground = $true
            file       = (Join-Path $session.ArtifactDir 'failure-capture.png')
        } | Out-Null
    } catch {}
    throw
}
finally {
    Stop-PdfViewerHoldingFile -Path $PdfPath
    if ($ctrl -and -not $ctrl.HasExited) { try { $ctrl.Kill($true) } catch {} }
    foreach ($p in Get-Process -Name winprint -ErrorAction SilentlyContinue) {
        try { $p.Kill($true) } catch {}
    }
    Remove-Item $sessionDir -Recurse -Force -ErrorAction SilentlyContinue
    $env = Get-McecEnvironment -ExePath $mcecExe -McpUrl 'http://127.0.0.1:5151/mcp'
    $bundle = Complete-McecSession -Session $session -Passed $passed -Environment $env
    if ($bundle.Bundle) { Write-Host "Evidence: $($bundle.Bundle)" }
}

if (-not $passed) { exit 1 }
Write-Host "Wrote $outGif"