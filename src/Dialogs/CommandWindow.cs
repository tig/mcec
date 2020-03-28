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
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.Security;
using WindowsInput.Native;

namespace MCEControl {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1501", Justification = "WinForms generated", Scope = "namespace")]
    public partial class CommandWindow : Form {
        private readonly log4net.ILog log4;

        public CommandWindow() {
            log4 = log4net.LogManager.GetLogger("MCEControl");
            InitializeComponent();
            testRadio.Checked = true;
        }
        
        private void CommandWindow_Load(object sender, EventArgs e) {
            Icon = MainWindow.Instance.Icon;
        }

        private void CommandWindow_FormClosing(object sender, FormClosingEventArgs e) {
            if (e.CloseReason == CloseReason.UserClosing) {
                this.Hide();
                MainWindow.Instance.Settings.ShowCommandWindow = false;
                e.Cancel = true;
            }
        }

        private void listCmds_ItemActivate(object sender, EventArgs e) {

        }

        private void buttonSend_Click(object sender, EventArgs e) {
            // TELEMETRY: 
            // what: See how often users test commands 
            // why: To determine how often users actaully test commands, if at all.
            TelemetryService.Instance.TrackEvent("listCmds_DoubleClick");

            textBoxSendCommand.Select();
            Task.Run(() => {
                foreach (string line in textBoxSendCommand.Lines)
                    Send(line);
            });
        }

        private void listCmds_DoubleClick(object sender, EventArgs e) {
            // TELEMETRY: 
            // what: See how often users test commands 
            // why: To determine how often users actaully test commands, if at all.
            TelemetryService.Instance.TrackEvent("listCmds_DoubleClick");

            if (listCmds.SelectedItems.Count > 0 && testRadio.Checked) {
                log4.Debug("listCmds_DoubleClick");
                textBoxSendCommand.Text = listCmds.SelectedItems[0].Text;
                textBoxSendCommand.Select();
                Send(textBoxSendCommand.Text);
            }
        }

        private void Send(string line) {
            MainWindow.Instance.SendLine(line);
        }

        private void CommandWindow_VisibleChanged(object sender, EventArgs e) {
            if (!Visible) return;
            RefreshList();
        }

        public void RefreshList() { 
            listCmds.Items.Clear();
            var orderedCmds = MainWindow.Instance.Invoker.Keys.Cast<string>().OrderBy(c => c);
            foreach (string command in orderedCmds) {
                Command cmd = MainWindow.Instance.Invoker.Values.Cast<Command>().FirstOrDefault(c => c.Cmd.ToLowerInvariant().Equals(command.ToLowerInvariant(), StringComparison.Ordinal));
                var item = new ListViewItem(cmd.Cmd);
                Match match = Regex.Match(cmd.GetType().ToString(), @"MCEControl\.([A-za-z]+)Command");
                item.SubItems.Add(match.Groups[1].Value);
                item.SubItems.Add(cmd.ToString());
                item.Checked = cmd.Enabled;
                listCmds.Items.Add(item);
            }
            // Set column widths to fit longest items
            listCmds.Columns[0].Width = -1;
            listCmds.Columns[1].Width = -1;
            listCmds.Columns[2].Width = -1;

            listCmds.Focus();
            listCmds.Items[0].Selected = true;
            saveChangesBtn.Enabled = false;
        }

        private void listCmds_ItemChecked(object sender, ItemCheckedEventArgs e) {
            Command cmd = MainWindow.Instance.Invoker.Values.Cast<Command>().FirstOrDefault(c => c.Cmd.ToLowerInvariant().Equals(e.Item.SubItems[0].Text.ToLowerInvariant(), StringComparison.Ordinal)); 
            cmd.Enabled = e.Item.Checked;
            saveChangesBtn.Enabled = true;
        }

        private void saveChangesBtn_Click(object sender, EventArgs e) {
            // TELEMETRY: 
            // what: See how often manually save commands 
            // why: 
            TelemetryService.Instance.TrackEvent("saveChangesBtn_Click");

            MainWindow.Instance.Invoker.Save($@"{Program.ConfigPath}MCEControl.commands");
        }

        private void testRadio_CheckedChanged(object sender, EventArgs e) {
            listCmds.CheckBoxes = editRadio.Checked;
            saveChangesBtn.Visible = editRadio.Checked;

            labelSendAnyChars.Visible = testRadio.Checked;
            textBoxSendCommand.Visible = testRadio.Checked;
            buttonSend.Visible = testRadio.Checked;
        }
    }
}
