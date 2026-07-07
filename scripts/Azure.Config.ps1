<#
.SYNOPSIS
  Single source of truth for MCEC's Azure Trusted Signing + GitHub OIDC trust.
.DESCRIPTION
  Dot-sourced/invoked by SetupAzure.ps1 and ValidateAzure.ps1. Edit values HERE only.
  Returns a hashtable when invoked:  $cfg = & "$PSScriptRoot/Azure.Config.ps1"
  None of these values are secret; they are Azure/GitHub identifiers, not credentials.
  The actual trust is the federated credential (no client secret is ever created).

  REUSE NOTE: MCEC reuses WinPrint's already-validated Trusted Signing account
  and certificate profile (publisher identity = Kindel LLC). The one-time Microsoft
  identity validation is therefore NOT repeated. Only a *dedicated app registration*
  ('mcec') with its own GitHub OIDC federated credentials is created for this repo, and
  it is granted the signer role on the same certificate profile. See dev/code-signing.md.
#>
@{
    # --- Subscription / signing identity (REUSED from WinPrint; do not recreate) ---
    SubscriptionId = '7bee0c7c-3217-4628-a783-dd7d687112d3'   # Kindel LLC
    ResourceGroup  = 'WinPrint_Resources'                     # where the signing account lives
    Location       = 'eastus'
    SigningAccount = 'winprint'                               # existing Trusted Signing account
    CertProfile    = 'WinPrint'                               # existing Public Trust cert profile (Kindel LLC)

    # --- Dedicated app registration + OIDC trust for THIS repo (created by SetupAzure.ps1) ---
    AppDisplayName = 'mcec'
    GhOwner        = 'tig'
    GhRepo         = 'mcec'
    Branches       = @('develop', 'main')   # plus refs/tags/* (always added)
    SignerRole     = 'Artifact Signing Certificate Profile Signer'
}
