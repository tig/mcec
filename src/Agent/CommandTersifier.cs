// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// Builds the condensed one-line label the command overlay (#119) shows for a command; the "MainWindow
/// log view, tersified" the issue asks for. Pure formatting so it is fully unit-testable; the overlay
/// renders the <see cref="CommandOutcome"/> separately (colour/emphasis).
/// </summary>
public static class CommandTersifier {
    /// <summary>
    /// Terse label for an agent tool call, e.g. <c>capture window="About"</c>, <c>invoke expand "Help"</c>,
    /// <c>wait-for "OK" → timeout</c>. The per-tool body comes from the tool's
    /// <see cref="ToolDescriptor.Tersify"/> formatter in <see cref="ToolCatalog"/> (#205; one registry,
    /// so a new tool cannot silently render as its bare name); an unknown name falls back to the name.
    /// <paramref name="detail"/> (e.g. an error category) is appended only on a
    /// <see cref="CommandOutcome.Failed"/> outcome.
    /// </summary>
    public static string ForAgentTool(string tool, JsonObject args, CommandOutcome outcome, string? detail = null) {
        string body = ToolCatalog.TryGet(tool, out ToolDescriptor descriptor) ? descriptor.Tersify(args) : tool;
        if (outcome == CommandOutcome.Pending) {
            body += " …";
        }
        else if (outcome == CommandOutcome.Failed) {
            body += detail is not null ? $" → {detail}" : " → failed";
        }
        return body;
    }

    /// <summary>Terse label for a raw <c>send_command</c> pass-through, e.g. <c>send winr</c> (long commands clipped).</summary>
    public static string ForRawCommand(string command) {
        string c = command.Trim();
        if (c.Length > 40) {
            c = c[..40] + "…";
        }
        return $"send {c}";
    }

    /// <summary>
    /// The target descriptor the way <see cref="WindowResolver"/> actually resolves it: handle &gt;
    /// foreground &gt; window &gt; process &gt; className. Showing the filter text first would mislabel a
    /// call that reused a handle (or asked for foreground) with a stale title/process the resolver ignored.
    /// Internal: the per-tool formatters in <see cref="ToolCatalog"/> compose from these building blocks.
    /// </summary>
    internal static string Target(JsonObject args) {
        if (args["handle"] is JsonValue hv && hv.TryGetValue(out long handle) && handle > 0) {
            return $"handle=0x{handle:X}";
        }
        if (args["foreground"] is JsonValue fv && fv.TryGetValue(out bool fg) && fg) {
            return "foreground";
        }
        string? window = Arg(args, "window");
        if (window is not null) {
            return $"window=\"{window}\"";
        }
        string? process = Arg(args, "process");
        if (process is not null) {
            return $"process=\"{process}\"";
        }
        string? className = Arg(args, "className");
        if (className is not null) {
            return $"class=\"{className}\"";
        }
        return "?";
    }

    /// <summary>
    /// The discovery filter label for the <c>windows</c> tool: the first non-empty of window/process/
    /// className, or <c>all</c> when no filter is given (a bare list of every window).
    /// </summary>
    internal static string WindowFilter(JsonObject args) {
        string? window = Arg(args, "window");
        if (window is not null) {
            return $"window=\"{window}\"";
        }
        string? process = Arg(args, "process");
        if (process is not null) {
            return $"process=\"{process}\"";
        }
        string? className = Arg(args, "className");
        if (className is not null) {
            return $"class=\"{className}\"";
        }
        return "all";
    }

    /// <summary>The element selector for find/wait-for/invoke: a bare quoted value for <c>by=name</c>, else <c>by="value"</c>.</summary>
    internal static string Selector(JsonObject args) {
        string value = Arg(args, "value") ?? "";
        string by = Arg(args, "by") ?? "name";
        return by.Equals("name", StringComparison.OrdinalIgnoreCase)
            ? $"\"{value}\""
            : $"{by}=\"{value}\"";
    }

    /// <summary>A drag/click endpoint label: an element value (quoted) if present, else a pixel pair.</summary>
    internal static string Endpoint(JsonObject args, string key) {
        if (args[key] is not JsonObject ep) {
            return "?";
        }
        string? value = Arg(ep, key: "value");
        if (value is not null) {
            string by = Arg(ep, "by") ?? "name";
            return by.Equals("name", StringComparison.OrdinalIgnoreCase) ? $"\"{value}\"" : $"{by}=\"{value}\"";
        }
        return $"{Int(ep, "x")},{Int(ep, "y")}";
    }

    /// <summary>A string argument for display: null when absent OR empty (an empty value renders as a default, not "").</summary>
    internal static string? Arg(JsonObject args, string key) =>
        args[key] is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrEmpty(s) ? s : null;

    private static int Int(JsonObject args, string key) =>
        args[key] is JsonValue v && v.TryGetValue(out int i) ? i : 0;
}
