# MCEC Architecture

## Overview

MCEC is a Windows desktop application built on .NET 10 (WinForms) that enables remote control of a Windows PC through multiple communication channels (TCP/IP sockets and serial ports). It receives commands over these channels and executes them by simulating keyboard/mouse input, launching processes, sending Windows messages, and more.

**Primary Use Case**: Home theater PC (HTPC) automation and control, particularly for Windows Media Center environments.

## High-Level Architecture

```
???????????????????????????????????????????????????????????????????
?                         MainWindow (UI)                         ?
?                     (Singleton, WinForms)                       ?
???????????????????????????????????????????????????????????????????
            ?                                     ?
            ?? Settings (AppSettings)            ?
            ?? Logging (Logger)                  ?
            ?? Telemetry (TelemetryService)      ?
            ?? Updates (UpdateService)            ?
                                                  ?
    ?????????????????????????????????????????????????????????????????
    ?         Communication Services            ?    Command Engine ?
    ?????????????????????????????????????????????????????????????????
            ?                                             ?
    ?????????????????????????????????????????   ???????????????????
    ?              ?          ?             ?   ?                 ?
??????????  ???????????  ????????????  ??????????????  ???????????????????
? Socket ?  ? Socket  ?  ?  Serial  ?  ?  Activity  ?  ?  CommandInvoker ?
? Server ?  ? Client  ?  ?  Server  ?  ?  Monitor   ?  ?   (Hashtable)   ?
??????????  ???????????  ????????????  ??????????????  ???????????????????
    ?            ?             ?              ?                  ?
    ?            ?             ?              ?         ???????????????????
    ?????????????????????????????????????????????????????  Command Queue  ?
                                               ?         ? (Concurrent)    ?
                  Receives Command Strings     ?         ???????????????????
                                               ?                  ?
                                               ?         ???????????????????
                                               ?         ?   ICommand      ?
                                               ?         ?  Execution      ?
                                               ?         ???????????????????
                                               ?                  ?
                   ????????????????????????????????????????????????
                   ?                           ?
        ?????????????????????         ?????????????????????
        ?  Windows Input    ?         ?  Command Types    ?
        ?   Simulation      ?         ?????????????????????
        ?????????????????????                  ?
                 ?                    ?????????????????????????
        ???????????????????          ?                       ?
        ?  WindowsInput   ?    SendInputCommand      StartProcessCommand
        ?   Library       ?    SendMessageCommand    MouseCommand
        ?                 ?    CharsCommand          ShutdownCommand
        ? - Keyboard      ?    PauseCommand          McecCommand
        ? - Mouse         ?    SetForegroundWindow   (user-defined)
        ? - InputBuilder  ?
        ???????????????????
```

## Core Components

### 1. MainWindow (Entry Point & Orchestrator)

**Location**: `MainWindow.cs`

**Responsibilities**:
- Singleton pattern - single instance of the application UI
- Lifecycle management for all services
- UI presentation (WinForms with MenuStrip, StatusStrip, system tray icon)
- Coordination between communication services and command execution
- Settings management and persistence
- Window message handling (WM_POWERBROADCAST, WM_QUERYENDSESSION)

**Key Dependencies**:
- All service components (SocketServer, SocketClient, SerialServer, UserActivityMonitorService)
- CommandInvoker (command execution engine)
- Logger, TelemetryService, UpdateService

### 2. Communication Services

All services inherit from `ServiceBase` which provides:
- Common notification pattern via delegates
- Status management (Started, Waiting, Connected, Sleeping, Stopped)
- Error handling
- Telemetry integration

#### 2.1 SocketServer
**Location**: `Services\SocketServer.cs`

**Purpose**: TCP/IP server that listens for incoming client connections

**Features**:
- Listens on configurable port (default: 5150)
- Supports multiple concurrent clients
- Each client gets a unique ServerReplyContext
- Optional "wakeup" command broadcasting on startup/shutdown
- Thread-per-client model for handling connections

#### 2.2 SocketClient
**Location**: `Services\SocketClient.cs`

**Purpose**: TCP/IP client that connects to remote servers

**Features**:
- Connects to configurable host:port
- Auto-reconnect with configurable delay
- Bidirectional communication
- Persistent connection management

#### 2.3 SerialServer
**Location**: `Services\SerialServer.cs`

**Purpose**: Serial port communication

**Features**:
- Configurable serial parameters (baud rate, parity, data bits, stop bits, handshake)
- RS-232 communication for hardware integration
- Event-driven data reception

