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

        private void buttonSendChars_Click(object sender, EventArgs e) {
            if (textBoxChars.TextLength == 1)
                textBoxSendCommand.Text = textBoxChars.Text;
            else {
                textBoxSendCommand.Text = "chars:" + textBoxChars.Text;
            }
            Send();
        }

        private void buttonSend_Click(object sender, EventArgs e) {
            foreach (string line in textBoxSendCommand.Lines)
                Send(line);
        }

        private static void Send(string line) {
            Logger.Instance.Log4.Info("Sending Command: " + line);
            MainWindow.Instance.SendLine(line);
        }

        private void Send() {
            Send(textBoxSendCommand.Text);
        }

        private void listCmds_DoubleClick(object sender, EventArgs e) {
            if (listCmds.SelectedItems.Count > 0) {
                log4.Debug("listCmds_DoubleClick");
                textBoxSendCommand.Text = listCmds.SelectedItems[0].Text;
                Send();
            }
        }

        private void CommandWindow_VisibleChanged(object sender, EventArgs e) {
            if (!Visible) return;
            RefreshList();
        }

        public void RefreshList() { 
            listCmds.Items.Clear();
            var orderedKeys = MainWindow.Instance.Invoker.Keys.Cast<string>().OrderBy(c => c);
            foreach (string key in orderedKeys) {
                Command cmd = (Command)MainWindow.Instance.Invoker[key];
                var item = new ListViewItem(cmd.Key);
                Match match = Regex.Match(cmd.GetType().ToString(), @"MCEControl\.([A-za-z]+)Command");
                item.SubItems.Add(match.Groups[1].Value);
                item.SubItems.Add(cmd.ToString());
                listCmds.Items.Add(item);
            }
            // Set column widths to fit longest items
            listCmds.Columns[0].Width = -1;
            listCmds.Columns[1].Width = -1;
            listCmds.Columns[2].Width = -1;

            listCmds.Focus();
            listCmds.Items[0].Selected = true;
        }
    }
}
