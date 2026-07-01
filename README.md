By Tig Kindel ([@ckindel on Twitter](http://www.twitter.com/ckindel)) - Copyright © [Kindel](http://www.kindel.com), LLC.

![mcec](docs/hero.gif "MCEC — one agent driving another: launch, File ▸ Settings (every tab), mouse-resize, drag the title bar in circles, Help ▸ About, recorded with MCEC's own agent tools")

**MCEC** — the **Model Context Environment Controller** — is eyes, hands, and a safe front door for AI agents on Windows.

It is a small, self-contained native Windows daemon that a computer-use model can **mount, see through, and drive**. An agent runs the loop *observe → target → act → observe*, and MCEC gives it all four: capture a window as a PNG, read its UI Automation tree, find and wait for controls, launch apps, and actuate keyboard/mouse/window input — exposed to agents and scripts over the **Model Context Protocol (MCP)** (stdio via `mcec.exe --mcp`, or a localhost HTTP floor).

```
mcec.exe --mcp        # run headless as an MCP stdio server an agent can mount
```

Every agent capability is **opt-in, disabled by default, localhost-bound, and loudly audit-logged**, with a global emergency-stop hotkey and disposable isolated sessions so the operator stays in control.

MCEC is also the same **battle-tested remote control for home-automation systems** it has always been. In its long-standing role it runs in the background listening on the network (or a serial port) for commands, and translates them into keystrokes, text input, mouse moves, window messages, and app launches. Any remote control or home-control system that can send text over TCP/IP or RS-232 — [Control4](https://www.control4.com/), [iRule](http://www.iruleathome.com/), [Crestron](http://www.crestron.com/), and others — can use MCEC to drive a Windows PC. The agent surface in 3.0 is **purely additive**: every existing home-automation feature is unchanged.

* [Documentation](https://tig.github.io/mcec/documentation.html) — start here
* [Agent Server user guide](docs/agent-server.md) — the full agent/MCP tool reference and security model
* [Agent safety](docs/safety-emergency-stop-and-provisioning.md) — emergency stop + isolated session provisioning
* [Home Automation & Remote Control](docs/home-automation.md) — the classic TCP/serial command surface
* [AGENTS.md](AGENTS.md) — connect-time agent guidance + the dogfood recipe (MCEC driving MCEC)

Links:

* [Home Page](https://tig.github.io/mcec/)
* [Download & Install](https://github.com/tig/mcec/releases)
* [Tig's Blog Posts on MCEC](https://ceklog.kindel.com/?s=mcec)

# Integrations
* [Control4 User Activity Driver](https://github.com/tig/User_Activity)
* [Control4.MceControllerDriver](https://github.com/garrynewman/Control4.MceControllerDriver)
