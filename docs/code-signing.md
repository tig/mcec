# Code signing: Azure Trusted Signing (runbook)

MCEC signs its Windows installer (`mcec.Setup.exe`) with **Azure Trusted
Signing** (a.k.a. Azure Artifact Signing), authenticating from GitHub Actions via **OIDC
federation**; there is **no client secret** and nothing sensitive to rotate. Signing is wired
into [`.github/workflows/release.yml`](../.github/workflows/release.yml) and gated on the six
`AZURE_*` repo secrets, so the release path still works (unsigned, with a warning) before signing
is configured or on a fork.

## Reuses WinPrint's signing identity

This repo **reuses the Trusted Signing account and certificate profile already validated for
[tig/winprint](https://github.com/tig/winprint)** (publisher identity = **Kindel LLC**). A
Trusted Signing certificate represents the *publisher*, not a single app, so one validated
identity signs any number of apps. The one-time Microsoft **identity validation is NOT repeated**.

All that's created for this repo is a **dedicated app registration (`mcec`)** with its own GitHub
OIDC federated credentials, granted the signer role on the same certificate profile. So winprint's
trust is untouched and independent.

| | Value |
|---|---|
| Subscription | Kindel LLC (`7bee0c7c-…`) |
| Resource group | `WinPrint_Resources` |
| Trusted Signing account | `winprint` (reused) |
| Certificate profile | `WinPrint`; Public Trust, Kindel LLC (reused) |
| App registration (this repo) | `mcec` (created by `SetupAzure.ps1`) |
| OIDC subjects | `repo:tig/mcec:ref:refs/heads/{develop,main}` + flexible `refs/tags/*` |

The single source of truth for these values is [`scripts/Azure.Config.ps1`](../scripts/Azure.Config.ps1).

## One-time setup (you run this)

Prereqs: `pwsh` 7+, `az` (Azure CLI) logged in as an account with rights to create an app
registration + role assignment on the `winprint` Trusted Signing account, and `gh` (to push
secrets). The signing account/profile already exist (from winprint), so there is **no portal step**.

```bash
az login                                         # interactive; run it yourself
pwsh scripts/SetupAzure.ps1 -SetGitHubSecrets    # creates the mcec app reg + OIDC trust + role, pushes the 6 secrets
pwsh scripts/ValidateAzure.ps1                   # read-only verification
```

`SetupAzure.ps1` is **find-or-create** at every step; safe to re-run any time (e.g. after editing
`Branches`). Omit `-SetGitHubSecrets` to print the six secret values without touching the repo. It
creates:

1. App registration `mcec` + its service principal.
2. Federated credentials: exact-match subjects for `develop`/`main`, plus a **flexible**
   credential (`claimsMatchingExpression`) for `refs/tags/*` (Entra `subject` is exact-match, so a
   wildcard tag subject can't work; see the gotchas in the script).
3. Role assignment **“Artifact Signing Certificate Profile Signer”** at the certificate-profile scope.
4. The six repo secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
   `AZURE_SIGNING_ACCOUNT`, `AZURE_SIGNING_PROFILE`, `AZURE_SIGNING_ENDPOINT`.

## Cutting a (signed) release

Once the secrets are set, releasing is one push:

```bash
git tag -a v2.5.0 -m "MCEC v2.5.0"
git push origin v2.5.0
```

`release.yml` then builds the self-contained installer (`Release.ps1`), signs
`mcec.Setup.exe` via Azure Trusted Signing, and publishes the GitHub Release with
auto-generated notes. (You can also run it from the Actions tab via **workflow_dispatch** with a
version, for a dry run.)

For a **local, unsigned** build, `pwsh ./Release.ps1 -Version 2.5.0` still produces the installer in
`src/bin/`; handy for testing, but Windows SmartScreen will warn on an unsigned build.

## How `release.yml` consumes the trust

- `SIGN` (env) is true only when all six secrets are present; the `azure/login` (OIDC) and
  `azure/artifact-signing-action` steps are gated on it.
- The job requests `permissions: id-token: write`; `azure/login@v2` exchanges the GitHub OIDC token
  for the federated credential; **no secret**. Tag pushes match the flexible `refs/tags/*`
  credential; `workflow_dispatch` runs match the per-branch credentials.

## Gotchas (baked into the scripts)

- **Entra federated `subject` is EXACT-match; wildcards don't work.** Tags use a *flexible*
  federated identity credential (`claimsMatchingExpression`) created via the **beta** Microsoft
  Graph endpoint; the `v1.0` endpoint and `az ad app federated-credential create` reject/omit it.
- **PowerShell `$var:` trap.** `"...$GhRepo:ref..."` makes PowerShell read `GhRepo:` as a scope
  qualifier and drop the repo name; always brace: `${GhRepo}`.
- **Role rename.** “Trusted Signing Certificate Profile Signer” → “Artifact Signing Certificate
  Profile Signer”. `ValidateAzure.ps1` accepts both.
- **RBAC propagation.** A freshly created role assignment can take a few minutes before the first
  CI signing call succeeds.
- **First signed run validates the action.** The signing step uses `azure/artifact-signing-action@v2`;
  because OIDC signing can't be exercised outside CI, confirm the first tag-triggered run is green.

## Teardown (if ever needed)

```bash
# Remove this repo's CI trust (leaves winprint and the shared signing account/profile intact):
APPID=$(az ad app list --display-name mcec --query "[0].appId" -o tsv)
az ad app delete --id "$APPID"   # deletes the mcec app + SP + federated creds; role assignment is GC'd
```
