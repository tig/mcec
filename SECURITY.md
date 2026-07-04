<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Security Policy

## Reporting a vulnerability

MCEC can drive a Windows desktop with real keyboard/mouse input and can expose an MCP / localhost-HTTP
control surface, so security reports are taken seriously. **Please do not open a public issue for a security
vulnerability.**

Report it privately through GitHub instead:
**[Report a vulnerability](https://github.com/tig/mcec/security/advisories/new)**
(the repository's **Security ▸ Advisories ▸ Report a vulnerability**). This opens a private advisory visible
only to you and the maintainers. You will get an acknowledgement, and we will coordinate a fix and
disclosure with you.

## Supported versions

Security fixes target the latest 3.0.x release. Please reproduce on the current version
(`winget upgrade Kindel.mcec`) before reporting, in case the issue is already fixed.

## What is most valuable

MCEC's agent surface is **off by default** and gated behind explicit, independent opt-ins (see
[Agent Safety](docs/safety-emergency-stop-and-provisioning.md)). Reports are especially valuable when they
show a way to:

- reach an agent tool or `send_command` **without** the required gates (`AgentCommandsEnabled`, per-command
  `Enabled`, `McpServerEnabled`);
- cross the localhost/bearer-token boundary of the MCP HTTP floor (a non-loopback bind or a request without
  a valid token reaching actuation);
- defeat or bypass the **emergency stop**;
- break the isolation of a provisioned session (act on the installed instance, or tear down a session
  without its token).
