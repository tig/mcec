# Self-Hosted Interactive Windows Runner (non–Session-0)

This runner is required for real desktop interaction tests (keyboard/mouse injection, window capture, UIA, launching apps that expect a visible desktop). GitHub-hosted `windows-latest` runners run in Session 0 (no interactive desktop / no visible UI) and cannot be used for these tests.

## Labels and Registration
When adding the self-hosted runner (via the GitHub runner app or `config.cmd`):

```powershell
# example labels (use what your org standardizes on)
.\config.cmd --url https://github.com/tig/mcec --token <TOKEN> --labels self-hosted,Windows,interactive --name "win-interactive-01" --work _work
```

The CI lane uses:

```yaml
runs-on: [self-hosted, Windows, interactive]
```

## Prerequisites on the Runner Machine
- Windows 10/11 Pro or Enterprise (Home has limited autologon options)
- .NET 10 SDK (matches the project)
- NSIS (if you want to build installers locally)
- The user that runs the runner service must be able to auto-logon and have the desktop visible.

## Autologon + Auto-Unlock (for unattended desktop)
To have the runner machine always have an interactive desktop:

1. Enable autologon (stores password in registry - see security below):
   Use Sysinternals `Autologon.exe` or set the registry keys directly:

   ```powershell
   # Requires elevation
   $user = "YOURDOMAIN\RunnerUser"   # or ".\LocalRunner"
   $pass = Read-Host -AsSecureString "Password for autologon"
   # Use Sysinternals Autologon (recommended)
   .\Autologon.exe $user . (ConvertFrom-SecureString $pass -AsPlainText)
   ```

2. Disable screen lock / require password on wake:
   - Settings → Accounts → Sign-in options → "If you've been away, when should Windows require you to sign in again?" → Never
   - Power & sleep → Screen and sleep → set to never when plugged in
   - gpedit.msc (Pro+): Computer Configuration → Windows Settings → Security Settings → Local Policies → Security Options → "Interactive logon: Machine inactivity limit" = 0

3. Disable "lock on lid close" etc. if laptop.

4. (Optional) Use a dedicated local account with minimal rights.

## Running the Interactive CI Lane
The lane is triggered on push/PR to develop/main or manually (workflow_dispatch).

It will:
- Build the solution
- Run the full xUnit suite
- Run a trivial smoke test:
  - Launch MCEC (in temp dir with agent opt-ins)
  - Use MCP stdio + the `capture` tool to grab the main "MCEC" window
  - Assert the captured frame is non-blank (reasonable PNG size + pixel variation)
- Upload artifacts (test results, smoke outputs)

If the runner has no interactive desktop (e.g. accidentally runs on a session-0 machine), the smoke step reports cleanly with `::notice::` and exits 0 without failing the job.

## Security Trade-offs (Autologon)
**Warning**: Autologon stores the password in the registry (HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon) in a reversibly encrypted form that can be read by local admins or malware running as admin.

- Use a low-privilege dedicated account.
- The machine should be physically secured or in a locked lab.
- Consider BIOS/UEFI password + full disk encryption.
- Do **not** use a high-privilege domain admin account for the runner.
- Prefer a local account over domain if possible.
- Review the trade-off: you gain real desktop automation capability at the cost of automatic logon.

See also the general security guidance in the agent docs (least-privilege, opt-ins, audit logs).

## Verifying the Runner
On the machine, you can manually test the smoke:

```powershell
cd <repo>
dotnet build MCEControl.slnx -c Release
# set MCEC_DESKTOP_E2E or just run the smoke logic
# (or simply run the workflow via workflow_dispatch)
```

The lane should report the smoke as skipped with a notice if run on a non-interactive machine.

## Artifact Shape (aligned with #87 bundle ideas)
- test-results-... (trx, coverage)
- smoke-... (if produced)
- Any future per-observation screenshots/GIFs from E2E/agent tests can follow the same upload pattern.

This lane is intentionally independent of the agent "skeleton" and only consumes the built MCEC binary + existing test infrastructure.

## Next
Once stable, it can host the full desktop E2E / agent automation tests (#98) that require real input + visible desktop.
