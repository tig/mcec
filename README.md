By [Tig Kindel](https://twitter.com/tigkindel) - Copyright © [Kindel](http://www.kindel.com), LLC.

![mcec](docs/hero.gif "MCEC: one agent driving another; launch, File ▸ Settings (every tab), mouse-resize, drag the title bar in circles, Help ▸ About; recorded with MCEC's own agent tools")

**MCEC** — the **Model Context Environment Controller** — is eyes, hands, and a safe front door for AI
agents on Windows, and the same battle-tested TCP/serial remote control for integration systems it has
always been.

For agents, it is a small native Windows daemon that gives a **computer use** model (the same capability
Claude, Codex, and similar agents use to see a screen and drive a keyboard and mouse) something to
**mount, see through, and drive** over the **Model Context Protocol (MCP)**: capture windows, read UI
Automation trees, find controls, launch apps, and actuate keyboard/mouse input. For control systems, it
listens on TCP/IP or a serial port and
translates remote commands into keystrokes, text, mouse moves, window messages, and app launches
([Control4](https://www.control4.com/), [Crestron](http://www.crestron.com/), [iRule](http://www.iruleathome.com/),
and others). The 3.0 agent surface is **purely additive**; classic remote control is unchanged.

> [!CAUTION]
> The agent surface is powerful and **off by default**. Once enabled, an agent acts with your rights on
> whatever it targets. Read [Agent Safety](https://tig.github.io/mcec/safety-emergency-stop-and-provisioning.html)
> before you opt in.

## Getting started

Install with winget (recommended):

```
winget install Kindel.mcec
```

Or [download the signed installer](https://github.com/tig/mcec/releases). Launch **MCEC** from the Start
menu when setup finishes.

To let a desktop agent app drive Windows through MCEC, use **Provision new…** on **File ▸ Settings ▸ Agent**
(do not enable agent gates on the Program Files install). The full walkthrough — opt-in, handoff dialog,
MCP client setup, and teardown — is in [Agent Control → Quick start](https://tig.github.io/mcec/agent_control.html#quick-start-use-it-from-a-desktop-agent-app).

## Documentation

Full guides live on the [docs site](https://tig.github.io/mcec/):

* [Install](https://tig.github.io/mcec/install.html): winget, what gets installed where, side-by-side copies
* [Configuration](https://tig.github.io/mcec/configuration.html): Settings, `mcec.settings`, commands, logging
* [Agent Control](https://tig.github.io/mcec/agent_control.html) - the computer use surface: observation, targeting, actuation, MCP/HTTP
* [Agent Safety](https://tig.github.io/mcec/safety-emergency-stop-and-provisioning.html): consent, provisioning, emergency stop, overlay
* [Remote Control](https://tig.github.io/mcec/remote_control.html): TCP/serial commands and User Activity Monitor
* [Examples](https://tig.github.io/mcec/examples.html): worked agent-driving recipes

Developers and agents: [AGENTS.md](AGENTS.md) (connect-time guidance and the MCEC-drives-MCEC dogfood test).
Contributor docs (CI, signing, architecture, image regeneration) live in [`dev/`](dev/).

## Integrations

* [Control4 User Activity Driver](https://github.com/tig/User_Activity)
* [Control4.MceControllerDriver](https://github.com/garrynewman/Control4.MceControllerDriver)