// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// The single registry of the gated agent tools (#205): one <see cref="ToolDescriptor"/> per tool;
/// capture, query, displays, find, wait-for, invoke, drag, click, record, launch; carrying its
/// <c>tools/list</c> schema, its argument→<see cref="Command"/> mapping, its overlay tersifier, and its
/// policy flags. Every consumer (the <see cref="AgentServer"/> gate/dispatch/schema sites,
/// <see cref="AgentSession"/>, <see cref="CommandTersifier"/>, <see cref="SessionProvisioner"/>) looks
/// tools up here, so adding a tool is one descriptor + its command class, not shotgun surgery.
///
/// <para>The META-TOOLS; <c>send_command</c>, <c>provision-session</c>, <c>end-session</c>; are
/// deliberately NOT in the catalog: they do not map 1:1 onto a <see cref="Command"/> in the loaded
/// table (raw pass-through / session lifecycle), have their own transport-sensitive gating (#153,
/// #138), and are special-cased in <see cref="AgentServer"/> right next to the catalog dispatch.</para>
///
/// <para>Lookups are case-SENSITIVE (ordinal), preserving the historical <c>name is "capture" or …</c>
/// gate and switch behavior; <see cref="SessionProvisioner"/> lower-cases requested names first, as it
/// always did.</para>
/// </summary>
public static class ToolCatalog {
    /// <summary>Every gated agent tool, in the order <c>tools/list</c> advertises them.</summary>
    public static IReadOnlyList<ToolDescriptor> All { get; } = BuildCatalog();

    private static readonly Dictionary<string, ToolDescriptor> _byName = BuildIndex();

    /// <summary>Whether <paramref name="name"/> is a gated agent tool (the tools/call gate membership test).</summary>
    public static bool Contains(string name) => _byName.ContainsKey(name);

    /// <summary>Looks up the descriptor for <paramref name="name"/> (ordinal match).</summary>
    public static bool TryGet(string name, out ToolDescriptor descriptor) {
        if (_byName.TryGetValue(name, out ToolDescriptor? found)) {
            descriptor = found;
            return true;
        }
        descriptor = null!;
        return false;
    }

    private static Dictionary<string, ToolDescriptor> BuildIndex() {
        Dictionary<string, ToolDescriptor> index = new(StringComparer.Ordinal);
        foreach (ToolDescriptor d in All) {
            index.Add(d.Name, d);
        }
        return index;
    }

