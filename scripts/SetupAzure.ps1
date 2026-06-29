<#
.SYNOPSIS
  One-shot, idempotent setup of MCE Controller's Azure Trusted Signing OIDC trust for CI.

.DESCRIPTION
  Creates (or reuses, if already present) everything release.yml needs to sign Windows
  builds with Azure Trusted Signing using GitHub OIDC — NO client secret:

    1. Entra ID app registration  (Azure.Config.ps1 -> AppDisplayName)
    2. Its service principal
    3. Federated credentials      (refs/tags/* + each configured branch)
    4. Role assignment            (SignerRole, at the certificate-profile scope)
    5. (optional) the six GitHub Actions repo secrets

  Re-running is safe: each step is find-or-create. Run it again any time the config
  changes (e.g. you add a branch) and it will converge.

  PREREQUISITES THAT THIS SCRIPT DOES NOT CREATE (manual, one-time — see docs/code-signing.md):
    * The Trusted Signing *account* and a *PublicTrust certificate profile*. Public-trust
      profiles require a Microsoft identity-validation request that is completed in the
      Azure Portal and cannot be fully scripted. Create those first; this script then
      wires CI up to them. Pass -CreateResourceGroup to create just the resource group.

.EXAMPLE
  pwsh scripts/SetupAzure.ps1
  pwsh scripts/SetupAzure.ps1 -SetGitHubSecrets        # also push the 6 repo secrets via gh

.NOTES
  Requires: pwsh 7+, Azure CLI (az) logged in (`az login`) with rights to create app
  registrations and role assignments, and — for -SetGitHubSecrets — GitHub CLI (gh) auth'd.
#>
#requires -Version 7
[CmdletBinding()]
param(
    [string]$ConfigPath = "$PSScriptRoot/Azure.Config.ps1",
    [switch]$CreateResourceGroup,
    [switch]$SetGitHubSecrets
)

$ErrorActionPreference = 'Stop'
$cfg = & $ConfigPath

function Require-Cli([string]$name, [string]$hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required CLI '$name' not found. $hint"
    }
}
Require-Cli az  "Install: brew install azure-cli  (then: az login)"
if ($SetGitHubSecrets) { Require-Cli gh "Install: brew install gh  (then: gh auth login)" }

# Fail early with a clear message if not logged in.
$null = az account show 2>$null
if ($LASTEXITCODE -ne 0) { throw "Not logged in to Azure. Run: az login" }

Write-Host "==> Selecting subscription $($cfg.SubscriptionId)" -ForegroundColor Cyan
az account set --subscription $cfg.SubscriptionId
$TenantId = az account show --query tenantId -o tsv

# ---------------------------------------------------------------------------
# 0. (optional) resource group — the signing account/profile must be made by hand.
# ---------------------------------------------------------------------------
if ($CreateResourceGroup) {
    Write-Host "==> Ensuring resource group $($cfg.ResourceGroup)" -ForegroundColor Cyan
    az group create --name $cfg.ResourceGroup --location $cfg.Location --only-show-errors | Out-Null
}

$ProfileScope = "/subscriptions/$($cfg.SubscriptionId)/resourceGroups/$($cfg.ResourceGroup)" +
    "/providers/Microsoft.CodeSigning/codeSigningAccounts/$($cfg.SigningAccount)" +
    "/certificateProfiles/$($cfg.CertProfile)"

# Verify the manually-created signing account/profile actually exist before wiring CI to them.
$acct = az resource show --ids "/subscriptions/$($cfg.SubscriptionId)/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.CodeSigning/codeSigningAccounts/$($cfg.SigningAccount)" --query "name" -o tsv 2>$null
if (-not $acct) {
    throw "Trusted Signing account '$($cfg.SigningAccount)' not found in RG '$($cfg.ResourceGroup)'. " +
          "Create the account + a PublicTrust certificate profile in the Azure Portal first (see docs/code-signing.md)."
}

# ---------------------------------------------------------------------------
# 1. App registration (find-or-create by display name)
# ---------------------------------------------------------------------------
Write-Host "==> App registration '$($cfg.AppDisplayName)'" -ForegroundColor Cyan
$AppId = az ad app list --display-name $cfg.AppDisplayName --query "[0].appId" -o tsv
if (-not $AppId) {
    $AppId = az ad app create --display-name $cfg.AppDisplayName --query appId -o tsv
    Write-Host "    created appId=$AppId"
} else {
    Write-Host "    exists  appId=$AppId"
}

# ---------------------------------------------------------------------------
# 2. Service principal (find-or-create)
# ---------------------------------------------------------------------------
Write-Host "==> Service principal" -ForegroundColor Cyan
$SpObjectId = az ad sp show --id $AppId --query id -o tsv 2>$null
if (-not $SpObjectId) {
    $SpObjectId = az ad sp create --id $AppId --query id -o tsv
    Write-Host "    created spObjectId=$SpObjectId"
} else {
    Write-Host "    exists  spObjectId=$SpObjectId"
}

# ---------------------------------------------------------------------------
# 3. Federated credentials
#
#    Branches use exact-match `subject` credentials. Tags use a *flexible* FIC
#    (`claimsMatchingExpression`) instead, because Entra federated-credential
#    `subject` is an EXACT string match — a wildcard subject like
#    "repo:o/r:ref:refs/tags/*" never matches a real tag token and OIDC login
#    fails with AADSTS700213. The flexible form (beta Graph endpoint) supports
#    the `matches` operator and covers every `v*` tag with one credential.
# ---------------------------------------------------------------------------
Write-Host "==> Federated credentials" -ForegroundColor Cyan
$AppObjectId = az ad app show --id $AppId --query id -o tsv
$ficUrl = "https://graph.microsoft.com/beta/applications/$AppObjectId/federatedIdentityCredentials"
$existing = (az rest --method get --url $ficUrl | ConvertFrom-Json).value

# 3a. Branch credentials (exact subject)
foreach ($b in $cfg.Branches) {
    $name = "gh-$b"
    $subject = "repo:$($cfg.GhOwner)/$($cfg.GhRepo):ref:refs/heads/$b"
    if (@($existing.subject) -contains $subject) {
        Write-Host "    exists  $name -> $subject"
        continue
    }
    $body = @{
        name      = $name
        issuer    = 'https://token.actions.githubusercontent.com'
        subject   = $subject
        audiences = @('api://AzureADTokenExchange')
    } | ConvertTo-Json -Compress
    $tmp = New-TemporaryFile
    Set-Content -Path $tmp -Value $body -Encoding utf8
    az ad app federated-credential create --id $AppId --parameters "@$tmp" --only-show-errors | Out-Null
    Remove-Item $tmp
    Write-Host "    created $name -> $subject"
}

# 3b. Tag credential (flexible claimsMatchingExpression)
$tagName = 'gh-tags'
$tagExpr = "claims['sub'] matches 'repo:$($cfg.GhOwner)/$($cfg.GhRepo):ref:refs/tags/*'"
$tagCred = $existing | Where-Object { $_.name -eq $tagName }
if ($tagCred -and $tagCred.subject) {
    # Legacy broken form (wildcard subject) from before the flexible-FIC fix — replace it.
    az ad app federated-credential delete --id $AppId --federated-credential-id $tagCred.id --only-show-errors | Out-Null
    Write-Host "    removed legacy subject-based $tagName"
    $tagCred = $null
}
if (-not $tagCred) {
    $body = @{
        name                     = $tagName
        issuer                   = 'https://token.actions.githubusercontent.com'
        audiences                = @('api://AzureADTokenExchange')
        claimsMatchingExpression = @{ value = $tagExpr; languageVersion = 1 }
    } | ConvertTo-Json -Compress -Depth 5
    $tmp = New-TemporaryFile
    Set-Content -Path $tmp -Value $body -Encoding utf8
    az rest --method post --url $ficUrl --headers "Content-Type=application/json" --body "@$tmp" | Out-Null
    Remove-Item $tmp
    Write-Host "    created (flexible) $tagName -> $tagExpr"
} else {
    Write-Host "    exists  (flexible) $tagName"
}

# ---------------------------------------------------------------------------
# 4. Role assignment at the certificate-profile scope
# ---------------------------------------------------------------------------
Write-Host "==> Role assignment '$($cfg.SignerRole)'" -ForegroundColor Cyan
$have = az role assignment list --assignee-object-id $SpObjectId --scope $ProfileScope --include-inherited `
    --query "[?roleDefinitionName=='$($cfg.SignerRole)'] | length(@)" -o tsv
if ($have -eq '0' -or -not $have) {
    az role assignment create `
        --assignee-object-id $SpObjectId `
        --assignee-principal-type ServicePrincipal `
        --role $cfg.SignerRole `
        --scope $ProfileScope --only-show-errors | Out-Null
    Write-Host "    created (propagation can take a few minutes)"
} else {
    Write-Host "    exists"
}

# ---------------------------------------------------------------------------
# 5. Discover the signing endpoint and emit the GitHub secret values
# ---------------------------------------------------------------------------
$Endpoint = az rest --method get `
    --url "https://management.azure.com$('/subscriptions/' + $cfg.SubscriptionId + '/resourceGroups/' + $cfg.ResourceGroup + '/providers/Microsoft.CodeSigning/codeSigningAccounts/' + $cfg.SigningAccount)?api-version=2024-02-05-preview" `
    --query "properties.accountUri" -o tsv

$secrets = [ordered]@{
    AZURE_CLIENT_ID       = $AppId
    AZURE_TENANT_ID       = $TenantId
    AZURE_SUBSCRIPTION_ID = $cfg.SubscriptionId
    AZURE_SIGNING_ACCOUNT = $cfg.SigningAccount
    AZURE_SIGNING_PROFILE = $cfg.CertProfile
    AZURE_SIGNING_ENDPOINT = $Endpoint
}

Write-Host "`n==> GitHub Actions secrets (release.yml):" -ForegroundColor Green
foreach ($k in $secrets.Keys) { "{0}={1}" -f $k, $secrets[$k] }

if ($SetGitHubSecrets) {
    Write-Host "`n==> Pushing secrets to $($cfg.GhOwner)/$($cfg.GhRepo)" -ForegroundColor Cyan
    foreach ($k in $secrets.Keys) {
        gh secret set $k --repo "$($cfg.GhOwner)/$($cfg.GhRepo)" --body $secrets[$k]
        Write-Host "    set $k"
    }
}

Write-Host "`nSetup complete. Verify with: pwsh scripts/ValidateAzure.ps1" -ForegroundColor Green
