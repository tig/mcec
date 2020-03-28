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
            this.labelSendChars = new System.Windows.Forms.Label();
            this.textBoxChars = new System.Windows.Forms.TextBox();
            this.buttonSendChars = new System.Windows.Forms.Button();
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
            // labelSendChars
            // 
            this.labelSendChars.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelSendChars.AutoSize = true;
            this.labelSendChars.Location = new System.Drawing.Point(8, 408);
            this.labelSendChars.Name = "labelSendChars";
            this.labelSendChars.Size = new System.Drawing.Size(126, 13);
            this.labelSendChars.TabIndex = 0;
            this.labelSendChars.Text = "&Send \"chars:\" command:";
            // 
            // textBoxChars
            // 
            this.textBoxChars.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxChars.Location = new System.Drawing.Point(141, 408);
            this.textBoxChars.Name = "textBoxChars";
            this.textBoxChars.Size = new System.Drawing.Size(202, 20);
            this.textBoxChars.TabIndex = 1;
            // 
            // buttonSendChars
            // 
            this.buttonSendChars.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonSendChars.Location = new System.Drawing.Point(352, 408);
            this.buttonSendChars.Name = "buttonSendChars";
            this.buttonSendChars.Size = new System.Drawing.Size(84, 23);
            this.buttonSendChars.TabIndex = 2;
            this.buttonSendChars.Text = "Send";
            this.buttonSendChars.UseVisualStyleBackColor = true;
            this.buttonSendChars.Click += new System.EventHandler(this.buttonSendChars_Click);
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
            this.listCmds.Location = new System.Drawing.Point(12, 55);
            this.listCmds.MultiSelect = false;
            this.listCmds.Name = "listCmds";
            this.listCmds.Size = new System.Drawing.Size(765, 345);
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
            this.columnCmd.Width = 200;
            // 
            // columnType
            // 
            this.columnType.Text = "Type";
            this.columnType.Width = 150;
            // 
            // columnDetails
            // 
            this.columnDetails.Text = "Details";
            this.columnDetails.Width = 300;
            // 
            // labelSendAnyChars
            // 
            this.labelSendAnyChars.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelSendAnyChars.AutoSize = true;
            this.labelSendAnyChars.Location = new System.Drawing.Point(24, 432);
            this.labelSendAnyChars.Name = "labelSendAnyChars";
            this.labelSendAnyChars.Size = new System.Drawing.Size(109, 13);
            this.labelSendAnyChars.TabIndex = 7;
            this.labelSendAnyChars.Text = "Send &any commands:";
            // 
            // textBoxSendCommand
            // 
            this.textBoxSendCommand.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxSendCommand.Location = new System.Drawing.Point(141, 432);
            this.textBoxSendCommand.Multiline = true;
            this.textBoxSendCommand.Name = "textBoxSendCommand";
            this.textBoxSendCommand.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxSendCommand.Size = new System.Drawing.Size(202, 90);
            this.textBoxSendCommand.TabIndex = 8;
            // 
            // buttonSend
            // 
            this.buttonSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonSend.Location = new System.Drawing.Point(349, 500);
            this.buttonSend.MaximumSize = new System.Drawing.Size(84, 23);
            this.buttonSend.MinimumSize = new System.Drawing.Size(84, 23);
            this.buttonSend.Name = "buttonSend";
            this.buttonSend.Size = new System.Drawing.Size(84, 23);
            this.buttonSend.TabIndex = 9;
            this.buttonSend.Text = "Send";
            this.buttonSend.UseVisualStyleBackColor = true;
            this.buttonSend.Click += new System.EventHandler(this.buttonSend_Click);
            // 
            // saveChangesBtn
            // 
            this.saveChangesBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.saveChangesBtn.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.saveChangesBtn.Enabled = false;
            this.saveChangesBtn.Location = new System.Drawing.Point(12, 406);
            this.saveChangesBtn.Name = "saveChangesBtn";
            this.saveChangesBtn.Size = new System.Drawing.Size(221, 23);
            this.saveChangesBtn.TabIndex = 10;
            this.saveChangesBtn.Text = "Save MCECommands.commands file";
            this.saveChangesBtn.UseVisualStyleBackColor = true;
            this.saveChangesBtn.Click += new System.EventHandler(this.saveChangesBtn_Click);
            // 
            // testRadio
            // 
            this.testRadio.AutoSize = true;
            this.testRadio.Location = new System.Drawing.Point(13, 13);
            this.testRadio.Name = "testRadio";
            this.testRadio.Size = new System.Drawing.Size(241, 17);
            this.testRadio.TabIndex = 11;
            this.testRadio.TabStop = true;
            this.testRadio.Text = "&Test Sending Commands (doule-click to send)";
            this.testRadio.UseVisualStyleBackColor = true;
            this.testRadio.CheckedChanged += new System.EventHandler(this.testRadio_CheckedChanged);
            // 
            // editRadio
            // 
            this.editRadio.AutoSize = true;
            this.editRadio.Location = new System.Drawing.Point(13, 32);
            this.editRadio.Name = "editRadio";
            this.editRadio.Size = new System.Drawing.Size(153, 17);
            this.editRadio.TabIndex = 11;
            this.editRadio.TabStop = true;
            this.editRadio.Text = "&Enable/Disable Commands";
            this.editRadio.UseVisualStyleBackColor = true;
            // 
            // CommandWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(789, 527);
            this.Controls.Add(this.editRadio);
            this.Controls.Add(this.testRadio);
            this.Controls.Add(this.saveChangesBtn);
            this.Controls.Add(this.buttonSend);
            this.Controls.Add(this.textBoxSendCommand);
            this.Controls.Add(this.labelSendAnyChars);
            this.Controls.Add(this.listCmds);
            this.Controls.Add(this.buttonSendChars);
            this.Controls.Add(this.textBoxChars);
            this.Controls.Add(this.labelSendChars);
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

        private System.Windows.Forms.Label labelSendChars;
        private System.Windows.Forms.TextBox textBoxChars;
        private System.Windows.Forms.Button buttonSendChars;
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
