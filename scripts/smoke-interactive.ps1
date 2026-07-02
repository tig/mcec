<#
.SYNOPSIS
  Trivial unattended smoke for interactive Windows runner.
.DESCRIPTION
  Launches built mcec.exe --mcp with minimal agent settings.
  Sends capture request for the main window.
  Asserts the PNG frame is non-blank using actual image analysis.
  Reports cleanly with GitHub Actions notice if no interactive desktop.
.PARAMETER Force
  Force the smoke test even if desktop detection says non-interactive (for local validation on desktop machines).
#>
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$isInteractive = [System.Windows.Forms.SystemInformation]::UserInteractive
$proc = [System.Diagnostics.Process]::GetCurrentProcess()
$isSession0 = ($proc.SessionId -eq 0) -or ($env:SESSIONNAME -eq 'Console')

if ((-not $isInteractive -or $isSession0) -and -not $Force) {
    Write-Host "::notice::No interactive desktop available (session-0 or non-interactive runner). Smoke skipped cleanly. This is expected on GitHub-hosted windows-latest."
    exit 0
}

if ($Force) {
    Write-Host "Force mode enabled - running smoke regardless of desktop detection."
}

Write-Host "Running interactive smoke test..."

$buildDir = "src\bin\Release\net10.0-windows"
$exe = Join-Path $buildDir "mcec.exe"
if (-not (Test-Path $exe)) { 
    throw "Built mcec.exe not found at $exe (did Build succeed?)" 
}

$temp = Join-Path $env:TEMP ("mcec-smoke-" + [Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $temp -Force | Out-Null
Copy-Item (Join-Path $buildDir "*") $temp -Recurse -Force

# Minimal settings to enable capture (Mcp false since --mcp stdio)
$settings = @"
<?xml version="1.0" encoding="utf-8"?>
<AppSettings>
  <AgentCommandsEnabled>true</AgentCommandsEnabled>
  <McpServerEnabled>false</McpServerEnabled>
  <ActAsServer>false</ActAsServer>
  <DisableUpdatePopup>true</DisableUpdatePopup>
</AppSettings>
"@
$settings | Set-Content (Join-Path $temp "mcec.settings")

# Enable capture command
$cmds = @"
<?xml version="1.0" encoding="utf-8"?>
<MCEController xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<Commands xmlns="http://www.kindel.com/products/mcecontroller">
  <capture Cmd="capture" Enabled="true" />
</Commands>
</MCEController>
"@
$cmds | Set-Content (Join-Path $temp "mcec.commands")

Push-Location $temp
$driver = $null
try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = ".\mcec.exe"
    $psi.Arguments = "--mcp"
    $psi.WorkingDirectory = $PWD
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    
    $driver = [System.Diagnostics.Process]::Start($psi)
    $driver.BeginErrorReadLine()

    Start-Sleep -Seconds 6  # Give time for window to appear

    # Send capture
    $req = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"capture","arguments":{"window":"MCEC"}}}'
    $driver.StandardInput.WriteLine($req)
    $driver.StandardInput.Flush()

    Start-Sleep -Seconds 3
    $line = $driver.StandardOutput.ReadLine()
    Write-Host "Capture response received."

    if ($line -notmatch '"encoding":"png"') {
        throw "Smoke FAILED: capture response did not contain png data. Response: $line"
    }

    # Parse base64 PNG data
    if ($line -match '"data":\s*\{[^}]*"[^"]+"\s*:\s*"([A-Za-z0-9+/=]+)"') {
        $b64 = $matches[1]
        $bytes = [Convert]::FromBase64String($b64)
        
        if ($bytes.Length -lt 5000) {
            throw "Smoke FAILED: PNG too small (possible blank) len=$($bytes.Length)"
        }

        # Load as bitmap and check for non-blank (variance in pixel colors)
        $ms = New-Object System.IO.MemoryStream(,$bytes)
        $img = [System.Drawing.Image]::FromStream($ms)
        $bm = New-Object System.Drawing.Bitmap($img)
        
        $sampleCount = 0
        $totalDiff = 0
        $firstPixel = $bm.GetPixel(10, 10)
        
        for ($x = 20; $x -lt $bm.Width; $x += [math]::Max(1, [int]($bm.Width / 10))) {
            for ($y = 20; $y -lt $bm.Height; $y += [math]::Max(1, [int]($bm.Height / 10))) {
                if ($sampleCount -gt 20) { break }
                $c = $bm.GetPixel($x, $y)
                $diff = [math]::Abs($c.R - $firstPixel.R) + [math]::Abs($c.G - $firstPixel.G) + [math]::Abs($c.B - $firstPixel.B)
                $totalDiff += $diff
                $sampleCount++
            }
        }
        
        $avgDiff = if ($sampleCount -gt 0) { $totalDiff / $sampleCount } else { 0 }
        
        if ($avgDiff -lt 5) {
            throw "Smoke FAILED: captured frame appears blank or nearly uniform (avg color diff = $avgDiff)"
        }
        
        Write-Host "Smoke OK: non-blank frame captured (png bytes=$($bytes.Length), avgColorDiff=$avgDiff)"
    } else {
        throw "Smoke FAILED: could not extract PNG data from response"
    }

    if (-not $driver.HasExited) {
        $driver.StandardInput.Close()
        if (-not $driver.WaitForExit(5000)) { $driver.Kill($true) }
    }
} catch {
    Write-Error "Smoke test failed: $_"
    exit 1
} finally {
    Pop-Location
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    if ($driver -and -not $driver.HasExited) { $driver.Kill($true) }
}

Write-Host "Smoke completed successfully."
