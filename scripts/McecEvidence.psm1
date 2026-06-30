<#
.SYNOPSIS
    Standard MCEC dogfood evidence bundle (issue #87).

.DESCRIPTION
    A reusable layer that any MCEC-driven runner (Customer 0 self-dogfood, Customer 1 WinPrint, ...)
    uses to produce an identical, inspectable, zip-able evidence bundle so a failed run can be
    understood without rerunning it. Layout:

        <artifactDir>/
          session.json        - session record: id, scenario, ordered steps, pass/fail, last observation
          tool-calls.jsonl    - one JSON object per MCP request/response (base64 image bytes redacted)
          environment.json    - OS, .NET, display, DPI, app version, host
          failure-summary.md  - PASS/FAIL, last good observation, the failing step
          screenshot*.png ... - observation artifacts the runner writes into ArtifactDir
        <artifactDir>.zip     - the same directory, zipped for issue attachment

    See docs/evidence-bundles.md for the format spec and replay/export notes.
#>

# Size cap for the transcript so a long run can't produce an unbounded log.
$script:MaxToolCallBytes = 8MB

function New-McecSession {
    <#.SYNOPSIS Starts a session and allocates its artifact directory.#>
    param(
        [Parameter(Mandatory)][string]$Scenario,
        [int]$Issue = 0,
        [Parameter(Mandatory)][string]$ArtifactRoot
    )
    $id = ([guid]::NewGuid()).ToString("N").Substring(0, 12)
    $stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
    $dir = Join-Path $ArtifactRoot "$stamp-$id"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    return @{
        SessionId       = $id
        Scenario        = $Scenario
        Issue           = $Issue
        StartedAt       = $stamp
        ArtifactRoot    = $ArtifactRoot
        ArtifactDir     = $dir
        ToolCallLog     = (Join-Path $dir "tool-calls.jsonl")
        Steps           = [System.Collections.Generic.List[object]]::new()
        LastObservation = $null
        ToolCallBytes   = 0
        Truncated       = $false
    }
}

function Add-McecStep {
    <#.SYNOPSIS Records an ordered step and echoes it to the host.#>
    param([Parameter(Mandatory)]$Session, [Parameter(Mandatory)][string]$Name,
          [Parameter(Mandatory)][string]$Status, [string]$Detail = "")
    $Session.Steps.Add([ordered]@{ name = $Name; status = $Status; detail = $Detail; at = (Get-Date).ToString("o") })
    Write-Host ("[{0,-7}] {1} {2}" -f $Status.ToUpper(), $Name, $Detail)
}

function Write-McecToolCall {
    <#.SYNOPSIS Appends one request/response record to tool-calls.jsonl (base64 redacted, size-capped).#>
    param([Parameter(Mandatory)]$Session, [Parameter(Mandatory)][string]$Direction,
          [string]$Method, $Payload)
    if ($Session.Truncated) { return }
    $entry = [ordered]@{ ts = (Get-Date).ToString("o"); sessionId = $Session.SessionId; direction = $Direction; method = $Method; payload = $Payload }
    $json = ($entry | ConvertTo-Json -Depth 20 -Compress)
    # Privacy + size: keep image bytes out of the transcript — the PNG is a separate artifact. Redact by
    # field (the capture `base64` value and an image content block's `data`) regardless of length: a small
    # region capture can produce a PNG whose base64 is well under any length threshold, and a length gate
    # would leak those screen bytes into the transcript (#110).
    $json = [regex]::Replace($json, '("(?:data|base64)"\s*:\s*")[A-Za-z0-9+/=]+(")', '${1}<redacted base64>${2}')
    if ($Session.ToolCallBytes + $json.Length -gt $script:MaxToolCallBytes) {
        Add-Content -Path $Session.ToolCallLog -Value '{"truncated":"tool-call log size cap reached"}' -Encoding utf8
        $Session.Truncated = $true
        return
    }
    Add-Content -Path $Session.ToolCallLog -Value $json -Encoding utf8
    $Session.ToolCallBytes += $json.Length + 1
}

function Get-McecEnvironment {
    <#.SYNOPSIS Captures standard environment metadata for a bundle.#>
    param([string]$ExePath, [string]$McpUrl)
    $screen = $null
    try { Add-Type -AssemblyName System.Windows.Forms; $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds } catch {}
    $dpi = "unknown"
    try {
        Add-Type -AssemblyName System.Drawing
        $g = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero)
        $dpi = "$([int]$g.DpiX)"; $g.Dispose()
    } catch {}
    $ver = try { (Get-Item $ExePath).VersionInfo.ProductVersion } catch { "unknown" }
    return [ordered]@{
        os          = [System.Environment]::OSVersion.VersionString
        dotnet      = "$([System.Environment]::Version)"
        mcecVersion = $ver
        display     = if ($screen) { "$($screen.Width)x$($screen.Height)" } else { "unknown" }
        dpi         = $dpi
        host        = $env:COMPUTERNAME
        mcpUrl      = $McpUrl
    }
}