    private static IReadOnlyList<ToolDescriptor> BuildCatalog() => [
        new() {
            Name = "capture",
            BuildSchema = BuildCaptureSchema,
            BuildCommand = args => new CaptureCommand {
                Window = Str(args, "window")!,
                Handle = Long(args, "handle"),
                Process = Str(args, "process")!,
                ClassName = Str(args, "className")!,
                Foreground = Bool(args, "foreground"),
                X = Int(args, "x"),
                Y = Int(args, "y"),
                Width = Int(args, "width"),
                Height = Int(args, "height"),
                File = Str(args, "file")!,
            },
            CreateCommandInstance = () => new CaptureCommand(),
            Tersify = args => $"capture {CommandTersifier.Target(args)}",
            IsObservation = true,
            ProvisionedByDefault = true,
        },
        new() {
            Name = "query",
            BuildSchema = BuildQuerySchema,
            BuildCommand = args => {
                int maxDepth = Int(args, "maxDepth");
                int maxNodes = Int(args, "maxNodes");
                return new QueryCommand {
                    Window = Str(args, "window")!,
                    Handle = Long(args, "handle"),
                    Process = Str(args, "process")!,
                    ClassName = Str(args, "className")!,
                    Foreground = Bool(args, "foreground"),
                    MaxDepth = maxDepth > 0 ? maxDepth : 6,
                    MaxNodes = maxNodes > 0 ? maxNodes : 1000,
                };
            },
            CreateCommandInstance = () => new QueryCommand(),
            Tersify = args => $"query {CommandTersifier.Target(args)}",
            IsObservation = true,
            ProvisionedByDefault = true,
        },
        new() {
            Name = "displays",
            BuildSchema = BuildDisplaysSchema,
            BuildCommand = _ => new DisplaysCommand(),
            CreateCommandInstance = () => new DisplaysCommand(),
            Tersify = _ => "displays",
            ProvisionedByDefault = true,
        },
        new() {
            Name = "windows",
            BuildSchema = BuildWindowsSchema,
            BuildCommand = args => new WindowsCommand {
                Window = Str(args, "window")!,
                Process = Str(args, "process")!,
                ClassName = Str(args, "className")!,
                Timeout = Int(args, "timeout"),
            },
            CreateCommandInstance = () => new WindowsCommand(),
            Tersify = args => $"windows {CommandTersifier.WindowFilter(args)}",
            // NOT IsObservation: it returns a SET of windows, not a single active target the session
            // should record as LastObservation (that stays query/capture/find/wait-for).
            ProvisionedByDefault = true,
        },
        new() {
            Name = "find",
            BuildSchema = BuildFindSchema,
            BuildCommand = BuildFindCommand,
            CreateCommandInstance = () => new FindCommand(),
            Tersify = args => $"find {CommandTersifier.Selector(args)}",
            IsObservation = true,
            ProvisionedByDefault = true,
        },
        new() {
            Name = "wait-for",
            BuildSchema = BuildWaitForSchema,
            BuildCommand = BuildFindCommand,
            CreateCommandInstance = () => new FindCommand(),
            Tersify = args => $"wait-for {CommandTersifier.Selector(args)}",
            IsObservation = true,
            ProvisionedByDefault = true,
        },
        new() {
            Name = "invoke",
            BuildSchema = BuildInvokeSchema,
            BuildCommand = args => new InvokeCommand {
                Window = Str(args, "window")!,
                Handle = Long(args, "handle"),
                Process = Str(args, "process")!,
                ClassName = Str(args, "className")!,
                Foreground = Bool(args, "foreground"),
                By = Str(args, "by") ?? "name",
                Value = Str(args, "value")!,
                Action = Str(args, "action") ?? "invoke",
                Text = Str(args, "text")!,
            },
            CreateCommandInstance = () => new InvokeCommand(),
            Tersify = args => $"invoke {CommandTersifier.Arg(args, "action") ?? "invoke"} \"{CommandTersifier.Arg(args, "value") ?? ""}\"",
            ProvisionedByDefault = true,
        },
        new() {
            Name = "drag",
            BuildSchema = BuildDragSchema,
            BuildCommand = BuildDragCommand,
            CreateCommandInstance = () => new DragCommand(),
            Tersify = args => $"drag {CommandTersifier.Endpoint(args, "from")} → {CommandTersifier.Endpoint(args, "to")}",
            SerializesOnInput = true,
            ProvisionedByDefault = true,
        },
        new() {
            Name = "click",
            BuildSchema = BuildClickSchema,
            BuildCommand = BuildClickCommand,
            CreateCommandInstance = () => new ClickCommand(),
            Tersify = args => $"click {CommandTersifier.Endpoint(args, "at")}",
            ProvisionedByDefault = true,
        },
        new() {
            Name = "focus",
            BuildSchema = BuildFocusSchema,
            BuildCommand = BuildFocusCommand,
            CreateCommandInstance = () => new FocusCommand(),
            Tersify = args => $"focus {CommandTersifier.Endpoint(args, "at")}",
            // Like drag, focus synthesizes a real click as part of a compound gesture (foreground →
            // click → SetFocus → verify); serialize it on the input gate so that gesture cannot
            // interleave with queue-driven input (send_command/legacy pipeline) mid-sequence (#113).
            SerializesOnInput = true,
            ProvisionedByDefault = true,
        },
        new ToolDescriptor {
            Name = "clipboard",
            BuildSchema = BuildClipboardSchema,
            BuildCommand = args => new ClipboardCommand {
                Action = Str(args, "action")!,
                Text = Str(args, "text")!,
            },
            CreateCommandInstance = () => new ClipboardCommand(),
            Tersify = args => $"clipboard {CommandTersifier.Arg(args, "action") ?? "?"}",
            ProvisionedByDefault = true,
        },
        new() {
            Name = "record",
            BuildSchema = BuildRecordSchema,
            BuildCommand = args => new RecordCommand {
                Action = Str(args, "action")!,
                Window = Str(args, "window")!,
                Handle = Long(args, "handle"),
                Process = Str(args, "process")!,
                ClassName = Str(args, "className")!,
                Foreground = Bool(args, "foreground"),
                X = Int(args, "x"),
                Y = Int(args, "y"),
                Width = Int(args, "width"),
                Height = Int(args, "height"),
                Fps = Int(args, "fps"),
                DurationMs = Int(args, "durationMs"),
                MaxWidth = Int(args, "maxWidth"),
                File = Str(args, "file")!,
            },
            CreateCommandInstance = () => new RecordCommand(),
            // Mirrors capture: record shares its window-or-region targeting semantics. (#205 fixed the
            // drift where record rendered as a bare "record" on the overlay.)
            Tersify = args => $"record {CommandTersifier.Target(args)}",
            ProvisionedByDefault = true,
        },
        new() {
            Name = "launch",
            BuildSchema = BuildLaunchSchema,
            BuildCommand = args => new LaunchCommand {
                Path = Str(args, "path")!,
                Arguments = Str(args, "arguments")!,
                WorkingDirectory = Str(args, "workingDirectory")!,
                Timeout = Int(args, "timeout"),
            },
            CreateCommandInstance = () => new LaunchCommand(),
            // (#205 fixed the drift where launch rendered as a bare "launch" on the overlay.)
            Tersify = args => $"launch \"{CommandTersifier.Arg(args, "path") ?? ""}\"",
            // Not in SessionProvisioner's command set: a provisioned session's default posture is
            // observe/act-on-UI; direct process launch stays an explicit operator decision.
            ProvisionedByDefault = false,
        },
    ];

