//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec  
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;

namespace MCEControl {
    /// <summary>
    /// Summary description for StartProcessCommands.
    /// </summary>
    public class StartProcessCommand : Command {
        private String file;
        [XmlAttribute("file")] public string File { get => file; set => file = value; }
        private String arguments;
        [XmlAttribute("arguments")] public string Arguments { get => arguments; set => arguments = value; }
        private String verb;
        [XmlAttribute("verb")] public string Verb { get => verb; set => verb = value; }

        public static new List<Command> BuiltInCommands {
            get => new List<Command>() {
                  new StartProcessCommand() { Cmd = "code", File ="code" },
                  new StartProcessCommand() { Cmd = "tada", File=@"C:\Windows\Media\tada.wav", Verb="Open"  },
                  new StartProcessCommand() { Cmd = "term", File=@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App" },
                  new StartProcessCommand() { Cmd = "netflix", File=@"shell:AppsFolder\4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.App"  },

                  // Exmaple showing nested commands taken from documentation.md:
                  //<StartProcess Cmd = "notepad" File="notepad.exe" >
                  //    <Pause Args = "100" />
                  //    <Chars Cmd="test" Args="this is a test." />
                  //    <SendInput vk = "VK_RETURN" />
                  //        < Pause Args="100"/>
                  //    <SendInput vk = "VK_RIGHT" Shift="true" Win="true"/>
                  //    <SendMessage Cmd="maximize" Msg="274" wParam="61488" lParam="0" />
                  //    <SendInput vk = "VK_RETURN" >
                  //    <Chars Args="Second "/>
                  //    <Chars Args = "line.." >
                  //        < SendInput vk="h" Alt="true"/>
                  //        <SendInput vk = "a" Alt="false">
                  //        <SendInput vk = "VK_ESCAPE" />
                  //        </ SendInput >
                  //    </ Chars >
                  //    </ SendInput >
                  //</ StartProcess >                  
                  new StartProcessCommand() { 
                      // Start notepad.exe
                      Cmd = "type_into_notepad", File="notepad.exe", EmbeddedCommands = new List<Command>() {
                            // pause 100ms
                            new PauseCommand() { Args = "100", Enabled = true },
                            // send some text
                            new CharsCommand() { Args="this is a test typed into Notepad.", Enabled = true  },
                            // hit retrun
                            new SendInputCommand() { Vk = "VK_RETURN", Enabled = true  },
                            // hit win-shirt-right to put notepad on the other monitor
                            new SendInputCommand() { Vk = "VK_RIGHT", Win = true, Shift = true, Enabled = true  },
                            new SendMessageCommand() { Msg=274, WParam=61488, LParam=0, Enabled = true  },
                            new CharsCommand() { Args="Second ", Enabled = true  },
                            new CharsCommand() { Args="line...", Enabled = true, EmbeddedCommands = new List<Command>() {
                                // Bring up help, about
                                new PauseCommand() { Args = "250", Enabled = true },
                                new SendInputCommand() { Vk = "h", Alt=true, Enabled = true  },
                                new SendInputCommand() { Vk = "a", Alt=false, Enabled = true  }
                            }},
                       }
                  }
            };
        }

        public StartProcessCommand() {
        }

        //// Deal with ensuring NextCommand has the right reply context
        //public override Reply Reply {
        //    get => base.Reply; set {
        //        if (NextCommand != null)
        //            NextCommand.Reply = value;
        //        this.Reply = value;
        //    }
        //}

        // TODO: This does not show embedded next commands
        public override string ToString() {
            return $"Cmd=\"{Cmd}\" File=\"{File}\" Arguments=\"{Arguments}\" Verb=\"{Verb}\"";
        }

        public override ICommand Clone(Reply reply) => base.Clone(reply, new StartProcessCommand() {
            File = this.File,
            Arguments = this.Arguments,
            Verb = this.Verb
        });

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Process is long lived")]
        // ICommand:Execute
        public override bool Execute() {
            if (!base.Execute()) {
                return false;
            }

            if (this.Reply is null) {
                throw new InvalidOperationException("Reply property cannot be null.");
            }

            Logger.Instance.Log4.Info($"{this.GetType().Name}: Starting process: {ToString()}");
            if (File != null) {
                var p = new Process {
                    StartInfo = {
                        FileName = File,
                        Arguments = Arguments,
                        Verb = Verb,
                        UseShellExecute = true
                    },
                };

                // TODO: Make delay smarter
                p.Start();
                if (EmbeddedCommands != null && EmbeddedCommands.Count > 0) {
                    try {
                        p.WaitForInputIdle(1000); // TODO: Make this settable
                    }
                    catch (System.InvalidOperationException e) {
                        Logger.Instance.Log4.Error($"{this.GetType().Name}: {e.Message}");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
