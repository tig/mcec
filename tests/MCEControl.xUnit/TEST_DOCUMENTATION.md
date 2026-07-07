# MCEC Unit Test Suite

## Overview

This document describes the conventions and structure of MCEC's xUnit test suite, organized from the
bottom of the architecture up, following the dependency hierarchy. It intentionally does **not** hardcode
test counts or a full file inventory; those go stale the moment a test is added or renamed. For current
numbers, run the suite (see below) or browse the test project directly; its directory structure
(`Agent/`, `Commands/`, `Services/`, `Dialogs/`, `Helpers/`, `WindowsInput/`, `Integration/`) mirrors
`src/`'s layout one-to-one.

## Test Patterns and Best Practices

### 1. Arrange-Act-Assert (AAA) Pattern
All tests follow the AAA pattern:
```csharp
[Fact]
public void Method_Scenario_ExpectedBehavior()
{
    // Arrange: Set up test data
    var command = new TestCommand { Property = "value" };

    // Act: Execute the method under test
    var result = command.Execute();

    // Assert: Verify the expected outcome
    Assert.True(result);
}
```

### 2. Test Naming Convention
`MethodName_Scenario_ExpectedBehavior`
- Examples:
  - `Execute_WhenDisabled_ReturnsFalse`
  - `Clone_WithEmbeddedCommands_ClonesNested`
  - `Constructor_SetsDefaultProperties`

### 3. Test Isolation
- Each test creates its own instances
- Temporary files are cleaned up in IDisposable pattern
- No shared state between tests
- Tests can run in parallel, EXCEPT classes that touch process-global `AgentRuntime` state (emergency
  stop, ambient session, settings/invoker): those carry `[Collection("AgentSerial")]`
  (`Agent/AgentSerialCollection.cs`) so xUnit runs them sequentially and they don't stomp on each other

### 4. Mock Objects
- Custom `TestReply` class for reply context testing
- Concrete test implementations of abstract classes (e.g., `TestService`, `TestCommand`)
- Minimal mocking - prefer concrete implementations when possible

### 5. Boundary Testing
- Zero values
- Maximum values
- Null/empty strings
- Invalid input handling

## Running the Tests

```bash
cd tests/MCEControl.xUnit
dotnet test
```

With coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Note: `ci.yml` already collects coverage this way but nothing currently reports or gates on the result.

### Desktop end-to-end tests

A handful of tests (`Integration/AgentDesktopE2ETests.cs`, `Integration/RecordDesktopTests.cs`, anything
attributed `[DesktopInputFact]`) drive the real desktop (global keystrokes, mouse, launching apps) and are
**skipped unless `MCEC_DESKTOP_E2E=1`** is set, so a normal `dotnet test`/CI run never touches your desktop:

```powershell
$env:MCEC_DESKTOP_E2E=1 ; dotnet test --filter Category=DesktopE2E
```

`.github/workflows/real-windows.yml` is intended to run these on a self-hosted interactive runner, but as
of this writing does not set `MCEC_DESKTOP_E2E=1` in its test step, and no such runner is registered
(`INTERACTIVE_RUNNER_READY` repo variable is unset); see the workflow file for current status.

## What's NOT Tested (known, current gaps)

- **`SerialServer`** (`src/Services/SerialServer.cs`): no tests at all.
- **`MCEControl.Hooks`** (`src/Hooks/`, the global low-level keyboard/mouse hook layer): no direct tests;
  only an indirect subscriber-count snapshot via `Services/HookSubscriberBaseline.cs`, used by
  `UserActivityMonitorServiceTests`.
- **Actual OS-level side effects**: real `SendInput` to the OS, real window messages to real windows,
  real `Process.Start` are exercised only by the opt-in desktop E2E tests above, not the default suite.
- **External dependencies**: Azure Application Insights telemetry and the GitHub Releases API
  (`UpdateService`) are exercised via seams/fakes, not live network calls.

## Extending the Test Suite

### Adding tests for a new Command type

1. Create `Commands/YourCommandTests.cs`.
2. Cover, at minimum: constructor defaults, `BuiltInCommands` contents, `Clone` independence,
   disabled-command behavior (`Execute()` returns `false`), and any command-specific logic.
3. If the command is agent-gated, add it to `CommandRegistryTests`/`AgentCommandStructuralGateTests`'
   completeness checks rather than a bespoke test (see `CommandRegistry` / `CommandRegistryTests`).

### Adding integration tests

1. Create the test in `Integration/`.
2. Test complete workflows across multiple components with minimal mocking.
3. Focus on the happy path and the error conditions that matter.
