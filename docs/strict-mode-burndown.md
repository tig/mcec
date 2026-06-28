# Strict-mode burn-down — COMPLETE

This repo adopts WinPrint's build conventions (.NET 10, `.editorconfig`, `Directory.Build.props`
with `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` + `Nullable` + custom Roslyn analyzers).

The burn-down described below has been **completed**. Every category is at **zero** and is now
enforced as a build **error** (not a warning). `dotnet build MCEControl.slnx -c Release` and
`dotnet test MCEControl.slnx -c Release` are both green (115 tests pass) on Windows / .NET 10.

## What was driven to zero (all now hard errors)

| Category | Codes | Original count | Now |
|---|---|---|---|
| Nullable reference types | CS8600–CS8625, CS8762–CS8777 | ~844 | **0 (error)** |
| One type per file | MCEC0001 | ~82 | **0 (error)** |
| No nested types | MCEC0002 | ~28 | **0 (error)** |
| Collection expressions | IDE0028 / IDE0300 / IDE0305 | ~48 | **0 (error)** |

Each category's codes were removed from `<WarningsNotAsErrors>` in `Directory.Build.props` as it
reached zero, so the codebase now matches WinPrint's fully-strict posture.

## How it was done (behavior-neutral)

- **Collection expressions:** object/array/list initializers converted to `[...]`.
- **MCEC0001:** multi-type files split so each top-level type lives in its own file (same
  namespace, so no call-site changes). `Win32Structs.cs` became 26 files; the `ACL`/`ACE`/`LUID`
  interop structs use `_Struct`-suffixed filenames to avoid case-insensitive collisions with the
  existing `Acl`/`Ace`/`Luid` wrapper files.
- **MCEC0002:** nested types promoted to top-level in the same namespace (HookManager interop
  structs, the Socket/Serial reply-context classes, the Telnet enums, `POWERBROADCAST_SETTING`).
  Only references that were *qualified* by the former outer type needed touching
  (`SocketServer.ServerReplyContext` → `ServerReplyContext` in `MainWindow.cs`, plus a stale
  `CA1034` `GlobalSuppressions` entry removed).
- **Nullable:** annotations only — nullable `?`, the null-forgiving operator `!`, and `= null!;`
  initializers for late-initialized non-nullable fields/properties (WinForms controls, events,
  XML-serialized members). Event-handler signatures use `object? sender` to match the
  `EventHandler` delegates. No logic, control flow, or values were changed, so the 115-test
  WinForms/Win32 suite stays green.

## Remaining (intentionally kept as warnings)

`<WarningsNotAsErrors>` retains only the two WinForms platform advisories, matching WinPrint:

- `WFO1000` — WinForms source-generator advisory.
- `WFDEV004` — `Form.Closing` obsoletion in `MainWindow.Designer.cs` (designer-generated code).

These are platform/tooling advisories, not part of the house-style burn-down. The
`MCEControl.Analyzers` house-style rules (MCEC0001/MCEC0002) are defined in
`tools/MCEControl.Analyzers/`.
