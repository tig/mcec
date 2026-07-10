# MCEC

![MCEC](hero.gif)

**MCEC** — the **Model Context Environment Controller** — is eyes, hands, and a safe front door for AI
agents on Windows, and the same battle-tested TCP/serial remote control for integration systems it has
always been.

It is a small, self-contained native Windows daemon that gives a **computer use** model (the same
capability Claude, Codex, and similar agents use to see a screen and drive a keyboard and mouse)
something to **mount, see through, and drive**: capture a window as a PNG, read its UI Automation tree,
find and wait for controls, launch apps, and actuate keyboard/mouse/window input, over the **Model Context
Protocol (MCP)**. The agent surface is opt-in and off by default; the 3.0 agent features are purely
additive over the classic remote-control command surface (network and serial), which is unchanged.

**Install with winget:** `winget install Kindel.mcec` — see [Install](install.html).

## The chapters

* **[Install](install.html)**: install with winget or the signed installer; what gets installed where,
  running side-by-side copies, and disposable provisioned instances for agents.
* **[Configuration](configuration.html)** - everything you can configure: the Settings dialog (every
  tab), the `mcec.settings` file, the command table, and logging.
* **[Agent Control](agent_control.html)** - the computer use surface: the observation,
  targeting, and actuation tools, the structured result envelope, and the MCP / localhost-HTTP transports.
* **[Agent Safety](safety-emergency-stop-and-provisioning.html)**: command-access consent, disposable
  isolated session provisioning, the emergency-stop hotkey, and the on-screen command overlay.
* **[Remote Control](remote_control.html)** - the classic role: listen on TCP/IP or a
  serial port and translate remote commands into keystrokes, text, mouse, window messages, and app
  launches ([Control4](https://www.control4.com/), [iRule](http://www.iruleathome.com/),
  [Crestron](http://www.crestron.com/), and others). The same chapter covers the **User Activity Monitor**,
  which runs the flow in reverse: it reports when someone is actively using the PC, turning the machine into
  an occupancy sensor that can drive lighting and scenes.
* **[Examples](examples.html)**: worked agent-driving recipes (hero GIFs, prompt demos) and how to add more.

See also [AGENTS.md](https://github.com/tig/mcec/blob/main/AGENTS.md) for connect-time agent guidance and
the dogfood recipe (MCEC driving MCEC).

## Frequently Asked Questions (FAQ)

**Q: Claude, Copilot, and other agent platforms already support computer use. Why would I use MCEC?**

Those platforms aim for cross-platform reach, so their computer use is built primarily on computer
vision: a screenshot, interpreted by a vision model, on every step. That is slow (a network round trip per
observation) and token-expensive (a full-window PNG on every turn). MCEC is Windows-only by design, so it
can go deeper than pixels: `query` reads the real Windows UI Automation tree (control type, name,
automation ID, bounds, enabled state, value), letting an agent target and verify a specific control instead
of re-parsing an image every step. `capture` (a real screenshot) is still there for when vision is the
right tool. And because MCEC speaks the **Model Context Protocol (MCP)**, it works with any MCP-capable
agent, Claude, Codex, or a custom client, so a workflow or automated system built on it isn't locked to one
AI provider.

**Q: Is it safe?**

It depends on how you use it, but the defaults are safe. The agent surface is off until you opt in;
every command ships individually disabled; every agent action is audit-logged on screen (the overlay) and
in the log; and a global emergency-stop hotkey lets the operator halt a session instantly. See
[Agent Safety](safety-emergency-stop-and-provisioning.html) for the full model.

**Q: What's the history of this thing?**

* **3.0 (2026)**: Rebranded to the **Model Context Environment Controller**. Agent automation over MCP:
  observation (`capture`/`query`/`record`), targeting (`find`/`wait-for`), actuation
  (`invoke`/`launch`/`drag`/`click`), emergency stop, and isolated session provisioning; all opt-in and
  off by default.
* **2.x (2019)**: Major rework: robust client/server, User Activity Monitor (occupancy sensing), Commands
  Window with built-in test mode, per-monitor DPI support, config in `%APPDATA%`.
* **1.x (2004–2017)**: Born as Media Center Edition Controller (MS later dropped the "Edition" from
  "Windows Media Center") for Windows Media Center HTPCs. Grew keyboard/mouse/window-message
  simulation, `chars:` with Unicode escapes, serial support, multi-client TCP, and the `.commands`
  extension file. Moved from SourceForge to CodePlex to GitHub.

See [Releases](https://github.com/tig/mcec/releases) for full release notes.
