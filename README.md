By Tig Kindel ([@tigkindel on Twitter](http://www.twitter.com/ckindel)) - Copyright © [Kindel](http://www.kindel.com), LLC.

![mcec](https://tig.github.io/mcec/docs/hero.gif "MCEC — one agent driving another: launch, Help ▸ About, File ▸ Settings, File ▸ Exit, recorded with MCEC's own agent tools")

**MCEC** (Model Context Environment Controller) is eyes, hands, and a safe front door for agents on Windows; and the same battle-tested remote control for smart-home systems.

For its long-standing role, MCEC provides robust control of Windows PCs for smart home systems. It runs in the background listening on the network (or serial port) for commands. It then translates those commands into actions such as keystrokes, text input, and the starting of programs. Any remote control, home control system, or application that can send text strings via TCP/IP or a serial port can use MCEC to control a Windows PC.

New in 3.0, MCEC is also an **agent-automation server**: it can see the screen (capture a screenshot), query the UI Automation tree, find and wait for elements, and drive native Windows apps — exposed to AI agents and scripts over the **Model Context Protocol (MCP)**. All of these agent capabilities are opt-in and disabled by default. This is purely additive; every existing HTPC and smart-home feature is unchanged.

* New in 3.0 — Agent automation: see the [Agent Server user guide](docs/agent-server.md) and [AGENTS.md](AGENTS.md) (agent guidance + dogfood recipe).

* [Home Page](https://tig.github.io/mcec/)
* [Download & Install](https://github.com/tig/mcec/releases)
* [Documentation](https://tig.github.io/mcec/documentation.html)
* [Tig's Blog Posts on MCEC](https://ceklog.kindel.com/?s=mcec)

# Integrations
* [Control4 User Activity Driver](https://github.com/tig/User_Activity)
* [Control4.MceControllerDriver](https://github.com/garrynewman/Control4.MceControllerDriver)
