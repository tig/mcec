<#
.SYNOPSIS
  Verifies MCEC's Azure Trusted Signing OIDC trust is correctly configured for CI.

.DESCRIPTION
  Read-only. Reads scripts/Azure.Config.ps1, discovers the app registration by display
  name, then asserts: the expected federated-credential subjects exist and a valid signer
  role is assigned at the certificate-profile scope. Throws on the first problem.
  Run after SetupAzure.ps1 (or any time you suspect drift).

.NOTES
  Requires: pwsh 7+, Azure CLI (az) logged in with read access to the app + subscription.
#>
#requires -Version 7
[CmdletBinding()]
param(
    [string]$ConfigPath = "$PSScriptRoot/Azure.Config.ps1"
)

$ErrorActionPreference = 'Stop'
$cfg = & $ConfigPath

az account set --subscription $cfg.SubscriptionId
$TenantId = az account show --query tenantId -o tsv

$AppId = az ad app list --display-name $cfg.AppDisplayName --query "[0].appId" -o tsv
if (-not $AppId) { throw "App registration '$($cfg.AppDisplayName)' not found. Run scripts/SetupAzure.ps1 first." }
$SpObjectId = az ad sp show --id $AppId --query id -o tsv

$ProfileScope = "/subscriptions/$($cfg.SubscriptionId)/resourceGroups/$($cfg.ResourceGroup)" +
    "/providers/Microsoft.CodeSigning/codeSigningAccounts/$($cfg.SigningAccount)" +
    "/certificateProfiles/$($cfg.CertProfile)"

Write-Host "TenantId:     $TenantId"
Write-Host "AppId:        $AppId"
Write-Host "SP ObjectId:  $SpObjectId"
Write-Host "ProfileScope: $ProfileScope"

# 1) Federated credentials. Read via the beta Graph endpoint so the flexible tag
#    credential's claimsMatchingExpression is visible (the v1.0 `az` list omits it).
#    Branches are exact-match subjects; tags are a flexible claimsMatchingExpression
#    (Entra `subject` is exact-match, so a wildcard tag subject can't work — see
#    SetupAzure.ps1 / docs/code-signing.md). Note ${GhRepo}: the bare $GhRepo:ref form
#    makes PowerShell treat 'GhRepo:' as a scope qualifier and silently drops the repo name.
$AppObjectId = az ad app show --id $AppId --query id -o tsv
$fics = (az rest --method get --url "https://graph.microsoft.com/beta/applications/$AppObjectId/federatedIdentityCredentials" | ConvertFrom-Json).value
$fics | Select-Object name, subject, @{ n = 'claimsExpr'; e = { $_.claimsMatchingExpression.value } } | Format-Table -AutoSize

# Branches: exact subject must be present
foreach ($b in $cfg.Branches) {
    $subject = "repo:$($cfg.GhOwner)/$($cfg.GhRepo):ref:refs/heads/$b"
    if (-not ($fics.subject -contains $subject)) {
        throw "Missing federated credential subject: $subject"
    }
}

# Tags: a flexible credential whose expression covers refs/tags/* must be present
$expectedTagPattern = "repo:$($cfg.GhOwner)/$($cfg.GhRepo):ref:refs/tags/*"
$tagCred = $fics | Where-Object { $_.claimsMatchingExpression -and $_.claimsMatchingExpression.value -like "*$expectedTagPattern*" }
if (-not $tagCred) {
    throw "Missing flexible federated credential (claimsMatchingExpression) covering: $expectedTagPattern"
}

# 2) Role assignment at the certificate-profile scope
$assignments = az role assignment list `
    --assignee-object-id $SpObjectId `
    --scope $ProfileScope `
    --include-inherited `
    --query "[].{role:roleDefinitionName,scope:scope,principalType:principalType}" | ConvertFrom-Json

$assignments | Format-Table -AutoSize

# Accept either the current or the legacy role name (Microsoft renamed it).
$validRoles = @(
    'Artifact Signing Certificate Profile Signer',
    'Trusted Signing Certificate Profile Signer'
)
if (-not ($assignments | Where-Object { $validRoles -contains $_.role })) {
    throw "Expected signer role not found at certificate profile scope."
}

# 3) GitHub secret values to set/confirm
$Endpoint = az rest --method get `
    --url "https://management.azure.com$('/subscriptions/' + $cfg.SubscriptionId + '/resourceGroups/' + $cfg.ResourceGroup + '/providers/Microsoft.CodeSigning/codeSigningAccounts/' + $cfg.SigningAccount)?api-version=2024-02-05-preview" `
    --query "properties.accountUri" -o tsv

"AZURE_CLIENT_ID=$AppId"
"AZURE_TENANT_ID=$TenantId"
"AZURE_SUBSCRIPTION_ID=$($cfg.SubscriptionId)"
"AZURE_SIGNING_ACCOUNT=$($cfg.SigningAccount)"
"AZURE_SIGNING_PROFILE=$($cfg.CertProfile)"
"AZURE_SIGNING_ENDPOINT=$Endpoint"

Write-Host "`nValidation complete." -ForegroundColor Green