#### 2.4 UserActivityMonitorService
**Location**: `Services\UserActivityMonitorService.cs`

**Purpose**: Monitors user activity and system events

**Features**:
- Mouse/keyboard input detection
- Session lock/unlock detection
- Power management events (sleep, wake, user presence)
- Debouncing to prevent command flooding
- Generates configurable activity commands

**Dependencies**: Uses `Gma.UserActivityMonitor` library for low-level hook management

### 3. Command System (Command Pattern)

#### 3.1 CommandInvoker
**Location**: `Commands\CommandInvoker.cs`

**Purpose**: Central command registry and execution queue

**Architecture**:
- Hashtable-based command lookup (case-insensitive)
- ConcurrentQueue for command execution
- Combines built-in commands with user-defined commands from `mcec.commands` file
- Parses command strings and routes to appropriate command instances

**Command Sources**:
1. **Built-in commands**: Defined in Command-derived classes (disabled by default for security)
2. **User commands**: Loaded from XML file `mcec.commands`

#### 3.2 Command Base Classes

**Location**: `Commands\Command.cs`

**ICommand Interface**:
```csharp
public interface ICommand {
    bool Execute();
    Command Clone(Reply reply, Command clone);
}
```

**Command Abstract Class**:
- Base for all command types
- XML serialization support
- Enabled/disabled flag (security)
- Support for nested/embedded commands
- Telemetry tracking
- Reply context for bidirectional communication

#### 3.3 Command Types

All commands are located in the `Commands\` directory:

| Command Type | Purpose | Key Features |
|--------------|---------|--------------|
| **SendInputCommand** | Keyboard simulation | Uses WindowsInput library; supports modifier keys (Shift, Ctrl, Alt, Win); virtual key codes |
| **CharsCommand** | Text input | Types character sequences; uses SendInput |
| **MouseCommand** | Mouse control | Movement, clicks, double-clicks, wheel scrolling |
| **StartProcessCommand** | Launch applications | Process.Start wrapper; supports arguments, verbs; can embed commands to execute after process starts |
| **SendMessageCommand** | Windows messages | Posts WM_* messages to windows; PostMessage/SendMessage |
| **SetForegroundWindowCommand** | Window focus | Brings window to foreground |
| **ShutdownCommand** | System power | Shutdown, restart, logoff, sleep, hibernate |
| **PauseCommand** | Timing control | Thread.Sleep for command pacing |
| **McecCommand** | Internal control | Controls MCEC itself (reload, shutdown, etc.) |
| **CaptureCommand** | Agent: observe | Screenshot a window/region via `PrintWindow` (`PW_RENDERFULLCONTENT`) → PNG/base64 |
| **QueryCommand** | Agent: observe | Dump the UI Automation tree of a window (via FlaUI) |
| **FindCommand** | Agent: target | `find` / `wait-for` a UIA element by name/automation-id/class with a timeout |
| **InvokeCommand** | Agent: act | Drive a UIA element pattern (Invoke/Toggle/Value/SetFocus) |

> **MCEC 3.0 agent subsystem (`src/Agent/`, `Services/AgentServer.cs`).** The four agent
> commands above add *observation* and *targeting* on top of the existing actuation core, and
> are exposed as Model Context Protocol (MCP) tools over stdio (`mcec.exe --mcp`) and a
> localhost HTTP/JSON floor. They are **disabled by default** behind a dedicated opt-in
> (`AppSettings.AgentCommandsEnabled`, separate from the actuation enable), bind to localhost
> only, and loudly audit-log every action. The engine reaches settings/invoker through the
> UI-agnostic `AgentRuntime` seam so it runs headless (no `MainWindow`). See
> [`docs/agent-server.md`](../docs/agent-server.md) (users) and
> [`docs/agent-server-architecture.md`](../docs/agent-server-architecture.md) (devs).

**Nested Commands**:
Commands can contain embedded commands that execute after the parent completes. Example:
```xml
<StartProcess Cmd="notepad" File="notepad.exe">
    <Pause Args="100" />
    <Chars Args="Hello World" />
    <SendInput vk="VK_RETURN" />
</StartProcess>
```

### 4. Windows Input Simulation

**Location**: `WindowsInput\` namespace

**Purpose**: Low-level keyboard and mouse input simulation using Win32 SendInput API

**Architecture**:
```
InputSimulator (facade)
    ??? KeyboardSimulator
    ??? MouseSimulator
    ??? InputBuilder
            ??? Native Win32 Interop
                ??? INPUT structures
                ??? KEYBDINPUT
                ??? MOUSEINPUT
                ??? SendInput() API
