<#
.SYNOPSIS
  Shared MCP HTTP client helpers for MCEC dogfood runners (Customer 0 hero, Customer 1 WinPrint, ...).

.DESCRIPTION
  Thin wrapper around POST :5151/mcp JSON-RPC. Parses the agent tool-result envelope from MCP text
  content blocks. Import alongside McecEvidence.psm1 for evidence bundles.
#>

$script:McpId = 0
$script:McpUrl = 'http://127.0.0.1:5151/mcp'

function Set-McecMcpUrl {
    param([Parameter(Mandatory)][string]$Url)
    $script:McpUrl = $Url
}

function Invoke-McecRpc {
    param([Parameter(Mandatory)][string]$Method, $Params)
    $script:McpId++
    $body = @{ jsonrpc = '2.0'; id = $script:McpId; method = $Method; params = $Params } | ConvertTo-Json -Depth 12 -Compress
    return Invoke-RestMethod -Uri $script:McpUrl -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 60
}

function Invoke-McecTool {
    param(
        [Parameter(Mandatory)][string]$Name,
        $Arguments = @{},
        $Session = $null
    )
    $resp = Invoke-McecRpc 'tools/call' @{ name = $Name; arguments = $Arguments }
    if ($Session) {
        Write-McecToolCall -Session $Session -Direction 'request' -Method 'tools/call' -Payload @{ name = $Name; arguments = $Arguments }
        Write-McecToolCall -Session $Session -Direction 'response' -Method 'tools/call' -Payload $resp
    }
    return $resp
}

function Send-McecCommand {
    param([Parameter(Mandatory)][string]$Command, $Session = $null)
    Invoke-McecTool 'send_command' @{ command = $Command } -Session $Session | Out-Null
}

function Get-McecAgentEnvelope {
    param($ToolResponse)
    foreach ($b in $ToolResponse.result.content) {
        if ($b.type -eq 'text') {
            try { return ($b.text | ConvertFrom-Json) } catch {}
        }
    }
    return $null
}

function Get-McecTree {
    param($ToolResponse)
    $env = Get-McecAgentEnvelope $ToolResponse
    if ($env -and $env.result -and $env.result.tree) { return $env.result.tree }
    return $null
}

function Get-McecWindow {
    param($ToolResponse)
    $env = Get-McecAgentEnvelope $ToolResponse
    if ($env -and $env.result -and $env.result.window) { return $env.result.window }
    return $null
}

function Find-McecNode {
    param($Node, [scriptblock]$Predicate)
    if (& $Predicate $Node) { return $Node }
    if ($Node.children) {
        foreach ($c in $Node.children) {
            $r = Find-McecNode $c $Predicate
            if ($r) { return $r }
        }
    }
    return $null
}

function Invoke-McecClickElement {
    param(
        [Parameter(Mandatory)]$WindowTarget,
        [string]$By = 'name',
        [Parameter(Mandatory)][string]$Value,
        $Session = $null
    )
    $args = @{} + $WindowTarget + @{ at = @{ by = $By; value = $Value } }
    Invoke-McecTool 'click' $args -Session $Session | Out-Null
}

function Wait-McecWindow {
    param(
        $WindowTarget,
        [int]$Attempts = 30,
        [int]$DelayMs = 600
    )
    for ($i = 0; $i -lt $Attempts; $i++) {
        Start-Sleep -Milliseconds $DelayMs
        $w = Get-McecWindow (Invoke-McecTool 'query' ($WindowTarget + @{ maxDepth = 1 }))
        if ($w -and $w.handle) { return [long]$w.handle }
    }
    return 0
}

function Wait-McecMcp {
    param([int]$Attempts = 40, [int]$DelayMs = 600)
    for ($i = 0; $i -lt $Attempts; $i++) {
        Start-Sleep -Milliseconds $DelayMs
        try {
            if ((Invoke-McecRpc 'initialize' @{}).result.serverInfo.name -eq 'MCEC') { return $true }
        } catch {}
    }
    return $false
}

Export-ModuleMember -Function Set-McecMcpUrl, Invoke-McecRpc, Invoke-McecTool, Send-McecCommand,
    Get-McecAgentEnvelope, Get-McecTree, Get-McecWindow, Find-McecNode, Invoke-McecClickElement,
    Wait-McecWindow, Wait-McecMcp