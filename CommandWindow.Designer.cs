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
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxChars = new System.Windows.Forms.TextBox();
            this.buttonSendChars = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.listCmds = new System.Windows.Forms.ListView();
            this.columnCmd = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label5 = new System.Windows.Forms.Label();
            this.textBoxSendCommand = new System.Windows.Forms.TextBox();
            this.buttonSend = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(36, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "chars:";
            // 
            // textBoxChars
            // 
            this.textBoxChars.Location = new System.Drawing.Point(63, 11);
            this.textBoxChars.Name = "textBoxChars";
            this.textBoxChars.Size = new System.Drawing.Size(169, 20);
            this.textBoxChars.TabIndex = 1;
            // 
            // buttonSendChars
            // 
            this.buttonSendChars.Location = new System.Drawing.Point(239, 10);
            this.buttonSendChars.Name = "buttonSendChars";
            this.buttonSendChars.Size = new System.Drawing.Size(75, 23);
            this.buttonSendChars.TabIndex = 2;
            this.buttonSendChars.Text = "Send";
            this.buttonSendChars.UseVisualStyleBackColor = true;
            this.buttonSendChars.Click += new System.EventHandler(this.buttonSendChars_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 36);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(302, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "Commands from MCEControl.commands (double-click to send):";
            // 
            // listCmds
            // 
            this.listCmds.Activation = System.Windows.Forms.ItemActivation.TwoClick;
            this.listCmds.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listCmds.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnCmd,
            this.columnType});
            this.listCmds.FullRowSelect = true;
            this.listCmds.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listCmds.Location = new System.Drawing.Point(12, 52);
            this.listCmds.MultiSelect = false;
            this.listCmds.Name = "listCmds";
            this.listCmds.Size = new System.Drawing.Size(408, 290);
            this.listCmds.TabIndex = 6;
            this.listCmds.UseCompatibleStateImageBehavior = false;
            this.listCmds.View = System.Windows.Forms.View.Details;
            this.listCmds.ItemActivate += new System.EventHandler(this.listCmds_ItemActivate);
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
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(10, 352);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(104, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Send any command:";
            // 
            // textBoxSendCommand
            // 
            this.textBoxSendCommand.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxSendCommand.Location = new System.Drawing.Point(118, 348);
            this.textBoxSendCommand.Name = "textBoxSendCommand";
            this.textBoxSendCommand.Size = new System.Drawing.Size(202, 20);
            this.textBoxSendCommand.TabIndex = 8;
            // 
            // buttonSend
            // 
            this.buttonSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSend.Location = new System.Drawing.Point(338, 346);
            this.buttonSend.MaximumSize = new System.Drawing.Size(84, 23);
            this.buttonSend.MinimumSize = new System.Drawing.Size(84, 23);
            this.buttonSend.Name = "buttonSend";
            this.buttonSend.Size = new System.Drawing.Size(84, 23);
            this.buttonSend.TabIndex = 9;
            this.buttonSend.Text = "Send";
            this.buttonSend.UseVisualStyleBackColor = true;
            this.buttonSend.Click += new System.EventHandler(this.buttonSend_Click);
            // 
            // CommandWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(432, 374);
            this.Controls.Add(this.buttonSend);
            this.Controls.Add(this.textBoxSendCommand);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.listCmds);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.buttonSendChars);
            this.Controls.Add(this.textBoxChars);
            this.Controls.Add(this.label1);
            this.Name = "CommandWindow";
            this.Text = "Commands";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CommandWindow_FormClosing);
            this.Load += new System.EventHandler(this.CommandWindow_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxChars;
        private System.Windows.Forms.Button buttonSendChars;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ListView listCmds;
        private System.Windows.Forms.ColumnHeader columnCmd;
        private System.Windows.Forms.ColumnHeader columnType;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBoxSendCommand;
        private System.Windows.Forms.Button buttonSend;

    }
}