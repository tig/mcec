<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Install

## winget (recommended)

MCEC is on winget (3.0.15 and later):

```
winget install Kindel.mcec
```

That downloads and runs the signed installer. Update with `winget upgrade Kindel.mcec`; remove with
`winget uninstall Kindel.mcec`. You can also [download the installer](https://github.com/tig/mcec/releases)
and run it directly.

After installing, launch **MCEC** from the **Start Menu** (setup also adds a Desktop shortcut).

The installer may ask whether to share optional usage telemetry; it is **off unless you opt in**. See
[How MCEC Uses Telemetry](telemetry.html) for what is and is not collected.

## What gets installed, and where

- MCEC installs to **`C:\Program Files\Kindel\MCEC`**. It is a self-contained x64 build, so no separate
  .NET runtime is required.
- Setup adds a **Start Menu** entry and a **Desktop** shortcut, both pointing at `mcec.exe`.
- Because Program Files is read-only, the installed instance keeps its config, command table, and log
  under **`%APPDATA%\Kindel\MCEC`** (`mcec.settings`, `mcec.commands`, `mcec.log`).
- Uninstall from **Add/Remove Programs** (or `winget uninstall Kindel.mcec`).

## Side-by-side copies

MCEC is a self-contained folder, so you can run more than one at a time. Copy the install directory
somewhere writable and run `mcec.exe` from the copy. A copy that is **not** under Program Files reads its
config **co-located** in its own folder (MCEC resolves its config path to the exe's directory), so each
copy gets independent `.settings`, `.commands`, and `.log` files and they never contend over one file.
This directory-per-instance isolation is exactly what provisioning (below) automates.

## Provisioning: disposable instances for agents

The installed copy under Program Files deliberately **refuses to serve agents**: running `mcec.exe --mcp`
or starting the MCP/HTTP endpoint from Program Files is refused, because enabling the agent gates in your
installed config would leave them enabled if a session crashed. Instead, an authorized agent asks MCEC for
a fresh, disposable copy to drive (there is no `--provision` flag; provisioning is an in-product feature):

1. Turn on **Allow agents to provision disposable instances** on the Settings dialog's **Agent** tab
   (`AllowSessionProvisioning`). This is the one opt-in an operator performs.
2. Click **Provision new…**. MCEC creates a throwaway directory under `%LOCALAPPDATA%\MCEC\sessions\<id>`
   and shows a handoff dialog: MCP client setup plus a briefing prompt for your agent. The session copy
   has agent commands enabled **only** inside that directory. When the run is done, delete the instance
   from the Agent tab (or let MCEC reap stale session directories on launch).

The **Agent** tab (see [Configuration](configuration.html)) also lists provisioned instances and lets you
delete any an agent left behind. The full provisioning and emergency-stop model is in
[Agent Safety](safety-emergency-stop-and-provisioning.md).