    // -------------------------------------------------------------------------------------------
    // tools/list schemas (relocated verbatim from AgentServer.BuildToolsList, #205; content unchanged)
    // -------------------------------------------------------------------------------------------

    private static JsonObject BuildCaptureSchema() {
        JsonObject captureProps = WindowTargetProps();
        captureProps["x"] = PropSchema("integer", "Region left (use with width/height instead of a window)");
        captureProps["y"] = PropSchema("integer", "Region top");
        captureProps["width"] = PropSchema("integer", "Region width (max 16384/side, 64000000 px total; oversized fails with region-too-large)");
        captureProps["height"] = PropSchema("integer", "Region height (same limits as width)");
        captureProps["file"] = PropSchema("string", "Optional path to also save the PNG to");
        return Tool("capture",
            "Screenshot a window (PrintWindow PW_RENDERFULLCONTENT, captures WinUI/WPF surfaces) or a screen region; returns PNG. Blank/black frames are detected and reported as a capture-blank error (window) or warning (region) rather than a silent bad image.",
            captureProps, []);
    }

    private static JsonObject BuildQuerySchema() {
        JsonObject queryProps = WindowTargetProps();
        queryProps["maxDepth"] = PropSchema("integer", "Max UI Automation tree depth (default 6)");
        queryProps["maxNodes"] = PropSchema("integer", "Max UI Automation nodes returned (default 1000); a clipped tree is flagged with a tree-truncated warning");
        return Tool("query",
            "Dump the UI Automation tree of a window: control type, name, automation id, bounds, state. Returns nodeCount/truncated and warns when the node cap clips the tree.",
            queryProps, []);
    }

    private static JsonObject BuildDisplaysSchema() => Tool("displays",
        "Report display geometry: every monitor's pixel bounds, working area, primary flag, and DPI/scale, plus the union virtualBounds. Use it to interpret the absolute-pixel bounds query/find return and to place pixel clicks/drags; no arguments.",
        [], []);

    private static JsonObject BuildWindowsSchema() {
        JsonObject props = new() {
            ["window"] = PropSchema("string", "Filter by window title substring (case-insensitive)"),
            ["process"] = PropSchema("string", "Filter by process name (without .exe)"),
            ["className"] = PropSchema("string", "Filter by window class name (exact)"),
            ["timeout"] = PropSchema("integer", "Milliseconds to WAIT for a matching top-level window to appear (0 = list now, no wait); requires at least one filter"),
        };
        return Tool("windows",
            "Discover top-level windows: returns each window's handle, title, className, processName, processId, and bounds so you can find and target a window instead of guessing. Optionally filter by title substring / process / class. With a timeout it WAITS, polling until a matching window appears (or the timeout, returning count:0). No filter lists every window; a timeout with no filter is refused (it won't wait for an arbitrary window). Reuse a returned handle directly on query/capture/invoke.",
            props, []);
    }

    private static JsonObject BuildFindSchema() {
        JsonObject findProps = WindowTargetProps();
        findProps["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)");
        findProps["value"] = PropSchema("string", "Value to match");
        findProps["timeout"] = PropSchema("integer", "Milliseconds to wait for the element (0 = no wait)");
        return Tool("find",
            "Find (or wait for, with a timeout) a UI Automation element by name / automation id / class.",
            findProps, ["value"]);
    }

