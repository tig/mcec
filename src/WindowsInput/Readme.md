# WindowsInput (Windows Input Simulator)

This is a fork of Michael Noonan's **Windows Input Simulator** (`InputSimulator`), a C# wrapper
around the Win32 `SendInput` API for simulating keyboard and mouse input.

- Original home (no longer valid): http://inputsimulator.codeplex.com/ (CodePlex shut down in 2017)
- Current upstream mirror: https://github.com/michaelnoonan/inputsimulator
- License: Microsoft Public License (Ms-PL); see [license.txt](license.txt)

The snapshot MCEC vendored is CodePlex-era (pre-2013, no exact version/commit was recorded at the
time). It has since been modified in place; there is no plan to track upstream.

## Why MCEC uses it

Every actuation path goes through this code: the `chars:`/`key:`/shift-key commands
(`SendInputCommand`), `mouse:` (`MouseCommand`, including the agent drag primitive #123 and the
`click` tool #122), and the emergency stop's held-input release (#135,
`EmergencyStop.ReleaseHeldInput`). It is security-relevant surface; treat changes accordingly.

## Known local divergences from upstream

- **Middle-button support** (commit `a399557`, 2020): `IMouseSimulator`/`MouseSimulator` gained
  `MiddleButtonDown/Up/Click/DoubleClick` (upstream only exposes left/right/X buttons). MCEC's
  `mouse:` command and the emergency stop's release path depend on these.
- **Virtual-desktop absolute moves**: `MoveMouseToPositionOnVirtualDesktop` /
  `InputBuilder.AddAbsoluteMouseMovementOnVirtualDesktop` (`MOUSEEVENTF_VIRTUALDESK`) are present in
  this copy and load-bearing for multi-display agent actuation (drag #123, click #122). Upstream's
  GitHub mirror has an equivalent API; verify semantics before assuming parity.
- **.NET port and house style**: ported off .NET Framework, nullable-annotated, and reformatted to
  repo conventions (one type per file, collection expressions, strict analyzers).
- **P/Invoke fixes (#203/#210)**: `GetClassName` returns `int` (was wrongly `IntPtr`) and is
  explicitly `CharSet.Unicode`; `FindWindow` is explicitly Unicode (was ANSI-marshaled); the unused
  `FindWindowByCaption` was deleted; `Native/NativeMethods.cs` is the single declaration site for
  `SendInput`/`GetKeyState`/`GetAsyncKeyState`/`GetMessageExtraInfo`.

## Known upstream bugs still present

- `InputBuilder.AddCharacter` (~line 141): the "extended key" test
  `if ((scanCode & 0xFF00) == 0xE000)` is vestigial. With `KEYEVENTF_UNICODE` the `Scan` field
  carries the *character* (not a hardware scan code), so the 0xE0-prefix check is meaningless; it
  never fires for real extended keys and would only (spuriously) fire for U+E0xx private-use-area
  characters. Harmless for normal text; left as-is to stay diffable against upstream.

## Possible replacement: H.InputSimulator (decision deferred; see #27)

[`H.InputSimulator`](https://github.com/HavenDV/H.InputSimulator) is the maintained NuGet descendant
of this library (same API lineage, modern TFMs).

**Pros**: actively maintained; deletes ~15 vendored files; nullable/modern-.NET support comes for
free; upstream bug fixes (including the extended-key handling) flow in via package updates.

**Cons**: the local additions above (middle-button API, virtual-desktop moves) and the exact
`SendInput` batching behavior MCEC's drag/click and emergency-stop release depend on must be
verified against it, not assumed; it swaps first-party-auditable code for a third-party package on a
security-relevant input-injection surface (supply-chain exposure, signing/gating review); the #210
signature fixes are already done here, removing the main correctness motivation.

**Decision**: deferred. This readme is the provenance record in the meantime; #27 tracks the
evaluation.
