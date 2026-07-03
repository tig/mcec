// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MCEControl;

/// <summary>
/// Agent command: read or write the system text clipboard. System file dialogs (Open, Save Print Output As)
/// often expose no settable filename field via UIA; set the clipboard, focus the filename field, then
/// send Ctrl+V. Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited (structurally, via
/// <see cref="AgentCommand"/>). Disabled by default (security).
/// </summary>
public class ClipboardCommand : AgentCommand {
    [XmlAttribute("action")] public string Action { get; set; } = null!;
    [XmlAttribute("text")] public string Text { get; set; } = null!;

    public static List<Command> BuiltInCommands {
        get => [new ClipboardCommand { Cmd = "clipboard" }];
    }

    protected override string? AuditDetails() =>
        $"clipboard action={(Action ?? string.Empty).Trim().ToLowerInvariant()} textLen={Text?.Length ?? 0}";

    protected override CommandResult ExecuteCore() {
        string action = (Action ?? string.Empty).Trim().ToLowerInvariant();
        try {
            switch (action) {
                case "set":
                    if (string.IsNullOrEmpty(Text)) {
                        return CommandResult.Fail(Cmd, "clipboard set requires text.",
                            "clipboard-text-missing", "invalid-argument");
                    }
                    RunSta(() => Clipboard.SetText(Text));
                    return CommandResult.Ok(Cmd, new JsonObject { ["set"] = true, ["length"] = Text.Length });

                case "get": {
                    string text = RunSta(() => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty);
                    return CommandResult.Ok(Cmd, new JsonObject { ["text"] = text, ["length"] = text.Length });
                }

                default:
                    return CommandResult.Fail(Cmd, $"Unknown clipboard action '{action}'. Use set or get.",
                        "action-unknown", "invalid-argument");
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: {e.Message}");
            return CommandResult.Fail(Cmd, e.Message);
        }
    }

    /// <summary>
    /// Clipboard is STA-only. Agent tools may run on the MCP HTTP thread (MTA), so hop to an STA thread
    /// when the caller is not already STA; same constraint as WinForms OLE clipboard APIs.
    /// </summary>
    private static void RunSta(Action action) => RunSta<object?>(() => { action(); return null; });

    private static T RunSta<T>(Func<T> func) {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) {
            return func();
        }

        T result = default!;
        Exception? error = null;
        Thread thread = new(() => {
            try {
                result = func();
            }
            catch (Exception ex) {
                error = ex;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) {
            throw error;
        }
        return result;
    }
}