    private static JsonObject BuildWaitForSchema() {
        JsonObject waitForProps = WindowTargetProps();
        waitForProps["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)");
        waitForProps["value"] = PropSchema("string", "Value to match");
        waitForProps["timeout"] = PropSchema("integer", "Milliseconds to wait for the element (default 5000)");
        return Tool("wait-for",
            "Wait for a UI Automation element to appear: polls until found or the timeout elapses (default 5s).",
            waitForProps, ["value"]);
    }

    private static JsonObject BuildInvokeSchema() {
        JsonObject invokeProps = WindowTargetProps();
        invokeProps["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)");
        invokeProps["value"] = PropSchema("string", "Value to match");
        invokeProps["action"] = PropSchema("string", "invoke | toggle | setvalue | setfocus | expand | collapse | select (default invoke). Use expand to open a menu before invoking its items; use select for TabItem, ListItem, RadioButton etc.");
        invokeProps["text"] = PropSchema("string", "Text for the setvalue action");
        return Tool("invoke",
            "Drive a UI Automation element (Invoke/Toggle/Value/SetFocus/Expand/Collapse/Select); more reliable than coordinate clicks. Use 'select' for tabs, list items, radios (SelectionItem pattern).",
            invokeProps, ["value"]);
    }

    private static JsonObject BuildDragSchema() {
        JsonObject dragProps = WindowTargetProps();
        dragProps["from"] = EndpointSchema("Drag start: an element ({ by, value }) in the target window, or a pixel ({ x, y }).");
        dragProps["to"] = EndpointSchema("Drag end: an element ({ by, value }) in the target window, or a pixel ({ x, y }).");
        dragProps["path"] = new JsonObject {
            ["type"] = "array",
            ["description"] = "Optional intermediate waypoints (absolute screen pixels) between from and to.",
            ["items"] = new JsonObject {
                ["type"] = "object",
                ["properties"] = new JsonObject {
                    ["x"] = PropSchema("integer", "Waypoint X (screen pixels)"),
                    ["y"] = PropSchema("integer", "Waypoint Y (screen pixels)"),
                },
            },
        };
        return Tool("drag",
            "Press → move along a path → release, dispatched atomically (no interleaving). Endpoints are an element (by/value, dragged from/to its centre) or an absolute screen pixel; add path waypoints for a curved/multi-stop drag. Covers window resize/move by chrome, sliders, marquee select, drag-reorder. Give a window target when either endpoint is an element.",
            dragProps, ["from", "to"]);
    }

    private static JsonObject BuildClickSchema() {
        JsonObject clickProps = WindowTargetProps();
        clickProps["at"] = EndpointSchema("Where to click: an element ({ by, value }) in the target window (its centre) or an absolute screen pixel ({ x, y }).");
        clickProps["button"] = PropSchema("string", "Button: left | right | middle (default left)");
        clickProps["count"] = PropSchema("integer", "Click count: 1 = single, 2 = double (default 1)");
        return Tool("click",
            "Click at a point; an element (by/value, clicked at its centre) or an absolute screen pixel (the space query/find bounds report). Move+click is dispatched atomically. Prefer invoke for buttons/menus; use click for element types invoke cannot drive or when you must target a pixel. Give a window target when 'at' is an element.",
            clickProps, ["at"]);
    }

    private static JsonObject BuildFocusSchema() {
        JsonObject focusProps = WindowTargetProps();
        focusProps["at"] = EndpointSchema("Optional control to focus: an element ({ by, value }) in the target window (clicked at its centre) or an absolute screen pixel ({ x, y }). Omit to just bring the window to the foreground and confirm keyboard focus is in it.");
        return Tool("focus",
            "Give a window (and optionally a control in it) real keyboard focus so send_command/chars keystrokes reach it. Foregrounds the window, clicks the control (a real click focuses surfaces a bare UIA setfocus misses; e.g. a MAUI GraphicsView), then verifies. Fails with category 'foreground' if the window won't activate, 'focus' if no control took focus. Use before sending app keyboard shortcuts to a custom-drawn surface.",
            focusProps, []);
    }

