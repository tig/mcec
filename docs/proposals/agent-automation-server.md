# Proposal: MCEC, the Model Context Environment Controller

*An agent-friendly Windows automation server.*
*Status: Draft proposal (working-backwards PR/FAQ). Supersedes the discussion in [issue #46](https://github.com/tig/mcec/issues/46).*
*Author: Tig Kindel. Date: 2026-06-28.*

---

## Introduction

MCE Controller (MCEC) is already ~70% of a remote **actuation** server for Windows, and a
genuinely good one: a security-gated, disabled-by-default network listener that turns text
commands into `SendInput`, `SendMessage` (targetable by class/window name), text entry, mouse,
`SetForegroundWindow`, and process launches, over TCP (client *and* server) and serial.

What it cannot do is the other half of an AI agent's loop. An agent runs *observe → decide →
act → observe*. Today MCEC can only **act**. It cannot **see** the screen, **read** the UI tree,
or **target/wait for** a specific control; and it speaks raw TCP text, not a transport an LLM
mounts natively.

This document proposes closing that gap by adding three things (**observation**,
**targeting/synchronization**, and an **MCP transport**) on top of the existing, hardened command
core. It follows the working-backwards format: a short press-release artifact describing the
experience we want to ship, followed by an FAQ. The longest FAQ (by design) is the
prior-art/competition question, because "should we build this at all?" turns entirely on whether
something already does it well.

Part of the proposal is a rebrand. "MCE Controller" (Media Center Edition Controller) names a
product that's been dead for a decade. We keep the four letters and the pronunciation and give them
a meaning native to this world. **MCEC** now stands for the **Model Context Environment Controller**:
the local, native Windows *environment* that a computer-use *model* drives, exposed over the **Model
Context Protocol** (so MCEC reads as a natural sibling of MCP). Same daemon, new job. The rationale
is in FAQ 15.

The artifact below is written as though it were the real announcement on the day we ship.

---

## The Working-Backwards Artifact (Press Release)

### MCEC 3.0: "MCE Controller" becomes the Model Context Environment Controller, giving AI agents eyes, hands, and a safe front door on Windows

**A single, self-contained native Windows daemon an agent can mount, see through, and drive, with
the same disabled-by-default security model MCEC users have trusted for 15 years.**

**SEATTLE, <launch date>.** Kindel today released **MCEC 3.0**, retiring the old "MCE Controller"
(Media Center Edition Controller) name in favor of what the letters now stand for: the **Model
Context Environment Controller**. The free, open-source upgrade turns its battle-tested Windows
control daemon into a tool an AI agent can use directly. MCEC has spent more than a decade reliably translating remote commands into keystrokes,
mouse moves, window messages, and app launches for home-theater and home-automation systems. Version
3.0 keeps all of that and adds the half that AI agents were missing: the ability to **observe** the
screen and UI, **target and wait for** specific windows and controls, and to be **mounted as a
Model Context Protocol (MCP) server** by tools like Claude Code and Claude Desktop.

Until now, pointing an agent at a native Windows app meant hand-rolling a fragile harness:
`PrintWindow` tricks to screenshot composited WinUI/WPF surfaces, UI Automation glue to find a
button, retry loops to wait for a dialog, DPI and foreground juggling so input lands where you
think it does. MCEC 3.0 collapses that into a handful of tool calls.

> "MCEC always had good hands. It could press any key and click anywhere; it just couldn't see what
> it was doing or aim at anything by name," said Tig Kindel, MCEC's author. "I spent an afternoon
> hand-writing `PrintWindow` and UI Automation code just to drive one app for a demo. The actuation
> half (the genuinely hard, security-sensitive part) was already sitting in MCEC. 3.0 adds the
> eyes and a front door agents already know how to open."

**What's new in 3.0:**

- **`capture` (see the screen).** Screenshot a window or region to PNG/base64 using
  `PrintWindow(..., PW_RENDERFULLCONTENT)`, so it correctly captures DirectComposition surfaces
  (WinUI 3 / WPF) that plain screen grabs return black for, even on a locked session.
- **`query` (read the UI).** Dump the UI Automation tree for a process or window: control type,
  name, automation id, value, bounding rect, enabled/focusable state. The structured equivalent of
  "look at the screen," so an agent can target controls instead of pixel-hunting.
- **`find` / `wait-for` (stop guessing).** Locate or wait (with a timeout) for a window or UIA
  element by name, automation id, or class, replacing the retry loops every GUI script grows.
- **`invoke` (act precisely).** Drive a UIA element directly (Invoke / Toggle / Value / SetFocus),
  which is far more reliable than coordinate clicks and works on locked sessions.
- **MCP server (stdio + HTTP/JSON).** Every command, old and new, is exposed as an MCP tool with
  a schema. `capture` and `query` return content the model can reason over. Mount MCEC in any MCP
  client and the desktop becomes addressable.

Crucially, none of this weakens what MCEC is. The new observation commands ship **disabled by
default**, bind to **localhost only** by default, require their **own explicit opt-in** separate
from actuation, and **log loudly**. The existing TCP/serial transports and the 250+ HTPC commands
are untouched. 3.0 is purely additive.

MCEC 3.0 is **free and open source (MIT)**, a single native .NET 8 Windows executable
with no Python or Node runtime to install. Download it from
[github.com/tig/mcec/releases](https://github.com/tig/mcec/releases); enable the agent commands,
point your MCP client at it, and your agent can see, target, and act on a Windows desktop in
minutes.

---

## FAQ

### 1. Who is the customer, and why do they care?

Two customers, one product:

- **The agent builder / power user** who needs an LLM to drive a *native* Windows app: an
  internal line-of-business tool, a legacy WinForms/WPF/WinUI app, an installer, a desktop utility
  with no API. Today they either screen-scrape with brittle pixel-coordinate tools or hand-roll
  UIA + `PrintWindow` glue. They care because MCEC 3.0 turns hours of harness-building into a few
  tool calls, on a daemon with a real security model.
- **The existing MCEC / HTPC user**, who loses nothing. Everything they rely on stays, disabled-
  by-default, on the same transports. The agent features are opt-in and out of their way.

The deciding insight from issue #46: the painful, finicky, security-sensitive *actuation +
transport + gating* layer is the part that's hard to build and already exists in MCEC. Observation
and an MCP façade are additive and low-risk.

### 2. What exactly is new versus MCEC today?

| Capability | MCEC today | MCEC 3.0 |
|---|---|---|
| Keystrokes / text / mouse | ✅ `SendInput`, `Chars`, `Mouse` | ✅ unchanged |
| Window messages, targeted by class/window name | ✅ `SendMessage` | ✅ unchanged |
| Launch apps, focus windows, shutdown | ✅ `StartProcess`, `SetForegroundWindow` | ✅ unchanged |
| **See the screen** (screenshot a window/region) | ❌ | ✅ `capture` (PrintWindow / PW_RENDERFULLCONTENT) |
| **Read the UI tree** (UIA dump) | ❌ | ✅ `query` |
| **Find / wait for** a window or control | ⚠️ partial (`SendMessage` by name) | ✅ `find` / `wait-for` with timeout |
| **Invoke a control** by UIA pattern | ❌ | ✅ `invoke` |
| Transports | TCP server/client, serial | + **MCP (stdio + HTTP/JSON)** |
| Replies | opaque strings | + structured JSON (success/error + data) |

Everything in the "today" column keeps working exactly as it does now.

### 3. What is the customer experience; how does an agent actually use it?

An agent's loop becomes a sequence of MCP tool calls instead of a hand-built harness:

1. `find` / `wait-for` the target window (e.g. an app's main window or a modal dialog) with a
   timeout, instead of a sleep-and-pray retry loop.
2. `query` the UIA tree to see the controls: names, automation ids, bounds, state.
3. `invoke` the right control (button, menu item, toggle), or fall back to `SendInput` / `Mouse`
   when a control has no UIA pattern.
4. `capture` the window to confirm the result visually and feed the model a screenshot it can
   reason over.

The canonical motivating example from issue #46, driving the native **Open** dialog by hand
(button → filename edit → Open) with `PrintWindow` capture and UIA `Invoke`, becomes four tool
calls.

### 4. What is the prior art / competition, and why build this instead of using something that exists? *(the long one)*

This is the question the whole proposal turns on. We surveyed the mid-2026 landscape; the short
version is that the space is real and crowded, **but nothing occupies MCEC's exact position: a
single self-contained native Windows daemon that combines observation + targeting + actuation +
MCP behind one hardened, disabled-by-default security model and multiple transports.** The
landscape splits into clear camps, and each leaves a gap MCEC's existing assets already fill.

**Camp A: Vendor "computer use" models (screenshot + coordinate).**
*Anthropic Claude computer use* (beta; tool `computer_20251124` on Opus 4.8/4.7/4.6, Sonnet 4.6) and
*OpenAI CUA / Operator* are pure screenshot-in / pixel-action-out and **OS-agnostic by
construction**. Neither reads the Windows UIA tree, neither targets controls by name, and **neither
ships a supported native-Windows environment**: Anthropic's reference container is Linux-only;
OpenAI's standalone Operator was sunset Aug 2025 and the `computer-use-preview` model is deprecated
with an EOL of **2026-07-23**. They are the *model*, not the Windows *driver*. MCEC is exactly the
local, native, control-aware environment they tell you to "bring your own" of.
Sources: [Anthropic computer-use docs](https://platform.claude.com/docs/en/agents-and-tools/tool-use/computer-use-tool),
[OpenAI CUA](https://openai.com/index/computer-using-agent/),
[OpenAI deprecations](https://developers.openai.com/api/docs/deprecations).

**Camp B: Microsoft's own Windows-agent platform.**
The strategically important one. *Windows MCP / On-Device Agent Registry (ODR)* + *App Actions*
(announced Build 2025, reinforced Build 2026) make Windows itself an agent host: apps *declaratively*
expose actions as MCP tools, discovered via an OS registry, brokered through a trusted proxy with
per-tool consent. It is elegant and it is the future, but it is **preview / "prereleased," not GA
as of mid-2026**; it requires **apps to cooperate** (it deliberately avoids screen-scraping); and it
governs the *new* world of MCP-aware apps. It does **not** help you drive the millions of existing
WinForms/WPF/Win32 apps that will never declare App Actions. *Power Automate Desktop* is mature RPA
with UIA selectors, but proprietary, license-gated, and its agentic "computer use" lives in Copilot
Studio (vision-based, GA ~May 2026), not as an embeddable local daemon. MCEC targets the long tail
of *uncooperative* apps that Microsoft's declarative path leaves behind, with no platform-preview
dependency.
Sources: [Windows ODR/MCP overview](https://learn.microsoft.com/en-us/windows/ai/mcp/overview),
[Build 2025 dev blog](https://blogs.windows.com/windowsdeveloper/2025/05/19/advancing-windows-for-ai-development-new-platform-capabilities-and-tools-introduced-at-build-2025/),
[Copilot Studio computer use](https://learn.microsoft.com/en-us/microsoft-copilot-studio/computer-use).

**Camp C: Microsoft Research agents & perception (UFO, OmniParser, Windows Agent Arena).**
*UFO / UFO³* (MIT, ~9k★) is the most architecturally similar to where MCEC is heading (hybrid
UIA + vision), and UFO³ (Nov 2025) adds native MCP. But it is a **heavyweight multi-agent Python
research framework** (a whole agent *system*, with its own LLM orchestration), not a small embeddable
driver, and has no hardened "disabled-by-default, opt-in, locked-down" production security posture.
*OmniParser* (~25k★) is perception only; it turns a screenshot into labeled boxes, a component, not
a server, and its YOLO weights are AGPL. *Windows Agent Arena* is a benchmark, **stale since Nov
2024**. These validate the *approach* (UIA + vision is right) without competing for MCEC's *product
slot*.
Sources: [microsoft/UFO](https://github.com/microsoft/UFO),
[UFO² paper](https://arxiv.org/abs/2504.14603),
[microsoft/OmniParser](https://github.com/microsoft/OmniParser),
[WindowsAgentArena](https://github.com/microsoft/WindowsAgentArena).

**Camp D: The classic UIA automation libraries (the closest functional prior art).**
*FlaUI* (.NET, MIT, ~3k★, actively maintained, last commit Jun 2026) and *pywinauto* (Python, BSD,
~6k★, alive but slow-cadence) already solve observation + targeting: first-class screenshot, UIA tree
dump, and find-and-wait. *WinAppDriver* (Microsoft) is the WebDriver-protocol server that did this,
but it has been **effectively abandoned since 2020** (last RC 2021, tied to EOL .NET 5); Appium's own
docs now warn users off it. *appium-windows-driver* is an actively-maintained wrapper sitting on that
dead engine. The decisive fact: **none of these libraries ships a native MCP/agent transport, and
none is a security-gated network daemon.** They are in-process libraries you embed. MCEC's plan is to
*use FlaUI (or equivalent UIA)* internally for `query`/`find`/`invoke`; they are the right
implementation primitive, not a competitor to the product.
Sources: [FlaUI/FlaUI](https://github.com/FlaUI/FlaUI),
[pywinauto/pywinauto](https://github.com/pywinauto/pywinauto),
[microsoft/WinAppDriver](https://github.com/microsoft/WinAppDriver),
[appium-windows-driver](https://github.com/appium/appium-windows-driver).

**Camp E: Community Windows-MCP servers (the most direct competitors).**
This is where MCEC overlaps most, and where the gap is clearest.

| Project | Lang | Stars (≈) | Approach | Maintenance (2026) | Hardened security model? | Multi-transport beyond MCP? |
|---|---|---|---|---|---|---|
| **CursorTouch/Windows-MCP** | Python | ~6.3k | UIA tree + screenshot/coords | **Active** (v0.8.x, Jun 2026) | No (opt-in tooling, not gated) | No (MCP only) |
| shuyu-labs/Windows-MCP.Net | C#/.NET | ~225 | UIA + OCR + coords | Slowing (last Nov 2025) | No | No |
| claude-did-this/MCPControl | TS | ~327 | Coordinate-only + screenshot | Stale (Dependabot only) | No; README warns "dangerous" | No |
| dddabtc/winremote-mcp | Python | ~150 | Vision + Win32 annotate | Active | No | No |
| civyk-official/civyk-winwright | PowerShell | ~13 | UIA3-first, snapshot/inspect | Active, tiny | Permission guards (early) | No |
| shanselman/FlaUI-MCP | C# | ~56 | UIA via FlaUI | One-shot demo | No | No |
| trycua/cua | Python | ~19k | Vision/coordinate platform (cross-OS VMs) | Very active | Sandbox VMs | N/A (framework) |

The category leader, **CursorTouch/Windows-MCP**, is genuinely good and the honest benchmark: UIA-
first, active, MIT, ~6.3k stars, "Playwright for Windows." But it, and every one of these, is a
**fresh Python/Node/PowerShell project that bolts a control surface onto Windows with no inherited
security posture, no non-MCP transports, and (mostly) no track record on locked sessions or
composited-surface capture.** None is a single self-contained native binary; none carries a
disabled-by-default, opt-in, localhost-bound, loudly-logged gating model that has survived 15 years
as a *network-exposed* daemon.
Sources: [CursorTouch/Windows-MCP](https://github.com/CursorTouch/Windows-MCP),
[shuyu-labs/Windows-MCP.Net](https://github.com/shuyu-labs/Windows-MCP.Net),
[claude-did-this/MCPControl](https://github.com/claude-did-this/MCPControl),
[shanselman/FlaUI-MCP](https://github.com/shanselman/FlaUI-MCP),
[trycua/cua](https://github.com/trycua/cua).

**So what is MCEC's actual differentiator?** Three things none of the above combine:

1. **A hardened, disabled-by-default security model that already survived being network-exposed.**
   This is the expensive, easy-to-get-wrong part. Every competitor is retrofitting it (or hasn't);
   MCEC has shipped it for years.
2. **One self-contained native daemon (no Python/Node stack)** that already speaks **TCP (client
   and server) and serial**, to which MCP/HTTP is simply one more transport over the *same* command
   core. Nobody else is multi-transport.
3. **Actuation that is battle-tested**, plus the locked-session / composited-surface know-how
   (`PrintWindow PW_RENDERFULLCONTENT`, UIA `Invoke`) that the demo-grade servers haven't proven.

**Honest conclusion:** if the goal is "the best pure MCP server today," CursorTouch already exists
and we should learn from it rather than clone it. The bet worth making is the *combination*
(observation + targeting + actuation + MCP behind MCEC's existing gate and transports, as one native
binary) that **no one currently ships**. That is a small, additive, testable experiment on top of
assets we already own.

### 5. Isn't a daemon that can screenshot and enumerate the UI just a RAT? How is this safe?

The moment MCEC can `capture` and `query`, it has an exfiltration-shaped capability, and we treat it
that way. The safeguards:

- The new observation/targeting commands are **disabled by default**, with their **own explicit
  opt-in** separate from the existing actuation enable. Enabling "press keys" should not silently
  enable "screenshot my screen."
- **Localhost-only binding by default** for the MCP/HTTP surface; exposing it on the network is a
  deliberate, documented choice.
- **Loud, structured logging** of every observation call: what was captured/queried, when, by whom.
- Builds on MCEC's existing gating: built-in commands off by default, explicit enable, and the
  `DisableInternalCommands` registry override.

This is *sharper* security than the new-project competitors, most of which enable everything by
default. It is the strongest reason to build this on MCEC rather than from scratch.

### 6. Doesn't this dilute MCEC's identity? "HTPC remote" and "agent server" are different products.

Real risk, explicitly weighed. Two mitigations:

- **The change is purely additive.** No existing command, transport, or default changes. The HTPC
  user who never enables agent features sees the same MCEC.
- **If identity strain shows up**, the clean fallback (raised in issue #46) is to ship the agent
  surface as a **separate MCP front-end that depends on MCEC's command core**, rather than reshaping
  the daemon. We will build it in-tree first (lowest friction, shared security model) and split only
  if the HTPC and agent audiences genuinely diverge.

### 7. Should MCEC 3.0 be a TUI or a GUI or both? (cf. [tig/winprint](https://github.com/tig/winprint))

The precedent is winprint, which factors a common print engine behind *three* front-ends: a
cross-platform **TUI** (`wp`), a headless **CLI** (`wp print`), and a **GUI** (`wp gui`). The lesson
is not "pick one"; it is "keep the engine UI-agnostic and treat each front-end as a thin shell." That
maps cleanly onto MCEC, whose command core and services are already separated from `MainWindow`.

For 3.0, the priority order falls out of who the customer is:

- **Headless first (the real requirement).** The agent customer wants no window at all. The daemon
  must run windowless so an MCP client can launch it over stdio, or so it can run as a background
  service. This is the primary mode and the one thing 3.0 genuinely *must* add.
- **Keep the GUI (for humans).** The existing WinForms status window, system-tray icon, log view, and
  test pane are where a human flips the disabled-by-default security toggles and *watches the loud
  logging*. That affordance matters more, not less, once the daemon can see the screen. Retain it for
  interactive use and security configuration.
- **TUI is a later, optional bonus.** A winprint-style `wp`/text UI is attractive for remote and
  SSH administration (watch the log, flip toggles without the WinForms window). It is P2: nice, not
  required for the agent use case. Note a constraint winprint doesn't share: UIA and `PrintWindow`
  pin MCEC to Windows, so a TUI here is a Windows-console UI, not the cross-platform reach winprint's
  TUI enjoys.

**Recommendation: both, layered.** Engine stays UI-agnostic (it largely already is). Ship 3.0 with
(1) a true headless mode as the agent/service default, (2) the existing GUI retained for interactive
use and security config, and (3) a TUI as a follow-on front-end on the winprint pattern only if
remote-administration demand shows up. The MCP server is a transport, not a UI; it works in all three
modes.

### 8. What's the technology, and how big is the effort?

No rewrite; it slots into the existing .NET 8 architecture as new `Command` types plus one new
transport/host:

- `capture`: `PrintWindow` + `PW_RENDERFULLCONTENT` (the Win32 P/Invoke layer already exists in
  `src/Win32`).
- `query` / `find` / `wait-for` / `invoke`: UI Automation via **FlaUI** (MIT) or the raw UIA COM
  interop, the maintained, correct primitive (see FAQ 4, Camp D).
- MCP host: the **ModelContextProtocol C# SDK**, or a minimal `HttpListener` JSON façade, mounted
  over the existing `CommandInvoker`. Commands become tools with schemas; replies become structured
  JSON.

Estimated effort, consistent with issue #46: **a weekend to a fortnight** for the P0 trio
(`capture` + `query` + MCP façade). It is additive and low-risk to the HTPC path.

### 9. Why MCP, and not just an HTTP/JSON endpoint?

HTTP/JSON is the floor and we'll provide it. MCP is the leverage: it is the protocol agents *already
mount natively*. With an MCP server, commands become tools-with-schemas that Claude Code / Claude
Desktop / any MCP client can discover and call without bespoke glue; and `capture`/`query` return
content models reason over directly. It is the single change that turns MCEC from "a thing you
script" into "a thing an agent uses."

### 10. Why MCP, and not just a rich CLI?

A CLI puts the integration burden on a *human*; MCP puts it on the *model*. The differences that
matter for an agent customer:

- **Discovery.** An MCP client introspects the tool list and JSON schemas at runtime, so the model
  learns what `query`/`capture`/`invoke` take and return on its own. A CLI requires a human (or a
  hand-coded wrapper per agent) to read `--help` and translate intent into flags.
- **Typed, structured I/O.** MCP exchanges typed arguments and structured results, including rich
  content like images for `capture`. A CLI emits text you must parse, and returning a screenshot over
  stdout is awkward at best.
- **Session and transport.** MCP defines a session over stdio/HTTP that a client mounts and keeps
  open; the observe→act→observe loop maps onto tool calls. A CLI is spawn-per-invocation with no
  session and no clean place for the agent loop to live.
- **Native surface.** Claude Code, Claude Desktop, and other MCP clients mount MCP servers directly.
  A CLI would need each of them to wrap it.

That said, a CLI isn't worthless, and it's cheap once the command core is UI-agnostic (see FAQ 7).
MCEC already effectively exposes "a CLI over a socket" via its raw TCP text protocol; MCP is the
upgrade of that surface for *agents*, while a thin local CLI is the upgrade of it for *humans*, CI,
and shell scripts. So: **MCP first because the customer is an agent; a CLI is an additive bonus for
people, not the primary interface.**

### 11. What are the priorities / what ships first?

Following issue #46:

- **P0 (unlocks agent use at all):** `capture` (PrintWindow/PW_RENDERFULLCONTENT) + `query` (UIA
  tree dump) + the MCP/HTTP-JSON façade over the existing command set. With just these, an agent can
  see, target, and act.
- **P1 (reliability):** `find` / `wait-for` with timeouts; direct UIA `invoke`; structured JSON
  replies with success/error + data instead of opaque strings.
- **P2 (nice-to-have):** per-monitor DPI normalization; element-relative mouse; a "record" mode that
  emits a command script from real interaction; window enumeration; a winprint-style TUI (FAQ 7).

### 12. How will we know if it worked? (Success metrics)

The proposal is a falsifiable hypothesis: *does modernizing MCEC make agents more effective on
Windows?* We'll measure it by dogfooding:

- **The motivating task reproduced as tool calls.** The hand-rolled WinUI3 driving session from
  issue #46 (zoom/pan/reset/page/open-file, native Open dialog) done end-to-end through MCEC 3.0
  with no bespoke `PrintWindow`/UIA code. Success = it works in a handful of `find`/`query`/`invoke`/
  `capture` calls.
- **Locked-session capture** of a WinUI 3 / WPF window returns real pixels (not black) via
  `PW_RENDERFULLCONTENT`.
- **Zero regressions** in the HTPC command path with agent features disabled.
- Qualitative: an MCP client (Claude Code / Desktop) mounts MCEC and completes a multi-step GUI task
  the model chose the steps for.

### 13. What are we explicitly NOT doing?

- Not building an *agent* or LLM orchestration layer (that's UFO's job, and the client's). MCEC is
  the eyes-and-hands *tool*, not the brain.
- Not shipping a vision/OCR model in-box (OmniParser-style). `capture` returns pixels; the model
  reasons.
- Not changing or deprecating any HTPC command, transport, or default.
- Not chasing cross-platform. UIA, `PrintWindow`, and WinForms keep this Windows-only by design.

### 14. Pricing, licensing, availability, rollout?

Free and open source under the existing **MIT** license; a single native **.NET 8** Windows
executable distributed through [GitHub Releases](https://github.com/tig/mcec/releases), no Python/
Node runtime. Rolls out as a normal MCEC version bump (3.0), with the agent commands shipped
**disabled by default** so existing installs are unaffected until a user opts in.

### 15. Why rename it, and what does MCEC stand for now?

Because the old name points at a tombstone. "MCEC" was **Media Center Edition Controller**, named
for Windows Media Center, which Microsoft shipped its last version of in 2009 and removed from
Windows in 2015. The letters still fit a far more interesting product, so we keep them (and the
"em-see-ee-see" pronunciation, the GitHub repo, and 15 years of brand equity) and re-point their
meaning:

**MCEC = Model Context Environment Controller.**

- **M**odel **C**ontext: deliberately echoes **MCP** (the Model Context Protocol), the transport
  3.0 adds. MCEC becomes the thing you mount over MCP; the names advertise that they belong together.
- **E**nvironment: the precise word the computer-use vendors use. Anthropic and OpenAI both tell you
  to "bring your own **environment**" for the model to act in. MCEC *is* that environment: a local,
  native Windows desktop a model can see and drive. The name states the job.
- **C**ontroller: unchanged from "MCE *Controller*," so there is real continuity. Visually the name
  is identical; only its meaning moved.

One-liner for the website and the press release: *"MCEC, the Model Context Environment Controller:
eyes, hands, and a safe front door for agents on Windows."*

Alternatives considered (kept on the back-pocket list): **Model Context *Embodiment* Controller**
(more evocative; "embodiment" = giving an LLM a body of eyes and hands), **Model Context Execution
Core** (more infrastructural), and **Multimodal Control & Execution Conduit** (drops the MCP pun in
favor of "multimodal": screen + UI tree + input). We chose *Environment* for the continuity with
"Controller" and the direct tie to the "bring your own environment" framing in FAQ 4, Camp A.

---

## Appendix A: Prior-art capability matrix (summary)

| Offering | Camp | Native Win daemon | Observation (shots / UIA) | Targeting | MCP | Multi-transport | Hardened opt-in security | Status 2026 |
|---|---|---|---|---|---|---|---|---|
| Claude / OpenAI computer use | A | No | Shots only | Coordinate | No (native API tool) | No | Sandbox-level | Beta / model EOL Jul-2026 |
| Windows MCP/ODR + App Actions | B | OS feature | Bypassed (declarative) | Declarative intent | **Yes (primary)** | n/a | OS proxy/consent | **Preview, not GA** |
| Power Automate Desktop | B | No (RPA app) | UIA / CUA shots | UIA selectors | Indirect (Copilot Studio) | No | Enterprise controls | Active, proprietary |
| UFO / UFO³ | C | No (agent fw) | UIA + vision | UIA + grounding | Yes (UFO³) | No | No | Active, MIT |
| OmniParser | C | No (library) | Shots → boxes | Yes (boxes) | No | No | No | Active; AGPL weights |
| FlaUI / pywinauto | D | No (library) | **Shots + UIA tree** | **Yes** | **No** | No | No | Active / slow |
| WinAppDriver | D | Server | Shots / partial tree | WebDriver locators | No | No | No | **Abandoned** |
| CursorTouch/Windows-MCP | E | No (Python) | UIA + shots | UIA + coords | Yes | No | No | Active, ~6.3k★ |
| **MCEC 3.0 — Model Context Environment Controller (proposed)** | (new) | **Yes (.NET 8)** | **Shots (PrintWindow) + UIA** | **find/wait/invoke** | **Yes** | **TCP + serial + MCP/HTTP** | **Yes (15-yr model)** | Proposed |

## Appendix B: Sources

Prior-art findings compiled 2026-06-28 from primary sources (vendor docs/blogs, GitHub REST API for
stars/licenses/last-commit dates). Key references are linked inline in FAQ 4. GitHub star counts and
release dates are point-in-time and may have drifted. Self-reported user/adoption figures (e.g.
Windows-MCP "2M+ users") were not independently verified.