```

**Key Components**:
- **InputBuilder**: Constructs INPUT structures for SendInput
- **KeyboardSimulator**: Virtual key press/release, modifier keys, text input
- **MouseSimulator**: Absolute/relative movement, button clicks, wheel scrolling
- **WindowsInputDeviceStateAdaptor**: Queries key/button states
- **WindowsInputMessageDispatcher**: Sends INPUT arrays to Win32 SendInput API

**Win32 Integration**:
- Unsafe code for P/Invoke
- Uses Windows.h structures (INPUT, KEYBDINPUT, MOUSEINPUT)
- Virtual key codes (VirtualKeyCode enum)
- Mouse and keyboard flags

### 5. Supporting Services

#### 5.1 AppSettings
**Location**: `Services\AppSettings.cs`

**Purpose**: Application configuration management

**Storage**: XML serialization to `mcec.settings`

**Features**:
- Server/Client/Serial configuration
- UI preferences (opacity, window position/size)
- Activity monitor settings
- Telemetry filtering via `[SafeForTelemetry]` attributes
- Adapts to Program Files vs user directory installation

#### 5.2 Logger
**Location**: `Helpers\Logger.cs`

**Purpose**: Centralized logging

**Features**:
- Log4net wrapper
- Dual output: file and UI TextBox
- Configurable log levels
- Exception dump formatting

#### 5.3 TelemetryService
**Location**: `Services\TelemetryService.cs`

**Purpose**: Anonymous usage analytics

**Features**:
- Azure Application Insights integration
- Opt-in via registry key
- PII protection (user-defined commands not tracked)
- Session tracking with anonymized user ID (SHA256 hash)
- Metrics: command usage, connection times, settings

**Privacy**:
- User ID: SHA256 hash of username+machine name
- Only built-in command names are tracked
- User-defined commands tracked as `<userDefined>`
- Settings filtered by `[SafeForTelemetry]` attribute

#### 5.4 UpdateService
**Location**: `Services\UpdateService.cs`

**Purpose**: Automatic update checking

**Features**:
- GitHub Releases API integration (Octokit library)
- Semantic version comparison
- Update notification dialog
- Debug builds check for pre-releases

#### 5.5 CommandFileWatcher
**Location**: `Helpers\CommandFileWatcher.cs`

**Purpose**: Hot-reload of command definitions

**Features**:
- Watches `mcec.commands` file
- Triggers CommandInvoker reload on file changes
- FileSystemWatcher wrapper with debouncing

### 6. Win32 Integration Layer

**Location**: `Win32\` namespace

**Purpose**: P/Invoke wrappers for Windows APIs

**Key Areas**:
- **Security**: Token manipulation, ACLs, SIDs (for process elevation/security)
- **Window Management**: FindWindow, PostMessage, SendMessage
- **Input**: SendInput, keybd_event, mouse_event
- **Process**: CreateProcess wrappers
- **Memory**: Marshaling utilities for unmanaged structures

### 7. Third-Party Libraries

**Location**: `Gma.UserActivityMonitor\` namespace

**Purpose**: Global input hooks for activity monitoring

**Features**:
- Low-level mouse and keyboard hooks (SetWindowsHookEx)
- Global event subscription
- Threaded message pump for hook processing

## Data Flow

### Incoming Command Flow

```
1. Network/Serial Input
   ?? SocketServer receives text
   ?? SocketClient receives text
   ?? SerialServer receives text
         ?
         ?
2. MainWindow.ReceivedData(Reply, String)
   - Ensures execution on UI thread
         ?
         ?
3. CommandInvoker.Enqueue(Reply, String)
   - Parses command string
   - Looks up Command in hashtable
   - Clones Command with Reply context
   - Enqueues to ConcurrentQueue
   - Recursively enqueues embedded commands
         ?
         ?
4. CommandInvoker.ExecuteNext()
   - Dequeues ICommand instances
   - Calls Command.Execute()
   - Applies command pacing (Thread.Sleep)
         ?
         ?
5. Command.Execute() Implementation
   - Checks Enabled flag
   - Tracks telemetry
   - Performs command-specific action
   - Uses Reply context for responses
         ?
         ?
6. Windows API / Process Execution
   - SendInput() for keyboard/mouse
   - PostMessage() for window messages
   - Process.Start() for applications
   - System shutdown APIs
```

### Activity Monitoring Flow

```
1. Gma.UserActivityMonitor (Global Hooks)
   - Mouse/keyboard events
   - Session change events
   - Power management events
         ?
         ?
