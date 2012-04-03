//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;

namespace MCEControl
{
    /// <summary>
    /// Summary description for Form1.
    /// </summary>
    public class AboutBox : System.Windows.Forms.Form
    {
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.LinkLabel linkLabelMCEController;
        private System.Windows.Forms.LinkLabel linkLabelCharlie;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private LinkLabel linkLabel1;
        private Label label3;
        private PictureBox pictureBoxDonate;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        public AboutBox()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //

//			System.Version v = new Version(Application.ProductVersion);
//			label1.Text = "MCE Controller Version " + v.Major.ToString() + "." + v.Minor.ToString() + "." + v.Revision.ToString();
            label1.Text = "MCE Controller Version " + Application.ProductVersion;
            
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            if( disposing )
            {
                if(components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.linkLabelMCEController = new System.Windows.Forms.LinkLabel();
            this.buttonOK = new System.Windows.Forms.Button();
            this.linkLabelCharlie = new System.Windows.Forms.LinkLabel();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.label3 = new System.Windows.Forms.Label();
            this.pictureBoxDonate = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDonate)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Lucida Sans", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(399, 21);
            this.label1.TabIndex = 0;
            this.label1.Text = "MCE Controller";
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.label2.Location = new System.Drawing.Point(12, 137);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(96, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "Copyright © 2012";
            // 
            // linkLabelMCEController
            // 
            this.linkLabelMCEController.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.linkLabelMCEController.Location = new System.Drawing.Point(12, 222);
            this.linkLabelMCEController.Name = "linkLabelMCEController";
            this.linkLabelMCEController.Size = new System.Drawing.Size(208, 16);
            this.linkLabelMCEController.TabIndex = 7;
            this.linkLabelMCEController.TabStop = true;
            this.linkLabelMCEController.Tag = "http://www.kindel.com/products/mcecontroller";
            this.linkLabelMCEController.Text = "http://tig.github.com/mcecontroller/";
            this.linkLabelMCEController.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelMCEController_LinkClicked);
            // 
            // buttonOK
            // 
            this.buttonOK.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.buttonOK.Location = new System.Drawing.Point(336, 219);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 8;
            this.buttonOK.Text = "OK";
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // linkLabelCharlie
            // 
            this.linkLabelCharlie.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.linkLabelCharlie.Location = new System.Drawing.Point(100, 137);
            this.linkLabelCharlie.Name = "linkLabelCharlie";
            this.linkLabelCharlie.Size = new System.Drawing.Size(152, 16);
            this.linkLabelCharlie.TabIndex = 3;
            this.linkLabelCharlie.TabStop = true;
            this.linkLabelCharlie.Tag = "http://www.kindel.com";
            this.linkLabelCharlie.Text = "Kindel Systems, LLC.";
            this.linkLabelCharlie.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelCharlie_LinkClicked);
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(12, 164);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(387, 16);
            this.label4.TabIndex = 4;
            this.label4.Text = "MCE Controller is licensed under the MIT License.  Source code available at";
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(12, 206);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(184, 16);
            this.label5.TabIndex = 6;
            this.label5.Text = "For more information:";
            // 
            // linkLabel1
            // 
            this.linkLabel1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.linkLabel1.Location = new System.Drawing.Point(12, 180);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(208, 16);
            this.linkLabel1.TabIndex = 5;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Tag = "https://github.com/tig/mcecontroller";
            this.linkLabel1.Text = "github.com/tig/mcecontroller";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(12, 45);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(399, 29);
            this.label3.TabIndex = 1;
            this.label3.Text = "MCE Controller is distributed as freeware. Donations of any value appreciated. Ma" +
    "ke donations by clicking on the button below.";
            // 
            // pictureBoxDonate
            // 
            this.pictureBoxDonate.ImageLocation = "http://images.sourceforge.net/images/project-support.jpg";
            this.pictureBoxDonate.Location = new System.Drawing.Point(149, 88);
            this.pictureBoxDonate.Name = "pictureBoxDonate";
            this.pictureBoxDonate.Size = new System.Drawing.Size(93, 35);
            this.pictureBoxDonate.TabIndex = 8;
            this.pictureBoxDonate.TabStop = false;
            this.pictureBoxDonate.Click += new System.EventHandler(this.pictureBoxDonate_Click);
            // 
            // AboutBox
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(423, 254);
            this.ControlBox = false;
            this.Controls.Add(this.pictureBoxDonate);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.linkLabelCharlie);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.linkLabelMCEController);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutBox";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About MCE Controller";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDonate)).EndInit();
            this.ResumeLayout(false);

        }
        #endregion


        private void buttonOK_Click(object sender, System.EventArgs e)
        {
            this.Close();
        }

        private void linkLabelMCEController_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkLabelMCEController.Tag.ToString());
        }

        private void linkLabelCharlie_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkLabelCharlie.Tag.ToString());

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkLabel1.Tag.ToString());
        }

        private void pictureBoxDonate_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://sourceforge.net/donate/index.php?group_id=138158");
        }
    }
}
