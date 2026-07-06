// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

namespace MCEControl {
    partial class AgentSettingsTab {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null!;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this._checkBoxAllowProvisioning = new System.Windows.Forms.CheckBox();
            this._labelExplanation = new System.Windows.Forms.Label();
            this._groupSessions = new System.Windows.Forms.GroupBox();
            this._gridSessions = new System.Windows.Forms.DataGridView();
            this._columnId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._columnAge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._columnSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._columnStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._columnDelete = new System.Windows.Forms.DataGridViewButtonColumn();
            this._labelNoSessions = new System.Windows.Forms.Label();
            this._buttonProvision = new System.Windows.Forms.Button();
            this._buttonDeleteAll = new System.Windows.Forms.Button();
            this._toolTipAgent = new System.Windows.Forms.ToolTip(this.components);
            this._groupSessions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridSessions)).BeginInit();
            this.SuspendLayout();
            //
            // _checkBoxAllowProvisioning
            //
            this._checkBoxAllowProvisioning.AutoSize = true;
            this._checkBoxAllowProvisioning.Location = new System.Drawing.Point(12, 10);
            this._checkBoxAllowProvisioning.Margin = new System.Windows.Forms.Padding(1);
            this._checkBoxAllowProvisioning.Name = "_checkBoxAllowProvisioning";
            this._checkBoxAllowProvisioning.Size = new System.Drawing.Size(280, 21);
            this._checkBoxAllowProvisioning.TabIndex = 0;
            this._checkBoxAllowProvisioning.Text = "&Allow agents to provision disposable instances";
            this._toolTipAgent.SetToolTip(this._checkBoxAllowProvisioning, "When enabled, a connected agent may call provision-session to get a fresh, isolated" +
        " copy of MCEC to drive. Agent commands are enabled only inside that copy; this installed copy is never opened up.");
            this._checkBoxAllowProvisioning.CheckedChanged += new System.EventHandler(this.CheckAllowProvisioningCheckedChanged);
            //
            // _labelExplanation
            //
            this._labelExplanation.Location = new System.Drawing.Point(12, 34);
            this._labelExplanation.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelExplanation.Name = "_labelExplanation";
            this._labelExplanation.Size = new System.Drawing.Size(416, 44);
            this._labelExplanation.TabIndex = 1;
            this._labelExplanation.Text = "The agent gets a disposable, isolated copy to drive (deleted on teardown); this " +
                "installed copy is never opened up.";
            //
            // _groupSessions
            //
            this._groupSessions.BackColor = System.Drawing.SystemColors.Window;
            this._groupSessions.Controls.Add(this._gridSessions);
            this._groupSessions.Controls.Add(this._labelNoSessions);
            this._groupSessions.Controls.Add(this._buttonProvision);
            this._groupSessions.Controls.Add(this._buttonDeleteAll);
            this._groupSessions.Location = new System.Drawing.Point(12, 82);
            this._groupSessions.Margin = new System.Windows.Forms.Padding(1);
            this._groupSessions.Name = "_groupSessions";
            this._groupSessions.Padding = new System.Windows.Forms.Padding(1);
            this._groupSessions.Size = new System.Drawing.Size(416, 150);
            this._groupSessions.TabIndex = 2;
            this._groupSessions.TabStop = false;
            this._groupSessions.Text = "Provisioned instances";
            //
            // _gridSessions
            //
            this._gridSessions.AllowUserToAddRows = false;
            this._gridSessions.AllowUserToDeleteRows = false;
            this._gridSessions.AllowUserToResizeRows = false;
            this._gridSessions.BackgroundColor = System.Drawing.SystemColors.Window;
            this._gridSessions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._gridSessions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._columnId,
            this._columnAge,
            this._columnSize,
            this._columnStatus,
            this._columnDelete});
            this._gridSessions.Location = new System.Drawing.Point(8, 18);
            this._gridSessions.Margin = new System.Windows.Forms.Padding(1);
            this._gridSessions.MultiSelect = false;
            this._gridSessions.Name = "_gridSessions";
            this._gridSessions.ReadOnly = true;
            this._gridSessions.RowHeadersVisible = false;
            this._gridSessions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._gridSessions.Size = new System.Drawing.Size(400, 94);
            this._gridSessions.TabIndex = 0;
            this._gridSessions.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.GridSessionsCellContentClick);
            //
            // _columnId
            //
            this._columnId.HeaderText = "Session";
            this._columnId.Name = "_columnId";
            this._columnId.ReadOnly = true;
            this._columnId.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._columnId.Width = 100;
            //
            // _columnAge
            //
            this._columnAge.HeaderText = "Age";
            this._columnAge.Name = "_columnAge";
            this._columnAge.ReadOnly = true;
            this._columnAge.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._columnAge.Width = 62;
            //
            // _columnSize
            //
            this._columnSize.HeaderText = "Size";
            this._columnSize.Name = "_columnSize";
            this._columnSize.ReadOnly = true;
            this._columnSize.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._columnSize.Width = 72;
            //
            // _columnStatus
            //
            this._columnStatus.HeaderText = "Status";
            this._columnStatus.Name = "_columnStatus";
            this._columnStatus.ReadOnly = true;
            this._columnStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._columnStatus.Width = 64;
            //
            // _columnDelete
            //
            this._columnDelete.HeaderText = "";
            this._columnDelete.Name = "_columnDelete";
            this._columnDelete.ReadOnly = true;
            this._columnDelete.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this._columnDelete.Width = 64;
            //
            // _labelNoSessions
            //
            this._labelNoSessions.AutoSize = true;
            this._labelNoSessions.Location = new System.Drawing.Point(8, 124);
            this._labelNoSessions.Margin = new System.Windows.Forms.Padding(1, 0, 1, 0);
            this._labelNoSessions.Name = "_labelNoSessions";
            this._labelNoSessions.Size = new System.Drawing.Size(140, 17);
            this._labelNoSessions.TabIndex = 1;
            this._labelNoSessions.Text = "No provisioned instances.";
            //
            // _buttonProvision
            //
            this._buttonProvision.Location = new System.Drawing.Point(8, 120);
            this._buttonProvision.Margin = new System.Windows.Forms.Padding(1);
            this._buttonProvision.Name = "_buttonProvision";
            this._buttonProvision.Size = new System.Drawing.Size(110, 26);
            this._buttonProvision.TabIndex = 3;
            this._buttonProvision.Text = "&Provision new...";
            this._buttonProvision.UseVisualStyleBackColor = true;
            this._toolTipAgent.SetToolTip(this._buttonProvision, "Creates a fresh, disposable, isolated MCEC instance and shows how to point an agent at it. Requires the opt-in above.");
            this._buttonProvision.Click += new System.EventHandler(this.ButtonProvisionClick);
            //
            // _buttonDeleteAll
            //
            this._buttonDeleteAll.Location = new System.Drawing.Point(326, 120);
            this._buttonDeleteAll.Margin = new System.Windows.Forms.Padding(1);
            this._buttonDeleteAll.Name = "_buttonDeleteAll";
            this._buttonDeleteAll.Size = new System.Drawing.Size(82, 26);
            this._buttonDeleteAll.TabIndex = 2;
            this._buttonDeleteAll.Text = "&Delete all";
            this._buttonDeleteAll.UseVisualStyleBackColor = true;
            this._toolTipAgent.SetToolTip(this._buttonDeleteAll, "Deletes every provisioned instance directory. A running instance is skipped; stop it first.");
            this._buttonDeleteAll.Click += new System.EventHandler(this.ButtonDeleteAllClick);
            //
            // _toolTipAgent
            //
            this._toolTipAgent.ToolTipTitle = "Agent";
            //
            // AgentSettingsTab
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this._checkBoxAllowProvisioning);
            this.Controls.Add(this._labelExplanation);
            this.Controls.Add(this._groupSessions);
            this.Name = "AgentSettingsTab";
            this.Size = new System.Drawing.Size(440, 238);
            this.VisibleChanged += new System.EventHandler(this.AgentSettingsTab_VisibleChanged);
            this._groupSessions.ResumeLayout(false);
            this._groupSessions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridSessions)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox _checkBoxAllowProvisioning;
        private System.Windows.Forms.Label _labelExplanation;
        private System.Windows.Forms.GroupBox _groupSessions;
        private System.Windows.Forms.DataGridView _gridSessions;
        private System.Windows.Forms.DataGridViewTextBoxColumn _columnId;
        private System.Windows.Forms.DataGridViewTextBoxColumn _columnAge;
        private System.Windows.Forms.DataGridViewTextBoxColumn _columnSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn _columnStatus;
        private System.Windows.Forms.DataGridViewButtonColumn _columnDelete;
        private System.Windows.Forms.Label _labelNoSessions;
        private System.Windows.Forms.Button _buttonProvision;
        private System.Windows.Forms.Button _buttonDeleteAll;
        private System.Windows.Forms.ToolTip _toolTipAgent;
    }
}