2. UserActivityMonitorService
   - Debounces events
   - Generates activity command string
         ?
         ?
3. MainWindow.ReceivedData()
   - Routes to CommandInvoker
         ?
         ?
4. [Same as Incoming Command Flow from step 3]
```

## Configuration Files

| File | Format | Purpose |
|------|--------|---------|
| `mcec.settings` | XML | Application settings (serialized AppSettings) |
| `mcec.commands` | XML | User-defined and enabled built-in commands |
| `mcec.log` | Text | Log4net output |
| `version.txt` | Text | Build version (incremented by T4 template) |
| `app.manifest` | XML | Windows UAC and compatibility settings |

**File Locations**:
- **Development**: Same directory as executable
- **Production Install**: `%APPDATA%\Kindel\MCEC\` (if installed to Program Files; legacy installs used `Kindel Systems\MCEC` subfolder)

## Security Considerations

1. **Default Deny**: All built-in commands are disabled by default (`Enabled="false"`)
2. **Explicit Enable**: Users must edit `mcec.commands` to enable commands
3. **Registry Override**: `DisableInternalCommands` registry key can block all built-in commands
4. **Network Security**: No authentication on socket connections (assumes trusted network)
5. **Telemetry Privacy**: PII filtering via attributes, user-defined commands not tracked
6. **Process Elevation**: Uses Win32 security APIs for UAC/token manipulation when needed

## Threading Model

- **UI Thread**: MainWindow, all WinForms controls
- **Worker Threads**: Each SocketServer client connection, SocketClient connection
- **Command Execution**: Currently on UI thread (via BeginInvoke)
- **Activity Hooks**: WH_KEYBOARD_LL/WH_MOUSE_LL hooks (HookManager) install on the UI thread — there is no dedicated pump thread. Hook callbacks are enqueue-and-return (#198): only debounce/latch logic runs in the hook proc; heavy work (logging, telemetry, socket/serial sends) is posted off the callback path so the proc can never exceed `LowLevelHooksTimeout` (which would get the hook silently evicted)
- **Synchronization**: Thread-safe ConcurrentQueue for command execution

**Known Limitation**: Commands execute on UI thread which could block UI during long-running commands. Future enhancement would move CommandInvoker to dedicated thread.

## Build System

- **Target**: .NET 10 (net10.0-windows)
- **UI Framework**: Windows Forms
- **Project Type**: WinExe
- **T4 Templates**: 
  - `AssemblyFileVersion.tt` - Auto-increments build number in `version.txt`
  - `TelemetryService.tt` - Generates Application Insights key placeholder
- **Installer**: NSIS (Nullsoft Scriptable Install System)
- **Unit Tests**: xUnit framework in `MCEControl.xUnit` project

## Extensibility Points

1. **New Command Types**: Inherit from `Command`, implement `Execute()`, add to `Command.GetDerivedClassesCollection()`
2. **New Communication Services**: Inherit from `ServiceBase`, implement notification pattern
3. **Custom Input Simulation**: Extend `WindowsInput` namespace
4. **Plugin System**: None currently (all commands compiled in)

## Design Patterns Used

- **Singleton**: MainWindow, Logger, TelemetryService, UpdateService
- **Command Pattern**: ICommand, Command, CommandInvoker
- **Observer Pattern**: ServiceBase notifications via delegates
- **Factory Pattern**: CommandInvoker.Create(), Command.BuiltInCommands
- **Strategy Pattern**: Different Command implementations
- **Facade Pattern**: InputSimulator wraps KeyboardSimulator and MouseSimulator
- **Lazy Initialization**: Lazy<T> for singletons
- **Object Pool**: Command cloning for execution contexts

## Dependencies (NuGet)

| Package | Purpose |
|---------|---------|
| `log4net` | Logging infrastructure |
| `Microsoft.ApplicationInsights` | Telemetry collection |
| `Octokit` | GitHub API for update checking |
| `System.IO.Ports` | Serial port communication |
| `System.Text.Json` | JSON serialization |

## Future Architecture Considerations

1. **Async/Await**: Current model is largely synchronous; could benefit from async I/O
2. **Dependency Injection**: Hard-coded singletons could use DI container
3. **Command Thread**: Move command execution off UI thread
4. **Authentication**: Add optional security layer for network communication
5. **Plugin System**: Dynamic command loading from external assemblies
6. **Cross-Platform**: .NET 10 supports cross-platform, but WindowsInput and WinForms limit to Windows
