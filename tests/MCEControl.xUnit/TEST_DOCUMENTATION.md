# MCE Controller Unit Test Suite

## Overview

This document describes the comprehensive unit test suite for MCE Controller. Tests are organized from the bottom of the architecture up, following the dependency hierarchy.

## Test Structure

```
MCEControl.xUnit/
??? Win32/
?   ??? Win32HelpersTests.cs          # Win32 utility functions
??? Helpers/
?   ??? LoggerTests.cs                # Logging singleton
?   ??? CommandFileWatcherTests.cs    # File system monitoring
??? Services/
?   ??? ServiceBaseTests.cs           # Base service functionality
?   ??? AppSettingsTests.cs           # Configuration management
??? Commands/
?   ??? CommandTests.cs               # Base command functionality
?   ??? SerializedCommandsTests.cs    # Command serialization
?   ??? PauseCommandTests.cs          # Pause/delay commands
?   ??? CharsCommandTests.cs          # Character input
?   ??? SendInputCommandTests.cs      # Keyboard simulation
?   ??? MouseCommandTests.cs          # Mouse control
?   ??? StartProcessCommandTests.cs   # Process launching
?   ??? SendMessageCommandTests.cs    # Windows messages
?   ??? ShutdownCommandTests.cs       # System power
?   ??? McecCommandTests.cs           # Internal control
?   ??? SetForegroundWindowCommandTests.cs  # Window focus
?   ??? CommandInvokerTests.cs        # Command registry and execution
??? WindowsInput/
?   ??? InputBuilderTests.cs          # Input structure building
?   ??? InputSimulatorTests.cs        # Input simulation facade
??? Integration/
    ??? CommandExecutionPipelineTests.cs  # End-to-end command flow
```

## Test Coverage by Layer

### Layer 1: Win32 and Utilities (Foundation)

#### Win32HelpersTests (6 tests)
- **Purpose**: Test low-level Win32 utility functions
- **Coverage**:
  - LOWORD/HIWORD extraction
  - Boundary conditions (0, max values)
  - Bit manipulation correctness

#### LoggerTests (4 tests)
- **Purpose**: Test singleton logging infrastructure
- **Coverage**:
  - Singleton pattern
  - Log file path management
  - Log4net integration

#### CommandFileWatcherTests (3 tests)
- **Purpose**: Test file system monitoring for hot-reload
- **Coverage**:
  - Watcher creation
  - File change detection
  - Event firing

### Layer 2: Services (Business Logic)

#### ServiceBaseTests (6 tests)
- **Purpose**: Test common service functionality
- **Coverage**:
  - Status management
  - Notification pattern
  - Event delegation
  - Error handling

#### AppSettingsTests (4 tests)
- **Purpose**: Test application configuration
- **Coverage**:
  - Settings path resolution (Program Files vs. standalone)
  - Serialization/deserialization
  - Telemetry filtering
  - Default settings creation

### Layer 3: Command System (Core Domain)

#### CommandTests (9 tests)
- **Purpose**: Test base command functionality
- **Coverage**:
  - Command pattern implementation
  - Enabled/disabled behavior
  - Command cloning with reply context
  - Embedded command support
  - User-defined vs built-in commands
  - Command type discovery

#### SerializedCommandsTests (5 tests)
- **Purpose**: Test command persistence
- **Coverage**:
  - XML serialization
  - File I/O
  - Round-trip data integrity
  - Multiple command types
  - Missing file handling

#### Individual Command Tests (9 command type test files, ~40+ tests total)

**Common test patterns for each command type:**
1. Constructor initialization
2. Built-in command definitions
3. Clone functionality
4. Disabled command behavior
5. Property preservation
6. ToString formatting
7. Command-specific features

**Command Types Covered:**
- **PauseCommand** (7 tests): Timing, delay handling, invalid args
- **CharsCommand** (6 tests): Text input, argument handling
- **SendInputCommand** (8 tests): Keyboard simulation, modifier keys
- **MouseCommand** (5 tests): Mouse actions, coordinate parsing
- **StartProcessCommand** (8 tests): Process launching, embedded commands
- **SendMessageCommand** (8 tests): Window messages, parameter passing
- **ShutdownCommand** (6 tests): System power commands
- **McecCommand** (6 tests): Internal control commands
- **SetForegroundWindowCommand** (5 tests): Window focus management

#### CommandInvokerTests (5 tests)
- **Purpose**: Test command registry and execution engine
- **Coverage**:
  - Command creation from files
  - Built-in command loading
  - User-defined commands
  - Command modification persistence
  - Enabled/disabled filtering

### Layer 4: Windows Input Simulation

