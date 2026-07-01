// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// Builds the condensed one-line label the command overlay (#119) shows for a command — the "MainWindow
/// log view, tersified" the issue asks for. Pure formatting so it is fully unit-testable; the overlay
/// renders the <see cref="CommandOutcome"/> separately (colour/emphasis).
/// </summary>
public static class CommandTersifier {
    /// <summary>
    /// Terse label for an agent tool call, e.g. <c>capture window="About"</c>, <c>invoke expand "Help"</c>,
    /// <c>wait-for "OK" → timeout</c>. <paramref name="detail"/> (e.g. an error category) is appended only
    /// on a <see cref="CommandOutcome.Failed"/> outcome.
    /// </summary>
    public static string ForAgentTool(string tool, JsonObject args, CommandOutcome outcome, string? detail = null) {
        string body = tool switch {
            "capture" => $"capture {Target(args)}",
            "query" => $"query {Target(args)}",
            "find" or "wait-for" => $"{tool} {Selector(args)}",
            "invoke" => $"invoke {Str(args, "action") ?? "invoke"} \"{Str(args, "value") ?? ""}\"",
            _ => tool,
        };
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
        string c = (command ?? string.Empty).Trim();
        if (c.Length > 40) {
            c = c[..40] + "…";
        }
        return $"send {c}";
    }

    /// <summary>
    /// The target descriptor the way <see cref="WindowResolver"/> actually resolves it: handle &gt;
    /// foreground &gt; window &gt; process &gt; className. Showing the filter text first would mislabel a
    /// call that reused a handle (or asked for foreground) with a stale title/process the resolver ignored.
    /// </summary>
    private static string Target(JsonObject args) {
        if (args["handle"] is JsonValue hv && hv.TryGetValue(out long handle) && handle > 0) {
            return $"handle=0x{handle:X}";
        }
        if (args["foreground"] is JsonValue fv && fv.TryGetValue(out bool fg) && fg) {
            return "foreground";
        }
        string? window = Str(args, "window");
        if (window is not null) {
            return $"window=\"{window}\"";
        }
        string? process = Str(args, "process");
        if (process is not null) {
            return $"process=\"{process}\"";
        }
        string? className = Str(args, "className");
        if (className is not null) {
            return $"class=\"{className}\"";
        }
        return "?";
    }

    /// <summary>The element selector for find/wait-for/invoke: a bare quoted value for <c>by=name</c>, else <c>by="value"</c>.</summary>
    private static string Selector(JsonObject args) {
        string value = Str(args, "value") ?? "";
        string by = Str(args, "by") ?? "name";
        return by.Equals("name", StringComparison.OrdinalIgnoreCase)
            ? $"\"{value}\""
            : $"{by}=\"{value}\"";
    }

    private static string? Str(JsonObject args, string key) =>
        args[key] is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrEmpty(s) ? s : null;
}
