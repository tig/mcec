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

namespace MCEControl
{
    public partial class CommandWindow : Form {
        public CommandWindow()
        {
            InitializeComponent();
        }


        private void CommandWindow_Load(object sender, EventArgs e) {
            Icon = MainWindow.MainWnd.Icon;
            foreach (Command cmd in MainWindow.MainWnd.CmdTable.List) {
                var item = new ListViewItem(cmd.Key);
                Match match = Regex.Match(cmd.GetType().ToString(), @"MCEControl\.([A-za-z]+)Command");
                item.SubItems.Add(match.Groups[1].Value);
                listCmds.Items.Add(item);
            }

            // Now add VK_ commands
            foreach (VirtualKeyCode vk in Enum.GetValues(typeof(VirtualKeyCode))) {
                string s;
                if (vk > VirtualKeyCode.HELP && vk < VirtualKeyCode.LWIN)
                    s = vk.ToString();  // already have VK_
                else
                    s = "VK_" + vk.ToString(); 
                var item = new ListViewItem(s);
                item.SubItems.Add("SendInputCommand (pre-defined)");
                listCmds.Items.Add(item);
            }
        }

        private void CommandWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) {
                this.Hide();
                MainWindow.MainWnd.Settings.ShowCommandWindow = false;
                e.Cancel = true;
            }
        }

        private void listCmds_ItemActivate(object sender, EventArgs e)
        {

        }

        private void buttonSendChars_Click(object sender, EventArgs e) {
            textBoxSendCommand.Text = String.Format("chars:{0}", textBoxChars.Text);
            Send();
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("buttonSend_Click");
            Send();
        }

        private void Send()
        {
            Debug.WriteLine("Send");
            MainWindow.AddLogEntry("Client: Sending Command: " + textBoxSendCommand.Text);
            MainWindow.MainWnd.Client.Send(textBoxSendCommand.Text + "\n");
        }

        private void listCmds_DoubleClick(object sender, EventArgs e)
        {
            Debug.WriteLine("listCmds_DoubleClick");
            textBoxSendCommand.Text = listCmds.SelectedItems[0].Text;
            Send();
        }
    }
}