    private static JsonObject BuildClipboardSchema() {
        JsonObject clipboardProps = new() {
            ["action"] = PropSchema("string", "set | get"),
            ["text"] = PropSchema("string", "Text to set (required for action=set)"),
        };
        return Tool("clipboard",
            "Read or write the system text clipboard. Use set before pasting a path into a system file dialog filename field (Ctrl+V).",
            clipboardProps, ["action"]);
    }

    private static JsonObject BuildRecordSchema() {
        JsonObject recordProps = WindowTargetProps();
        recordProps["x"] = PropSchema("integer", "Region left (use with width/height instead of a window)");
        recordProps["y"] = PropSchema("integer", "Region top");
        recordProps["width"] = PropSchema("integer", "Region width (max 16384/side, 64000000 px total; oversized fails with region-too-large)");
        recordProps["height"] = PropSchema("integer", "Region height (same limits as width)");
        recordProps["action"] = PropSchema("string", "start | stop | oneshot (default: oneshot if durationMs given, else start)");
        recordProps["fps"] = PropSchema("integer", "Frames per second (default 5, clamped to the operator limit)");
        recordProps["durationMs"] = PropSchema("integer", "For a one-shot: how long to record (clamped to the operator limit)");
        recordProps["maxWidth"] = PropSchema("integer", "Downscale frames so width fits this (default 1280)");
        recordProps["file"] = PropSchema("string", "Output .gif path (a temp path is generated if omitted)");
        return Tool("record",
            "Record a window or region to an animated GIF over time (start/stop or a bounded one-shot). Use only to show CHANGE over time; for a single state check use capture.",
            recordProps, []);
    }

    private static JsonObject BuildLaunchSchema() {
        JsonObject launchProps = new() {
            ["path"] = PropSchema("string", "Path to executable, shell: protocol target (e.g. shell:AppsFolder\\...), or .lnk (required)"),
            ["arguments"] = PropSchema("string", "Command line arguments to pass to the process"),
            ["workingDirectory"] = PropSchema("string", "Initial working directory for the launched process"),
            ["timeout"] = PropSchema("integer", "Milliseconds to wait for the app window to appear (default 5000)"),
        };
        return Tool("launch",
            "Launch an application directly as a gated agent action. Starts the process and returns its pid plus the primary window (handle + descriptor) when it appears within timeout. Preferred over send_command winr dance for reliability.",
            launchProps, ["path"]);
    }

    // -------------------------------------------------------------------------------------------
    // Schema building blocks (shared with AgentServer's meta-tool schemas)
    // -------------------------------------------------------------------------------------------

    /// <summary>The five shared window-targeting selector properties.</summary>
    private static JsonObject WindowTargetProps() => new() {
        ["window"] = PropSchema("string", "Window title substring (case-insensitive) to target"),
        ["handle"] = PropSchema("integer", "Explicit window handle (HWND) to target"),
        ["process"] = PropSchema("string", "Process name (without .exe) to target"),
        ["className"] = PropSchema("string", "Window class name to target"),
        ["foreground"] = PropSchema("boolean", "Target the current foreground window"),
    };

    /// <summary>A single JSON-schema property node.</summary>
    internal static JsonObject PropSchema(string type, string description) =>
        new() { ["type"] = type, ["description"] = description };

    /// <summary>
    /// The optional per-call session-routing argument (#86 Phase 3). Every observation/actuation tool and
    /// <c>send_command</c> accepts it: echo the <c>sessionId</c> that <c>session-start</c> returned to run
    /// the call in that session, or omit it to use the implicit default session. Advertised on those tools'
    /// schemas (injected in <see cref="JsonRpcDispatcher"/>) so a schema-validating client keeps it. The
    /// session-lifecycle tools read <c>sessionId</c> as a TARGET, not routing, and carry their own copy.
    /// </summary>
    internal static JsonObject SessionArgProp() =>
        PropSchema("string", "Optional session id (from session-start) to run this call in; omit to use the default session (#86).");

    /// <summary>Schema for a drag/click endpoint: either an element ({ by, value }) or a pixel ({ x, y }).</summary>
    private static JsonObject EndpointSchema(string description) => new() {
        ["type"] = "object",
        ["description"] = description,
        ["properties"] = new JsonObject {
            ["by"] = PropSchema("string", "Match by: name | automationid | classname (default name)"),
            ["value"] = PropSchema("string", "Element value to match (omit for a pixel endpoint)"),
            ["x"] = PropSchema("integer", "Endpoint X in absolute screen pixels (omit for an element endpoint)"),
            ["y"] = PropSchema("integer", "Endpoint Y in absolute screen pixels (omit for an element endpoint)"),
        },
    };

