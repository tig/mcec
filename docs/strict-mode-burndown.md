# Strict-mode burn-down backlog

This repo adopts WinPrint's build conventions (.NET 10, `.editorconfig`, `Directory.Build.props`
with `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` + `Nullable` + custom Roslyn analyzers).

The mechanical style (file-scoped namespaces, target-typed `new`, braces, etc.) has already been
applied via `dotnet format` and is **enforced as errors**. The build is green.

The deeper findings cannot be auto-fixed and were **not** applied in the conversion commit because
they change semantics and must be validated by running the test suite, which only executes on
Windows (the conversion was done on Linux, where the WinForms/Win32 tests cannot run). They are kept
**enabled but downgraded to warnings** via `<WarningsNotAsErrors>` in `Directory.Build.props` so the
build stays green while they are driven to zero.

## What to grind (run on Windows so `dotnet test` validates each change)

Current counts (`dotnet build MCEControl.slnx`):

| Category | Codes | Count | Notes |
|---|---|---|---|
| Nullable reference types | CS8618, CS8602, CS8622, CS8600, CS8625, CS8604, CS8603, CS8601, CS8605 | ~818 | No auto-fixer; annotate/guard each. CS8618 (uninitialized non-nullable field) dominates. |
| One type per file | MCEC0001 | 78 | Split secondary top-level types into their own files. |
| No nested types | MCEC0002 | 18 | Promote nested types to top-level. |
| Collection expressions | IDE0028 | 34 | `dotnet format` left these; apply `[...]` collection expressions. |

## How to finish (make it fully strict, matching WinPrint)

1. On Windows with the .NET 10 SDK, `dotnet build MCEControl.slnx` to see the warnings.
2. Fix a category, `dotnet test MCEControl.slnx` to confirm no behavior regressions, commit.
3. As each category reaches zero, **remove its codes from `<WarningsNotAsErrors>`** in
   `Directory.Build.props` so it becomes a hard error again.
4. When the list is empty, `Directory.Build.props` matches WinPrint's fully-strict posture.

The `MCEControl.Analyzers` house-style rules (MCEC0001/MCEC0002) are defined in
`tools/MCEControl.Analyzers/`.
