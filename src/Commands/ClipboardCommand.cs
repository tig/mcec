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
/// often expose no settable filename field via UIA — set the clipboard, focus the filename field, then
/// send Ctrl+V. Gated by <see cref="AgentRuntime.AgentCommandsEnabled"/> and audited. Disabled by default.
/// </summary>
public class ClipboardCommand : Command {
    [XmlAttribute("action")] public string Action { get; set; } = null!;
    [XmlAttribute("text")] public string Text { get; set; } = null!;

    public static new List<Command> BuiltInCommands {
        get => [new ClipboardCommand { Cmd = "clipboard" }];
    }

    public override ICommand Clone(Reply reply) => base.Clone(reply, new ClipboardCommand {
        Action = this.Action,
        Text = this.Text,
    });

    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (!AgentRuntime.AgentCommandsEnabled) {
            Logger.Instance.Log4.Warn($"{GetType().Name}: BLOCKED — agent commands are disabled. Set AgentCommandsEnabled=true to opt in.");
            Reply?.WriteLine(CommandResult.Fail(Cmd, "Agent commands are disabled (AgentCommandsEnabled=false).").ToJson());
            return false;
        }

        string action = (Action ?? string.Empty).Trim().ToLowerInvariant();
        AgentRuntime.Audit(Cmd, $"clipboard action={action} textLen={Text?.Length ?? 0}");

        try {
            switch (action) {
                case "set":
                    if (string.IsNullOrEmpty(Text)) {
                        Reply?.WriteLine(CommandResult.Fail(Cmd, "clipboard set requires text.").ToJson());
                        return false;
                    }
                    RunSta(() => Clipboard.SetText(Text));
                    Reply?.WriteLine(CommandResult.Ok(Cmd, new JsonObject { ["set"] = true, ["length"] = Text.Length }).ToJson());
                    return true;

                case "get": {
                    string text = RunSta(() => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty);
                    Reply?.WriteLine(CommandResult.Ok(Cmd, new JsonObject { ["text"] = text, ["length"] = text.Length }).ToJson());
                    return true;
                }

                default:
                    Reply?.WriteLine(CommandResult.Fail(Cmd, $"Unknown clipboard action '{action}'. Use set or get.").ToJson());
                    return false;
            }
        }
        catch (Exception e) {
            Logger.Instance.Log4.Error($"{GetType().Name}: {e.Message}");
            Reply?.WriteLine(CommandResult.Fail(Cmd, e.Message).ToJson());
            return false;
        }
    }

    /// <summary>
    /// Clipboard is STA-only. Agent tools may run on the MCP HTTP thread (MTA), so hop to an STA thread
    /// when the caller is not already STA — same constraint as WinForms OLE clipboard APIs.
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