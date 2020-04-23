namespace MCEControl
{
    partial class CommandWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listCmds = new System.Windows.Forms.ListView();
            this.columnCmd = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnDetails = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.labelSendAnyChars = new System.Windows.Forms.Label();
            this.textBoxSendCommand = new System.Windows.Forms.TextBox();
            this.buttonSend = new System.Windows.Forms.Button();
            this.saveChangesBtn = new System.Windows.Forms.Button();
            this.testRadio = new System.Windows.Forms.RadioButton();
            this.editRadio = new System.Windows.Forms.RadioButton();
            this.SuspendLayout();
            // 
            // listCmds
            // 
            this.listCmds.Activation = System.Windows.Forms.ItemActivation.OneClick;
            this.listCmds.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listCmds.AutoArrange = false;
            this.listCmds.CheckBoxes = true;
            this.listCmds.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnCmd,
            this.columnType,
            this.columnDetails});
            this.listCmds.FullRowSelect = true;
            this.listCmds.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listCmds.HideSelection = false;
            this.listCmds.Location = new System.Drawing.Point(18, 85);
            this.listCmds.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.listCmds.MultiSelect = false;
            this.listCmds.Name = "listCmds";
            this.listCmds.Size = new System.Drawing.Size(760, 595);
            this.listCmds.TabIndex = 6;
            this.listCmds.UseCompatibleStateImageBehavior = false;
            this.listCmds.View = System.Windows.Forms.View.Details;
            this.listCmds.ItemActivate += new System.EventHandler(this.listCmds_ItemActivate);
            this.listCmds.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listCmds_ItemChecked);
            this.listCmds.DoubleClick += new System.EventHandler(this.listCmds_DoubleClick);
            // 
            // columnCmd
            // 
            this.columnCmd.Text = "Command";
            this.columnCmd.Width = 138;
            // 
            // columnType
            // 
            this.columnType.Text = "Type";
            this.columnType.Width = 150;
            // 
            // columnDetails
            // 
            this.columnDetails.Text = "Details";
            this.columnDetails.Width = 206;
            // 
            // labelSendAnyChars
            // 
            this.labelSendAnyChars.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSendAnyChars.AutoSize = true;
            this.labelSendAnyChars.Location = new System.Drawing.Point(802, 85);
            this.labelSendAnyChars.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelSendAnyChars.Name = "labelSendAnyChars";
            this.labelSendAnyChars.Size = new System.Drawing.Size(178, 20);
            this.labelSendAnyChars.TabIndex = 7;
            this.labelSendAnyChars.Text = "Send  list of commands:";
            // 
            // textBoxSendCommand
            // 
            this.textBoxSendCommand.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxSendCommand.Location = new System.Drawing.Point(807, 109);
            this.textBoxSendCommand.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBoxSendCommand.Multiline = true;
            this.textBoxSendCommand.Name = "textBoxSendCommand";
            this.textBoxSendCommand.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxSendCommand.Size = new System.Drawing.Size(337, 570);
            this.textBoxSendCommand.TabIndex = 8;
            // 
            // buttonSend
            // 
            this.buttonSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSend.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonSend.Location = new System.Drawing.Point(1020, 691);
            this.buttonSend.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.buttonSend.MaximumSize = new System.Drawing.Size(126, 35);
            this.buttonSend.MinimumSize = new System.Drawing.Size(126, 35);
            this.buttonSend.Name = "buttonSend";
            this.buttonSend.Size = new System.Drawing.Size(126, 35);
            this.buttonSend.TabIndex = 9;
            this.buttonSend.Text = "&Send";
            this.buttonSend.UseVisualStyleBackColor = true;
            this.buttonSend.Click += new System.EventHandler(this.buttonSend_Click);
            // 
            // saveChangesBtn
            // 
            this.saveChangesBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveChangesBtn.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.saveChangesBtn.Enabled = false;
            this.saveChangesBtn.Location = new System.Drawing.Point(448, 691);
            this.saveChangesBtn.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.saveChangesBtn.Name = "saveChangesBtn";
            this.saveChangesBtn.Size = new System.Drawing.Size(332, 35);
            this.saveChangesBtn.TabIndex = 10;
            this.saveChangesBtn.Text = "Save MCECommands.commands file";
            this.saveChangesBtn.UseVisualStyleBackColor = true;
            this.saveChangesBtn.Click += new System.EventHandler(this.saveChangesBtn_Click);
            // 
            // testRadio
            // 
            this.testRadio.AutoSize = true;
            this.testRadio.Location = new System.Drawing.Point(20, 20);
            this.testRadio.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.testRadio.Name = "testRadio";
            this.testRadio.Size = new System.Drawing.Size(362, 24);
            this.testRadio.TabIndex = 11;
            this.testRadio.TabStop = true;
            this.testRadio.Text = "&Test Sending Commands (doule-click to send).";
            this.testRadio.UseVisualStyleBackColor = true;
            this.testRadio.CheckedChanged += new System.EventHandler(this.testRadio_CheckedChanged);
            // 
            // editRadio
            // 
            this.editRadio.AutoSize = true;
            this.editRadio.Location = new System.Drawing.Point(20, 49);
            this.editRadio.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.editRadio.Name = "editRadio";
            this.editRadio.Size = new System.Drawing.Size(226, 24);
            this.editRadio.TabIndex = 11;
            this.editRadio.TabStop = true;
            this.editRadio.Text = "&Enable/Disable Commands";
            this.editRadio.UseVisualStyleBackColor = true;
            // 
            // CommandWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1164, 745);
            this.Controls.Add(this.editRadio);
            this.Controls.Add(this.testRadio);
            this.Controls.Add(this.saveChangesBtn);
            this.Controls.Add(this.buttonSend);
            this.Controls.Add(this.textBoxSendCommand);
            this.Controls.Add(this.labelSendAnyChars);
            this.Controls.Add(this.listCmds);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "CommandWindow";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "Commands";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CommandWindow_FormClosing);
            this.Load += new System.EventHandler(this.CommandWindow_Load);
            this.VisibleChanged += new System.EventHandler(this.CommandWindow_VisibleChanged);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ListView listCmds;
        private System.Windows.Forms.ColumnHeader columnCmd;
        private System.Windows.Forms.ColumnHeader columnType;
        private System.Windows.Forms.Label labelSendAnyChars;
        private System.Windows.Forms.TextBox textBoxSendCommand;
        private System.Windows.Forms.Button buttonSend;
        private System.Windows.Forms.ColumnHeader columnDetails;
        private System.Windows.Forms.Button saveChangesBtn;
        private System.Windows.Forms.RadioButton testRadio;
        private System.Windows.Forms.RadioButton editRadio;
    }
}