#### InputBuilderTests (18 tests)
- **Purpose**: Test INPUT structure construction
- **Coverage**:
  - Keyboard input building
  - Mouse input building
  - Character sequences
  - Modifier key combinations
  - Absolute/relative positioning
  - Wheel scrolling
  - Method chaining

#### InputSimulatorTests (21 tests across 3 test classes)
- **Purpose**: Test facade for input simulation
- **Coverage**:
  - **InputSimulator**: Facade initialization and property access
  - **KeyboardSimulator**: Key press/release, text entry, modified keystrokes
  - **MouseSimulator**: Button clicks, movement, scrolling
  - Fluent interface pattern

### Layer 5: Integration Tests

#### CommandExecutionPipelineTests (6 tests)
- **Purpose**: Test end-to-end command execution flow
- **Coverage**:
  - Command parsing and enqueueing
  - Command execution
  - Reply context handling
  - Embedded command sequences
  - Unknown command handling
  - Single character interpretation
  - Colon-syntax command parsing

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
- Tests can run in parallel

### 4. Mock Objects
- Custom `TestReply` class for reply context testing
- Concrete test implementations of abstract classes (e.g., `TestService`, `TestCommand`)
- Minimal mocking - prefer concrete implementations when possible

### 5. Boundary Testing
- Zero values
- Maximum values
- Null/empty strings
- Invalid input handling

## Test Metrics

### Current Coverage
- **Total Test Files**: 21
- **Total Tests**: ~120+
- **Layers Covered**: 5/5 (100%)
- **Core Components**: 100% (all major components have tests)

### Coverage by Component Type
| Component Type | Test Files | Approximate Tests |
|----------------|------------|-------------------|
| Win32 Utilities | 1 | 6 |
| Helpers | 2 | 7 |
| Services | 2 | 10 |
| Commands | 11 | 65+ |
| Windows Input | 2 | 39 |
| Integration | 1 | 6 |

## Running the Tests

### From Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All" or select specific tests
3. View results inline with code coverage

### From Command Line
```bash
cd tests\MCEControl.xUnit
dotnet test
```

### With Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## What's NOT Tested (Intentional Gaps)

### 1. UI Components
- WinForms controls (MainWindow, dialogs)
- Reason: Requires UI automation framework
- Mitigation: Manual testing, integration tests

### 2. Network I/O
- SocketServer/SocketClient actual connections
- SerialServer hardware communication
- Reason: Requires infrastructure/mocking complexity
- Mitigation: ServiceBase abstraction is tested

### 3. Win32 API Calls
- Actual SendInput to OS
- PostMessage/SendMessage to real windows
- Process.Start execution
- Reason: Side effects on test environment
- Mitigation: Input structures validated, command logic tested

### 4. File System Operations
- Actual file watching (flaky in CI)
- Registry access
- Reason: Environmental dependencies
- Mitigation: Abstraction layers tested

### 5. External Dependencies
- Azure Application Insights telemetry
- GitHub API (UpdateService)
- Reason: Network dependency, rate limits
- Mitigation: Service initialization tested

## Extending the Test Suite

### Adding Tests for New Commands

1. Create test file: `Commands\YourCommandTests.cs`
2. Follow existing command test pattern:

```csharp
public class YourCommandTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties() { }
    
    [Fact]
    public void BuiltInCommands_ContainsYourCommands() { }
    
    [Fact]
    public void Clone_CreatesIndependentCopy() { }
    
    [Fact]
    public void Execute_WhenDisabled_ReturnsFalse() { }
    
    [Fact]
    public void Execute_WithValidInput_ReturnsTrue() { }
    
    [Fact]
    public void ToString_ReturnsFormattedString() { }
}
```

### Adding Integration Tests

1. Create test in `Integration\` folder
2. Test complete workflows across multiple components
3. Use minimal mocking
4. Focus on happy path and error conditions

### Adding Performance Tests

Consider adding:
- Command execution throughput tests
- Input builder performance tests
- Large command queue handling

## Continuous Integration

The test suite is designed to:
- Run quickly (< 10 seconds for full suite)
- Run in parallel (no shared state)
- Run without admin privileges
- Run without network access
- Run on Windows only (.NET 8 Windows-specific APIs)

## Future Enhancements

1. **Code Coverage Analysis**: Integrate coverlet for detailed coverage reports
2. **Mutation Testing**: Use Stryker.NET to validate test quality
3. **Property-Based Testing**: Use FsCheck for command parsing edge cases
4. **Performance Benchmarks**: Use BenchmarkDotNet for input simulation
5. **Approval Tests**: For XML serialization round-trips
6. **UI Automation**: Consider Appium/WinAppDriver for MainWindow tests

## Conclusion

This test suite provides comprehensive coverage of the MCE Controller architecture from the ground up. Tests are organized by architectural layer, following best practices for maintainability and reliability. The suite serves as both validation and documentation of the system's behavior.