function Complete-McecSession {
    <#.SYNOPSIS Writes session.json/environment.json/failure-summary.md and zips the bundle.#>
    param([Parameter(Mandatory)]$Session, [Parameter(Mandatory)][bool]$Passed, [hashtable]$Environment)
    $dir = $Session.ArtifactDir

    $envObj = [ordered]@{ sessionId = $Session.SessionId; timestamp = (Get-Date).ToString("o") }
    if ($Environment) { foreach ($k in $Environment.Keys) { $envObj[$k] = $Environment[$k] } }
    ($envObj | ConvertTo-Json -Depth 6) | Set-Content -Path (Join-Path $dir "environment.json") -Encoding utf8

    $fail = $Session.Steps | Where-Object { $_.status -eq "fail" } | Select-Object -First 1
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# $($Session.Scenario) — $(if ($Passed) { 'PASS' } else { 'FAIL' })")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- Session: ``$($Session.SessionId)``")
    [void]$sb.AppendLine("- Scenario: $($Session.Scenario)$(if ($Session.Issue) { " (#$($Session.Issue))" })")
    [void]$sb.AppendLine("- Last good observation: $($Session.LastObservation)")
    if ($fail) { [void]$sb.AppendLine("- Failing step: **$($fail.name)** — $($fail.detail)") }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Steps")
    foreach ($s in $Session.Steps) { [void]$sb.AppendLine("- [$($s.status)] $($s.name): $($s.detail)") }
    $sb.ToString() | Set-Content -Path (Join-Path $dir "failure-summary.md") -Encoding utf8

    $sessionObj = [ordered]@{
        sessionId            = $Session.SessionId
        scenario             = $Session.Scenario
        issue                = $Session.Issue
        startedAt            = $Session.StartedAt
        passed               = $Passed
        lastObservation      = $Session.LastObservation
        steps                = $Session.Steps
        toolCallLogTruncated = $Session.Truncated
        artifacts            = @(Get-ChildItem -File $dir | Select-Object -ExpandProperty Name | Sort-Object)
    }
    ($sessionObj | ConvertTo-Json -Depth 10) | Set-Content -Path (Join-Path $dir "session.json") -Encoding utf8

    $bundle = Join-Path $Session.ArtifactRoot ("{0}-{1}.zip" -f $Session.StartedAt, $Session.SessionId)
    try { Compress-Archive -Path (Join-Path $dir '*') -DestinationPath $bundle -Force } catch { $bundle = $null }

    return [pscustomobject]@{ Passed = $Passed; ArtifactDir = $dir; Bundle = $bundle }
}

Export-ModuleMember -Function New-McecSession, Add-McecStep, Write-McecToolCall, Get-McecEnvironment, Complete-McecSession