    /// <summary>Assembles a complete <c>tools/list</c> entry.</summary>
    internal static JsonObject Tool(string name, string description, JsonObject properties, JsonArray required) => new() {
        ["name"] = name,
        ["description"] = description,
        ["inputSchema"] = new JsonObject {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
        },
    };

    // -------------------------------------------------------------------------------------------
    // Argument mapping helpers (relocated from AgentServer, #205; behavior unchanged)
    // -------------------------------------------------------------------------------------------

    /// <summary>Maps find/wait-for arguments onto a <see cref="FindCommand"/> (shared by both tool names).</summary>
    private static FindCommand BuildFindCommand(JsonObject args) => new() {
        Window = Str(args, "window")!,
        Handle = Long(args, "handle"),
        Process = Str(args, "process")!,
        ClassName = Str(args, "className")!,
        Foreground = Bool(args, "foreground"),
        By = Str(args, "by") ?? "name",
        Value = Str(args, "value")!,
        Timeout = Int(args, "timeout"),
    };

    /// <summary>Maps the drag tool's nested <c>from</c>/<c>to</c>/<c>path</c> arguments onto a <see cref="DragCommand"/>.</summary>
    private static DragCommand BuildDragCommand(JsonObject args) {
        JsonObject from = args["from"] as JsonObject ?? [];
        JsonObject to = args["to"] as JsonObject ?? [];
        return new DragCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            FromBy = Str(from, "by") ?? "name",
            FromValue = Str(from, "value")!,
            FromX = Int(from, "x"),
            FromY = Int(from, "y"),
            ToBy = Str(to, "by") ?? "name",
            ToValue = Str(to, "value")!,
            ToX = Int(to, "x"),
            ToY = Int(to, "y"),
            PathSpec = BuildPathSpec(args["path"] as JsonArray),
        };
    }

    /// <summary>Maps the click tool's nested <c>at</c> endpoint and button/count onto a <see cref="ClickCommand"/>.</summary>
    private static ClickCommand BuildClickCommand(JsonObject args) {
        JsonObject at = args["at"] as JsonObject ?? [];
        int count = Int(args, "count");
        return new ClickCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            By = Str(at, "by") ?? "name",
            Value = Str(at, "value")!,
            X = Int(at, "x"),
            Y = Int(at, "y"),
            Button = Str(args, "button") ?? "left",
            Count = count > 0 ? count : 1,
        };
    }

    /// <summary>Maps the focus tool's optional <c>at</c> endpoint onto a <see cref="FocusCommand"/>. An
    /// endpoint with a pixel (<c>x</c>/<c>y</c> present) and no <c>value</c> sets <c>PointSpecified</c> so a
    /// literal (0,0) is distinguished from "no endpoint" (a bare window focus).</summary>
    private static FocusCommand BuildFocusCommand(JsonObject args) {
        JsonObject at = args["at"] as JsonObject ?? [];
        bool hasElement = !string.IsNullOrEmpty(Str(at, "value"));
        bool hasPixel = !hasElement && (at.ContainsKey("x") || at.ContainsKey("y"));
        return new FocusCommand {
            Window = Str(args, "window")!,
            Handle = Long(args, "handle"),
            Process = Str(args, "process")!,
            ClassName = Str(args, "className")!,
            Foreground = Bool(args, "foreground"),
            By = Str(at, "by") ?? "name",
            Value = Str(at, "value")!,
            X = Int(at, "x"),
            Y = Int(at, "y"),
            PointSpecified = hasPixel,
        };
    }

    /// <summary>Flattens the drag tool's <c>path</c> array of <c>{ x, y }</c> points into DragCommand's <c>x,y;x,y</c> spec.</summary>
    private static string BuildPathSpec(JsonArray? path) {
        if (path is null || path.Count == 0) {
            return string.Empty;
        }
        List<string> pairs = [];
        foreach (JsonNode? node in path) {
            if (node is JsonObject p) {
                pairs.Add($"{Int(p, "x")},{Int(p, "y")}");
            }
        }
        return string.Join(";", pairs);
    }

    private static string? Str(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static long Long(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out long l) ? l : 0;

    private static int Int(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out int i) ? i : 0;

    private static bool Bool(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out bool b) && b;
}
