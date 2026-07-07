<!--
// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec
-->

# Examples

Worked, runnable examples of driving Windows with MCEC. Each one is written to serve **two readers at
once**: a person skimming to see what MCEC can do, and an **agent that executes it**. So every example
leads with the artifact and the plain-English prompt, then gives the exact recipe an agent follows.

## The gallery

| Example | Flavor | What it shows | Recipe |
|---------|--------|---------------|--------|
| **MCEC hero GIF** | Scripted recipe | One MCEC drives a second MCEC through a guided tour (Settings tabs, mouse-resize, drag the title bar, Help ▸ About) while the overlay narrates; recorded with the `record` tool. | [hero-gif.md](hero-gif.md) |
| **WinPrint hero GIF** | Scripted recipe | MCEC drives installed **WinPrint** through a guided tour (launch, settings/zoom, Print to PDF), recorded as a clean, window-only GIF. Owned and produced in the WinPrint repo. | [hero-gif-win.md](https://github.com/tig/winprint/blob/develop/docs/hero-gif-win.md) |
| **Paint → smiley → email** | Prompt demo | Hand a computer-use agent one sentence and it drives the desktop: open Paint, draw a smiley, copy it, start a new email, paste it in. | [paint-smiley-email.md](paint-smiley-email.md) |

## Two flavors

- **Scripted recipe.** A deterministic, choreographed tour that produces a **specific artifact** (a hero
  GIF) held to a visual bar. The recipe is a precise, numbered sequence of tool calls, and it doubles as a
  regression test: re-running it is how the artifact is regenerated, so the doc can't drift from what
  actually works. Committed doc images (PNG/GIF under `docs/`) are always produced this way — see
  [doc-images.md](doc-images.md).
- **Prompt demo.** A natural-language **task** you hand a capable computer-use agent, which it *improvises*
  with MCEC's tools. The point is to showcase the capability surface, not a pixel-perfect artifact, so the
  page is mostly the prompt, the gates it needs, and a captured recording of one good run.

## How these examples work

Everything below is common to every example; individual pages assume it and don't repeat it.

- **Bootstrap a controller first.** An agent can't drive MCEC over MCP before anything is listening, so one
  small in-repo script stands up the first authorized, MCP-serving MCEC (for the mcec hero that's
  [`scripts/Generate-HeroGif.ps1`](../scripts/Generate-HeroGif.ps1); it prints a machine-readable
  `HERO_MCP_URL=`). From there the agent does the rest over MCP; no tour logic ships in the repo.
- **Drive a disposable subject, not your install.** The controller [`provision-session`](safety-emergency-stop-and-provisioning.md)s
  a throwaway copy (or the recipe launches a clean copy from its own dir) and drives *that*, so a crash
  never leaves your installed MCEC with agent gates enabled.
- **The MCP envelope is double-wrapped.** A tool's `{ ok, result, error }` envelope arrives as a JSON
  **string** inside the JSON-RPC `result.content[].text` block. Unwrap it, branch on `ok`, then read
  `result` (success) or `error.code` (failure). See the reference call in [hero-gif.md](hero-gif.md).
- **Pass absolute paths.** The controller's working directory is its disposable copy, so any repo path you
  hand a tool (e.g. `record stop`'s `file`) must be absolute or the output lands in the temp copy and is lost.
- **Targeting gotchas that bite every recipe:**
  - **Element-by-name is global AND a substring match.** `click`/`query` with `at: { by: name }` resolves
    across **all** top-level windows (not just the one you targeted by `handle`/`window`/`foreground`), and
    it matches on **substring**, not exact text. Two failure modes: a name in two windows (a **"File"** menu
    in both the controller and the subject; a WinForms tab whose **TabItem header and its page share a
    Name**) is ambiguous and fails with `selector-matched-2`; and a name that is a substring of another
    **silently hits the wrong one** (ok, no error): `value:"Serial Server"` selects the **Server** tab.
    Window-title matching has the same trap: `window:"Settings"` matches the Windows **Settings** app when
    it is open, not your dialog. Drive menus by **keyboard** (`Alt`+mnemonic via `send_command
    shiftdown:alt` / `chars:<x>` / `shiftup:alt`; read the real mnemonic from the source `.Designer.cs`).
    For anything that can collide, target by **handle**: open the window/dialog, read its handle from
    `query { foreground:true }` (the unwrapped envelope is `result.window.handle`), then pass `handle` to
    every `capture`/`click`. **Always re-view each screenshot** - the wrong tab looks plausible.
  - **Tabs report null bounds; compute the header point yourself.** A WinForms `TabItem` comes back from
    `query` with `bounds: null`, so you can't read a center pixel to click. Compute it from the dialog's
    window rect (`query { handle }` -> `result.window` gives `x`/`y`): the tab strip sits at client-y ~27,
    tabs left to right; screen point = `(winX + 8 + tabCenterX, winY + 31 + 27)` (8 = Win11 side border,
    31 = title bar). Coordinate-click that.
  - **`PrintWindow` captures the Win11 invisible frame as black.** A `capture` of a window includes the
    **8px** invisible resize-border on the left/right/bottom as a black band (top is clean); crop those
    three sides (or capture a region). A FixedDialog captured at 491x412 crops to 475x404.
  - **Ctrl+key needs a real accelerator, not `chars:`.** A held `shiftdown:ctrl` plus `chars:c` injects a
    *character*, which does not reliably trigger an app's Ctrl+key accelerator (so Ctrl+A/Ctrl+C/Ctrl+V can
    silently do nothing). Send VK+modifier SendInput commands instead (the built-in `ctrl-x`, or define
    `ctrl-a`/`ctrl-c`/`ctrl-v`). Menu mnemonics via `chars:` are fine because those are plain characters.

## Adding an example

Copy this skeleton to `docs/<name>.md`, fill it in, and add a row to the gallery table above. Keep the
artifact and the prompt at the very top so a human sees the payoff first and an agent finds the task fast.

```markdown
# <Title>

<One sentence on what it shows.> <Embed the artifact: ![...](<name>.gif).>

**Flavor:** Scripted recipe | Prompt demo

## The prompt

> <The exact natural-language instruction you'd give an agent.>

## Preconditions

- Gates/commands to enable (link [Agent Safety](safety-emergency-stop-and-provisioning.md)).
- Bootstrap script, if one is needed.

## Recipe

<Numbered tool-call steps (scripted), OR "hand the agent the prompt and let it improvise" (prompt demo).>

## Expected result

<What success looks like: the artifact, frame count, the final on-screen state.>

## Gotchas

<Anything specific to this example beyond the shared list in examples.md.>
```

See also [AGENTS.md](https://github.com/tig/mcec/blob/main/AGENTS.md) for the connect-time guidance an
agent gets, and [Agent Control](agent_control.md) for the full tool reference.
