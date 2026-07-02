//-------------------------------------------------------------------
// Copyright © 2017 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MCEControl; 
/// <summary>
/// Commands that control MCE Controller, or get information about it
/// </summary>
public class McecCommand : Command {
    public const string CmdPrefix = "mcec:";
    public static List<Command> BuiltInCommands {
        get => [
        new McecCommand { Cmd = $"{CmdPrefix}" },   // Commands that use form of "cmd:" must define a blank version
        new McecCommand { Cmd = $"{CmdPrefix }ver" },
        new McecCommand { Cmd = $"{CmdPrefix }exit" },
        new McecCommand { Cmd = $"{CmdPrefix }cmds" },
        new McecCommand { Cmd = $"{CmdPrefix }time" }
        ];
    }

    public McecCommand() {
    }

    /// <summary>
    /// mcec: commands report state or exit the app; they never synthesize input, so the dispatcher
    /// (#195) need not hold <see cref="AgentRuntime.InputGate"/> while they run.
    /// </summary>
    internal override bool SynthesizesInput => false;

    public override string ToString() {
        return $"Cmd=\"{Cmd}\"";
    }

    // ICommand:Execute
    public override bool Execute() {
        if (!base.Execute()) {
            return false;
        }

        if (this.Reply is null) {
            throw new InvalidOperationException("Reply property cannot be null.");
        }

        if (this.Args is null) {
            throw new InvalidOperationException("Args property cannot be null.");
        }

        StringBuilder replyBuilder = new StringBuilder();
        switch (Args.ToLowerInvariant()) {
            // MCE Controller version
            case "ver":
                replyBuilder.Append(Application.ProductVersion);
                break;

            // Cause MCE Controller to exit
            case "exit":
                Reply.WriteLine("exiting");
                // #209: shutdown goes through the UI-agnostic host seam. GUI: MainWindow.ShutDown()
                // (self-marshals, so calling from this dispatcher thread is safe; #195). Headless
                // (--mcp): HeadlessAppHost schedules a clean deferred process exit so the in-flight
                // send_command reply reaches the client first; mcec:exit now actually exits
                // headless instead of the old #195 decline.
                AgentRuntime.RequestShutdown();
                return true;

            // Return a list of supported commands (really just for testing)
            case "cmds":
                Command cmd = this;
                Match? match = null;
                // #195: read the command table via the UI-agnostic AgentRuntime seam (populated by
                // both the GUI and headless hosts) instead of MainWindow.Instance; this runs on the
                // dispatcher thread and must not touch (or lazily construct) the Form.
                if (AgentRuntime.Invoker is not CommandInvoker invoker) {
                    Logger.Instance.Log4.Error($"{this.GetType().Name}: ({Cmd}:{Args}) command table is not available");
                    return false;
                }
                try {
                    replyBuilder.Append(Environment.NewLine);
                    IOrderedEnumerable<string> orderedKeys = invoker.Keys.Cast<string>().OrderBy(c => c);
                    foreach (string key in orderedKeys) {
                        cmd = (Command)invoker[key]!;
                        match = Regex.Match(cmd.GetType().ToString(), @"MCEControl\.([A-za-z]+)Command");
                        replyBuilder.Append($"<{match.Groups[1].Value} {cmd.ToString()} />{Environment.NewLine}");
                    }
                }
                catch (Exception e) {
                    Logger.Instance.Log4.Error($"{this.GetType().Name}: ({Cmd}:{Args}) <{match!.Groups[1].Value} {cmd.ToString()}/> - {e.Message}");
                    return false;
                }
                break;

            // Return the current date/time of the PC
            case "time":
                DateTime dt = DateTime.Now;
                replyBuilder.AppendFormat("{0}", DateTime.Now);
                break;
        }

        // Reply.  
        replyBuilder.Insert(0, $"{Args}=");
        Logger.Instance.Log4.Info($"{this.GetType().Name}: Sending reply: {replyBuilder.ToString()}");
        Reply.WriteLine(replyBuilder.ToString());
        return true;
    }
}
