<#
.SYNOPSIS
    MCEC 3.0 Customer 0 "walking skeleton" (issue #98).

.DESCRIPTION
    The thinnest end-to-end agent slice, driven through MCEC's own MCP HTTP surface —
    MCEC drives MCEC. Every layer is present in its minimal form (stub of the layer epic):

      session (#86)    : runner-owned sessionId + per-session artifact dir
      selector (#88)   : target the MCEC GUI window by process name
      wait (#89)       : poll `query` until the main window appears
      observe (#90)    : `capture` + a runner-side non-blank PNG check
      act (#91)        : `invoke` Help > About via UIA
      verify           : poll for an "About" window
      trace (#85/#87)  : session.json + tool-calls.jsonl + screenshot.png +
                         environment.json + failure-summary.md, zipped into a bundle

    Non-goals (live in their Wave 2+ epics): selector stability, rich waits, input
    fallbacks, multi-monitor/DPI, GIF. This is a happy-path slice only.

    A bundle is ALWAYS produced — including on failure, where failure-summary.md is the
    deliverable. Exit code is 0 on a verified pass, non-zero otherwise.

.PARAMETER Port
    Localhost MCP HTTP port. Default 5151.

.PARAMETER Configuration
    Build configuration to run (Debug/Release). Default Debug.

.PARAMETER ArtifactRoot
    Where session bundles are written. Default <repo>/artifacts/customer0.
#>
param(
    [int]$Port = 5151,
    [ValidateSet("Debug", "Release")][string]$Configuration = "Debug",
    [string]$ArtifactRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
$repoRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repoRoot "src/bin/$Configuration/net10.0-windows"
$exe = Join-Path $buildDir "mcec.exe"

if (-not (Test-Path $exe)) {
    throw "mcec.exe not found at $exe — build first: dotnet build src/MCEControl.csproj -c $Configuration"
}
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $repoRoot "artifacts/customer0" }

$sessionId = ([guid]::NewGuid()).ToString("N").Substring(0, 12)
$stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
$artifactDir = Join-Path $ArtifactRoot "$stamp-$sessionId"
# Stable run dir (NOT a per-run temp path): the mcec.exe path is then constant across runs,
# so a single Windows Firewall allow rule (scripts/Allow-McecFirewall.ps1) suppresses prompts
# for every run instead of Windows re-prompting for each new temp copy.
$runDir = Join-Path $env:LOCALAPPDATA "Kindel\mcec-skeleton-run"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$toolCallLog = Join-Path $artifactDir "tool-calls.jsonl"
$mcpUrl = "http://127.0.0.1:$Port/mcp"
$baseUrl = "http://127.0.0.1:$Port/"

$steps = [System.Collections.Generic.List[object]]::new()
$lastGoodObservation = $null
$guiProc = $null
$rpcId = 0

function Add-Step {
    param([string]$Name, [string]$Status, [string]$Detail = "")
    $steps.Add([ordered]@{ name = $Name; status = $Status; detail = $Detail; at = (Get-Date).ToString("o") })
    Write-Host ("[{0,-7}] {1} {2}" -f $Status.ToUpper(), $Name, $Detail)
}

# ---------------------------------------------------------------------------
# MCP JSON-RPC over HTTP, with full request/response logging to tool-calls.jsonl
# ---------------------------------------------------------------------------
function Invoke-Mcp {
    param([string]$Method, [hashtable]$Params, [int]$TimeoutSec = 30)
    $script:rpcId++
    $req = @{ jsonrpc = "2.0"; id = $script:rpcId; method = $Method; params = $Params }
    $body = $req | ConvertTo-Json -Depth 12 -Compress
    $entry = [ordered]@{ ts = (Get-Date).ToString("o"); sessionId = $sessionId; direction = "request"; method = $Method; payload = $req }
    ($entry | ConvertTo-Json -Depth 12 -Compress) | Add-Content -Path $toolCallLog -Encoding utf8

    $resp = Invoke-RestMethod -Uri $mcpUrl -Method Post -Body $body -ContentType "application/json" -TimeoutSec $TimeoutSec
    $rentry = [ordered]@{ ts = (Get-Date).ToString("o"); sessionId = $sessionId; direction = "response"; method = $Method; payload = $resp }
    ($rentry | ConvertTo-Json -Depth 20 -Compress) | Add-Content -Path $toolCallLog -Encoding utf8
    return $resp
}

# Call an MCP tool and return the parsed structured JSON payload (content[0].text).
function Invoke-Tool {
    param([string]$Name, [hashtable]$Arguments = @{}, [int]$TimeoutSec = 30)
    $resp = Invoke-Mcp -Method "tools/call" -Params @{ name = $Name; arguments = $Arguments } -TimeoutSec $TimeoutSec
    if ($null -ne ($resp.PSObject.Properties['error']) -and $resp.error) {
        throw "MCP tool '$Name' error: $($resp.error | ConvertTo-Json -Compress)"
    }
    $text = $null
    if ($resp.result -and $resp.result.content) {
        $textNode = $resp.result.content | Where-Object { $_.type -eq "text" } | Select-Object -First 1
        if ($textNode) { $text = $textNode.text }
    }
    if (-not $text) { return $null }
    $obj = $null
    try { $obj = ($text | ConvertFrom-Json) } catch { return $text }
    # Agent commands wrap their payload as { success, command, data:{...} }.
    if ($obj.PSObject.Properties['success'] -and -not $obj.success) {
        throw "MCP tool '$Name' returned success=false: $($obj | ConvertTo-Json -Depth 6 -Compress)"
    }
    if ($obj.PSObject.Properties['data']) { return $obj.data }
    return $obj
}

# ---------------------------------------------------------------------------
# Non-blank PNG validation (stub of observation hardening, #90)
# ---------------------------------------------------------------------------
function Test-PngNonBlank {
    param([string]$Path)
    Add-Type -AssemblyName System.Drawing
    $bmp = [System.Drawing.Bitmap]::FromFile($Path)
    try {
        $w = $bmp.Width; $h = $bmp.Height
        if ($w -lt 2 -or $h -lt 2) { return $false }
        $colors = @{}
        $n = 12
        for ($i = 1; $i -lt $n; $i++) {
            for ($j = 1; $j -lt $n; $j++) {
                $x = [int]($w * $i / $n); $y = [int]($h * $j / $n)
                $c = $bmp.GetPixel($x, $y)
                $colors["$($c.R),$($c.G),$($c.B)"] = $true
            }
        }
        # A real window has many distinct colors; an all-black/blank frame has 1-2.
        return ($colors.Keys.Count -ge 5)
    }
    finally { $bmp.Dispose() }
}

function Wait-For {
    param([scriptblock]$Condition, [int]$TimeoutMs = 15000, [int]$IntervalMs = 500)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        try { $r = & $Condition; if ($r) { return $r } } catch { }
        Start-Sleep -Milliseconds $IntervalMs
    }
    return $null
}

# Force-kill every mcec.exe launched from $dir and wait for them to exit, so a modal dialog left
# open by a prior run (the invoke-on-modal blocks the call but the process is still killable) can't
# bleed into the next run.
function Stop-RunDirMcec {
    param([string]$dir)
    $sel = { Get-Process -Name "mcec" -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -and $_.Path.StartsWith($dir) } catch { $false } } }
    & $sel | Stop-Process -Force -ErrorAction SilentlyContinue
    $deadline = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $deadline -and (& $sel)) { Start-Sleep -Milliseconds 200 }
}

# ===========================================================================
# MAIN
# ===========================================================================
$passed = $false
try {
    # --- Provision an isolated, MCP-enabled config by copying the build output ---
    Add-Step "provision" "run" "copy build output -> $runDir"
    if (Test-Path $runDir) {
        Stop-RunDirMcec $runDir
        Remove-Item -LiteralPath $runDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $runDir) | Out-Null
    Copy-Item -Path $buildDir -Destination $runDir -Recurse -Force
    $runExe = Join-Path $runDir "mcec.exe"
    $settingsFile = Join-Path $runDir "mcec.settings"
    $commandsFile = Join-Path $runDir "mcec.commands"
    Remove-Item -Path $settingsFile, $commandsFile -ErrorAction SilentlyContinue

    # Write a minimal mcec.settings. AppSettings has no [XmlRoot], so the root element is
    # <AppSettings>; any elements we omit deserialize to their defaults. We only need the
    # two gates on and the port set.
    @"
<?xml version="1.0" encoding="utf-8"?>
<AppSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <AgentCommandsEnabled>true</AgentCommandsEnabled>
  <McpServerEnabled>true</McpServerEnabled>
  <McpBindAddress>127.0.0.1</McpBindAddress>
  <McpHttpPort>$Port</McpHttpPort>
  <HideOnStartup>false</HideOnStartup>
  <DisableUpdatePopup>true</DisableUpdatePopup>
</AppSettings>
"@ | Set-Content -Path $settingsFile -Encoding utf8

    # Enable exactly the four agent commands (matching-Cmd user entries replace the
    # disabled built-ins). version present => no legacy "enable all" MessageBox.
    @'
<?xml version="1.0" encoding="utf-8"?>
<MCEController version="99.0.0"
               xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<Commands xmlns="http://www.kindel.com/products/mcecontroller">
  <query Cmd="query" Enabled="true" />
  <capture Cmd="capture" Enabled="true" />
  <find Cmd="find" Enabled="true" />
  <invoke Cmd="invoke" Enabled="true" />
</Commands>
</MCEController>
'@ | Set-Content -Path $commandsFile -Encoding utf8
    Add-Step "provision" "pass" "MCP + 4 agent commands enabled"

    # --- Launch the MCEC GUI (the target AND the MCP host: MCEC drives MCEC) ---
    Add-Step "launch" "run" "starting MCEC GUI"
    $guiProc = Start-Process -FilePath $runExe -WorkingDirectory $runDir -PassThru

    # --- Wait for the MCP HTTP server to come up ---
    $up = Wait-For -TimeoutMs 30000 -IntervalMs 750 -Condition {
        try { Invoke-RestMethod -Uri $baseUrl -Method Get -TimeoutSec 3 | Out-Null; return $true }
        catch { return ($_.Exception.Response -ne $null) }  # any HTTP response = listener is up
    }
    if (-not $up) { throw "MCP HTTP server did not come up on $mcpUrl within 30s" }
    Invoke-Mcp -Method "tools/list" -Params @{} | Out-Null
    Add-Step "launch" "pass" "MCP HTTP up on $mcpUrl"

    # --- Wait for the main window (selector + wait stubs). Match the title exactly ("MCEC") and
    #     capture its handle, then target everything else by that handle — so a stale 'About'
    #     dialog (which also belongs to process mcec) can never be mistaken for the main window. ---
    $win = Wait-For -TimeoutMs 20000 -Condition {
        $q = Invoke-Tool -Name "query" -Arguments @{ window = "MCEC"; maxDepth = 1 }
        if ($q -and $q.window -and $q.window.handle -and $q.window.title -eq "MCEC") { return $q } else { return $null }
    }
    if (-not $win) { throw "MCEC main window did not appear via query within 20s" }
    $mainHandle = $win.window.handle
    $lastGoodObservation = "main window: '$($win.window.title)' handle=$mainHandle"
    Add-Step "wait-window" "pass" $lastGoodObservation

    # --- Observe: capture + non-blank validation (target the main window by handle) ---
    $shot = Join-Path $artifactDir "screenshot.png"
    $cap = Invoke-Tool -Name "capture" -Arguments @{ handle = $mainHandle; file = $shot }
    if (-not (Test-Path $shot)) { throw "capture did not produce $shot" }
    if (-not (Test-PngNonBlank $shot)) { throw "captured frame failed non-blank validation (likely black/blank)" }
    $lastGoodObservation = "non-blank capture $($cap.width)x$($cap.height) -> screenshot.png"
    Add-Step "observe" "pass" $lastGoodObservation

    # --- Act: open the Help menu with `expand` (ExpandCollapse, falling back to Invoke for the
    #     WinForms ToolStripMenuItem), then invoke About... Once Help is expanded its sub-items
    #     appear as descendants of the SAME main window (MCEC > Help > Menu > About...), so we
    #     target the main window by handle — not the foreground. (This is the menu gap the first
    #     skeleton run surfaced; the expand action + this targeting is the fix.) ---
    Invoke-Tool -Name "invoke" -Arguments @{ handle = $mainHandle; by = "name"; value = "Help"; action = "expand" } | Out-Null
    Start-Sleep -Milliseconds 400
    # Invoking About... opens a modal dialog. Since #105 the invoke returns promptly with
    # modalPending instead of blocking for the dialog's lifetime, so no special timeout handling is
    # needed; verify confirms the dialog opened.
    $aboutInvoke = Invoke-Tool -Name "invoke" -Arguments @{ handle = $mainHandle; by = "name"; value = "About..."; action = "invoke" }
    $actNote = "expand Help -> invoke About... (modalPending=$($aboutInvoke.modalPending))"

    $about = Wait-For -TimeoutMs 6000 -Condition {
        $q = Invoke-Tool -Name "query" -Arguments @{ window = "About"; maxDepth = 1 }
        if ($q -and $q.window -and $q.window.handle) { return $q } else { return $null }
    }
    Add-Step "act" ($(if ($about) { "pass" } else { "fail" })) $actNote

    # --- Verify ---
    if ($about) {
        $lastGoodObservation = "About window appeared: '$($about.window.title)' handle=$($about.window.handle)"
        Add-Step "verify" "pass" $lastGoodObservation
        $passed = $true
    } else {
        Add-Step "verify" "fail" "no 'About' window appeared after invoke (menu-dropdown automation gap?)"
    }
}
catch {
    Add-Step "error" "fail" $_.Exception.Message
}
finally {
    # --- environment.json ---
    $screen = $null
    try { Add-Type -AssemblyName System.Windows.Forms; $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds } catch {}
    $mcecVer = try { (Get-Item $exe).VersionInfo.ProductVersion } catch { "unknown" }
    $env = [ordered]@{
        sessionId   = $sessionId
        timestamp   = (Get-Date).ToString("o")
        os          = "$([System.Environment]::OSVersion.VersionString)"
        dotnet      = "$([System.Environment]::Version)"
        mcecVersion = $mcecVer
        display     = if ($screen) { "$($screen.Width)x$($screen.Height)" } else { "unknown" }
        host        = $env:COMPUTERNAME
        mcpUrl      = $mcpUrl
    }
    ($env | ConvertTo-Json -Depth 5) | Set-Content -Path (Join-Path $artifactDir "environment.json") -Encoding utf8

    # --- session.json ---
    $session = [ordered]@{
        sessionId = $sessionId
        scenario  = "customer0-walking-skeleton"
        issue     = 98
        startedAt = $stamp
        passed    = $passed
        steps     = $steps
        artifacts = @("session.json", "tool-calls.jsonl", "screenshot.png", "environment.json", "failure-summary.md")
    }
    ($session | ConvertTo-Json -Depth 8) | Set-Content -Path (Join-Path $artifactDir "session.json") -Encoding utf8

    # --- failure-summary.md (always written; says PASS when green) ---
    $fail = $steps | Where-Object { $_.status -eq "fail" } | Select-Object -First 1
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# Customer 0 Walking Skeleton — $($(if($passed){'PASS'}else{'FAIL'}))")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- Session: ``$sessionId``")
    [void]$sb.AppendLine("- Scenario: customer0-walking-skeleton (#98)")
    [void]$sb.AppendLine("- Last good observation: $lastGoodObservation")
    if ($fail) {
        [void]$sb.AppendLine("- Failing step: **$($fail.name)** — $($fail.detail)")
    }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Steps")
    foreach ($s in $steps) { [void]$sb.AppendLine("- [$($s.status)] $($s.name): $($s.detail)") }
    $sb.ToString() | Set-Content -Path (Join-Path $artifactDir "failure-summary.md") -Encoding utf8

    # --- Cleanup: stop the GUI (also closes the About dialog), remove the run dir ---
    if ($guiProc) { try { Stop-Process -Id $guiProc.Id -Force -ErrorAction SilentlyContinue } catch {} }
    Stop-RunDirMcec $runDir
    try { Remove-Item -Path $runDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}

    # --- Zip the bundle ---
    $bundle = Join-Path $ArtifactRoot "$stamp-$sessionId.zip"
    try { Compress-Archive -Path (Join-Path $artifactDir '*') -DestinationPath $bundle -Force; Add-Step "bundle" "pass" $bundle } catch { Add-Step "bundle" "fail" $_.Exception.Message }

    Write-Host ""
    Write-Host "==================================================================="
    Write-Host (" RESULT: {0}" -f $(if ($passed) { "PASS" } else { "FAIL" }))
    Write-Host (" Bundle: {0}" -f $artifactDir)
    Write-Host "==================================================================="
}

exit $(if ($passed) { 0 } else { 1 })
