By Tig Kindel ([@tigkindel on Twitter](https://twitter.com/tigkindel)) - Copyright © [Kindel](http://www.kindel.com), LLC.

![mcec](docs/hero.gif "MCEC: one agent driving another; launch, File ▸ Settings (every tab), mouse-resize, drag the title bar in circles, Help ▸ About; recorded with MCEC's own agent tools")

**MCEC**: the **Model Context Environment Controller**; is eyes, hands, and a safe front door for AI agents on Windows.

It is a small, self-contained native Windows daemon that a computer-use model can **mount, see through, and drive**. An agent runs the loop *observe → target → act → observe*, and MCEC gives it all four: capture a window as a PNG, read its UI Automation tree, find and wait for controls, launch apps, and actuate keyboard/mouse/window input; exposed to agents and scripts over the **Model Context Protocol (MCP)** (stdio via `mcec.exe --mcp`, or a localhost HTTP floor).

**Install with winget:**

```
winget install Kindel.mcec
```

or [download the installer](https://github.com/tig/mcec/releases).

```
mcec.exe --mcp        # run headless as an MCP stdio server an agent can mount
```

> [!CAUTION]
> MCEC is powerful and off by default: once you enable it, an agent acts with your rights on whatever it targets. See [Agent Safety](docs/safety-emergency-stop-and-provisioning.md).

MCEC drives the Windows desktop with real user input. There is no sandbox, no permission model inside the session, and no way to give an agent "just a little" control. **Everything a user can do at the keyboard and mouse, an agent can do**: read whatever is on screen, type into any app, click anything, launch programs, open a browser logged in as you, delete files, send email. The gates decide *whether* an agent gets that power; they do not and cannot meter *how much*.

So the operator stays in control by construction:

* **Off by default.** Every agent capability is opt-in behind three independent gates (`AgentCommandsEnabled`, per-command `Enabled`, `McpServerEnabled`), and the network door binds to localhost only (a non-loopback bind requires a bearer token, or MCEC refuses to start it).
* **Visible when on.** An on-by-default on-screen overlay narrates each command as it executes, and every action is logged with a loud `AGENT-AUDIT:` line.
* **Stoppable.** A global emergency-stop hotkey (default `Ctrl+Alt+Shift+S`) halts a session instantly from any window; it reacts to physical input only, so an agent can never trip or defeat it.
* **Disposable.** Rather than enabling your installed instance, an authorized agent gets a throwaway provisioned session; teardown is deleting a directory, and a crash leaves the real install untouched.

Enable the agent surface only on a machine and session where you accept an agent acting as you. Details: [Environment Controller](docs/environment-controller.md) and [Agent safety](docs/safety-emergency-stop-and-provisioning.md).

MCEC is also the same **battle-tested remote control for home-automation systems** it has always been. In its long-standing role it runs in the background listening on the network (or a serial port) for commands, and translates them into keystrokes, text input, mouse moves, window messages, and app launches. Any remote control or home-control system that can send text over TCP/IP or RS-232 ([Control4](https://www.control4.com/), [iRule](http://www.iruleathome.com/), [Crestron](http://www.crestron.com/), and others) can use MCEC to drive a Windows PC. The agent surface in 3.0 is **purely additive**: every existing home-automation feature is unchanged.

* [Documentation](https://tig.github.io/mcec/configuration.html): start here
* [Environment Controller](docs/environment-controller.md): the full agent/MCP tool reference and security model
* [Agent safety](docs/safety-emergency-stop-and-provisioning.md): emergency stop + isolated session provisioning
* [Home Automation & Remote Control](docs/home-automation.md): the classic TCP/serial command surface
* [AGENTS.md](AGENTS.md): connect-time agent guidance + the dogfood recipe (MCEC driving MCEC)

Links:

* [Home Page](https://tig.github.io/mcec/)
* [Download & Install](https://github.com/tig/mcec/releases)
* [Tig's Blog Posts on MCEC](https://ceklog.kindel.com/?s=mcec)

# Integrations
* [Control4 User Activity Driver](https://github.com/tig/User_Activity)
* [Control4.MceControllerDriver](https://github.com/garrynewman/Control4.MceControllerDriver)
