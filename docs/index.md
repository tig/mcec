# MCEC

![MCEC](hero.gif)

**MCEC** — the **Model Context Environment Controller** — is eyes, hands, and a safe front door for AI
agents on Windows, and the same battle-tested TCP/serial remote control for integration systems it has
always been.

It is a small, self-contained native Windows daemon that a computer-use model can **mount, see through,
and drive**: capture a window as a PNG, read its UI Automation tree, find and wait for controls, launch
apps, and actuate keyboard/mouse/window input, over the **Model Context Protocol (MCP)**. The agent
surface is opt-in and off by default; the 3.0 agent features are purely additive over the classic
remote-control command surface (network and serial), which is unchanged.

**Install with winget:** `winget install Kindel.mcec` — see [Install](install.html).

## The chapters

* **[Install](install.html)** — install with winget or the signed installer; what gets installed where,
  running side-by-side copies, and disposable provisioned instances for agents.
* **[Configuration](configuration.html)** — everything you can configure: the Settings dialog (every
  tab), the `mcec.settings` file, the command table, and logging.
* **[Agent Control](agent_control.html)** — the agent surface: the observation,
  targeting, and actuation tools, the structured result envelope, and the MCP / localhost-HTTP transports.
* **[Agent Safety](safety-emergency-stop-and-provisioning.html)** — command-access consent, disposable
  isolated session provisioning, the emergency-stop hotkey, and the on-screen command overlay.
* **[Remote Control](remote_control.html)** — the classic role: listen on TCP/IP or a
  serial port and translate remote commands into keystrokes, text, mouse, window messages, and app
  launches ([Control4](https://www.control4.com/), [iRule](http://www.iruleathome.com/),
  [Crestron](http://www.crestron.com/), and others). The same chapter covers the **User Activity Monitor**,
  which runs the flow in reverse: it reports when someone is actively using the PC, turning the machine into
  an occupancy sensor that can drive lighting and scenes.
* **[Examples](examples.html)** — worked agent-driving recipes (hero GIFs, prompt demos) and how to add more.

See also [AGENTS.md](https://github.com/tig/mcec/blob/main/AGENTS.md) for connect-time agent guidance and
the dogfood recipe (MCEC driving MCEC).

## Version history

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
