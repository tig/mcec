// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
//
// To add a suppression to this file, right-click the message in the 
// Code Analysis results, point to "Suppress Message", and click 
// "In Suppression File".
// You do not need to add suppressions to this file manually.
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("", "ENC1003")]
[assembly: SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible", Scope = "member", Target = "WindowsInput.Native.KEYBDINPUT.#ExtraInfo")]
[assembly: SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible", Scope = "member", Target = "WindowsInput.Native.MOUSEINPUT.#ExtraInfo")]
[assembly: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", MessageId = "0", Scope = "member", Target = "WindowsInput.Native.NativeMethods.#GetAsyncKeyState(System.UInt16)")]
[assembly: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", MessageId = "0", Scope = "member", Target = "WindowsInput.Native.NativeMethods.#GetKeyState(System.UInt16)")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.UserActivityMonitor.Activity")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.UserActivityMonitor.HookManager_KeyDown(System.Object,System.Windows.Forms.KeyEventArgs)")]
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.CommandTable.Execute(MCEControl.Reply,System.String)")]
[assembly: SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.CommandTable.Deserialize(System.Boolean)~MCEControl.CommandTable")]
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "<Pending>", Scope = "type", Target = "~T:WindowsInput.Native.VirtualKeyCode")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.AboutBox.InitializeComponent")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "I know what I'm doing>")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.MainWindow.mainWindow_Load(System.Object,System.EventArgs)")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.MainWindow.ShowSettings(MCEControl.SettingsTab)")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.MainWindow.InitializeComponent")]
[assembly: SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.MainWindow.InitializeComponent")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.MainWindow.aboutMenuItem_Click(System.Object,System.EventArgs)")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.SendMessageCommand.Execute(MCEControl.Reply)")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.SettingsDialog.InitializeComponent")]
[assembly: SuppressMessage("Microsoft.Usage", "CA2213")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.McecCommand.Execute(MCEControl.Reply)")]
[assembly: SuppressMessage("Security", "CA3075:Insecure DTD processing in XML", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.CommandTable.Create(System.String,System.Boolean)~MCEControl.CommandTable")]
[assembly: SuppressMessage("Security", "CA3075:Insecure DTD processing in XML", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.CommandTable.LoadUserCommands")]
[assembly: SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.SerialServer.GetSettingsDisplayString~System.String")]
[assembly: SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Scope = "member", Target = "MCEControl.CommandTable.#LoadUserCommands()")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.SendMessageCommand.Execute(System.String,MCEControl.Reply)")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.McecCommand.Execute(System.String,MCEControl.Reply)")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.Command.Execute")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.McecCommand.Execute")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.McecCommand.Execute")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.MouseCommand.Execute")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.SendMessageCommand.Execute(y)")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.SendMessageCommand.Execute")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.StartProcessCommand.Execute")]
[assembly: SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.StringExt.IndexOfBreak(System.String,System.Int32,System.Int32@)~System.Int32")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "<Pending>", Scope = "member", Target = "~P:MCEControl.StartProcessCommand.Commands")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.CommandTable.LoadBuiltInCommands~MCEControl.CommandTable")]
[assembly: SuppressMessage("Usage", "CA2237:Mark ISerializable types with serializable", Justification = "<Pending>", Scope = "type", Target = "~T:MCEControl.Commands")]
[assembly: SuppressMessage("Design", "CA1010:Collections should implement generic interface", Justification = "<Pending>", Scope = "type", Target = "~T:MCEControl.Commands")]
[assembly: SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "<Pending>", Scope = "type", Target = "~T:MCEControl.Commands")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "<Pending>", Scope = "member", Target = "~P:MCEControl.MainWindow.Invoker")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.SerializedCommands.LoadBuiltInCommands~MCEControl.SerializedCommands")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>", Scope = "member", Target = "~M:MCEControl.UserActivityMonitorService.Start")]
