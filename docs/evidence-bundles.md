# MCEC evidence bundles

Every MCEC dogfood run (Customer 0 self-dogfood, Customer 1 WinPrint, future benchmarks) produces
the **same** evidence bundle, so a failed run can be understood without rerunning it and a bundle can
be attached to an issue as-is. The bundle is produced by [`scripts/McecEvidence.psm1`](../scripts/McecEvidence.psm1)
— any runner that imports the module emits an identical layout.

## Layout

```
<artifactDir>/                      e.g. artifacts/customer0/20260629-195442-5f19c9c01a3f/
  session.json          session record: id, scenario, ordered steps, pass/fail, last observation
  tool-calls.jsonl      one JSON object per MCP request/response (image bytes redacted)
  environment.json      OS, .NET, display, DPI, app version, host
  failure-summary.md    PASS/FAIL, last good observation, the failing step
  screenshot*.png ...    observation artifacts the runner writes into the directory
<artifactDir>.zip                   the same directory, zipped for issue attachment
```

`artifacts/` is git-ignored — bundles are run output, and screenshots may contain desktop contents.

## Files

### `session.json`
```jsonc
{
  "sessionId": "5f19c9c01a3f",
  "scenario": "customer0-walking-skeleton",
  "issue": 98,
  "startedAt": "20260629-195442",
  "passed": true,
  "lastObservation": "About window appeared: 'About' handle=10553630",
  "steps": [ { "name": "observe", "status": "pass", "detail": "...", "at": "<iso8601>" } ],
  "toolCallLogTruncated": false,
  "artifacts": [ "environment.json", "failure-summary.md", "screenshot.png", "tool-calls.jsonl" ]
}
```

### `tool-calls.jsonl`
One JSON object per line, in call order — the replay transcript:
```jsonc
{ "ts": "<iso8601>", "sessionId": "...", "direction": "request|response", "method": "tools/call", "payload": { ... } }
```
`payload` is the raw JSON-RPC request or response. Base64 image data in `capture` responses is replaced
with `"<redacted base64>"` — the PNG is kept as a separate artifact instead, which keeps the transcript
small and avoids embedding screen contents in the log.

### `environment.json`
`os`, `dotnet`, `mcecVersion`, `display` (WxH), `dpi`, `host`, `mcpUrl` — captured by `Get-McecEnvironment`.

### `failure-summary.md`
Always written. On failure it names the failing step and the **last good observation** so another agent
can triage without watching the run.

## Producing a bundle

```powershell
Import-Module ./scripts/McecEvidence.psm1
$s = New-McecSession -Scenario "winprint-hero-gif" -Issue 84 -ArtifactRoot ./artifacts/customer1
Add-McecStep $s "launch" "pass" "winprint up"
Write-McecToolCall $s "request" "tools/call" $req      # called from your MCP wrapper
# ... write screenshots into $s.ArtifactDir ...
$s.LastObservation = "hero frames captured"
Complete-McecSession -Session $s -Passed $true -Environment (Get-McecEnvironment -ExePath $exe -McpUrl $url)
```

## Size and privacy

- **Size:** the transcript is capped at 8 MB (`$MaxToolCallBytes`); past that it stops appending and sets
  `toolCallLogTruncated: true` in `session.json`. Image bytes never enter the transcript.
- **Privacy:** screenshots can contain whatever is on screen — treat bundles as sensitive, and `artifacts/`
  is git-ignored. Bind addresses and credentials are not collected.

## Replay / export (partial)

`tool-calls.jsonl` is an ordered, replayable transcript: each `request` line is a literal JSON-RPC call
that can be re-POSTed to a fresh MCEC `/mcp` to reproduce the sequence, and each `response` line is the
expected result to diff against. A full deterministic replayer (timing, window-handle remapping, screenshot
diffing) is future work; the format is stable enough to build it on. GIF recordings land as additional
`*.gif` artifacts in the same bundle.
