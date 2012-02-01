//-------------------------------------------------------------------
// By Charlie Kindel
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the BSD License.
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
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.label1.Location = new System.Drawing.Point(8, 8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(224, 16);
            this.label1.TabIndex = 1;
            this.label1.Text = "MCE Controller v1.2";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.label2.Location = new System.Drawing.Point(24, 32);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(96, 16);
            this.label2.TabIndex = 1;
            this.label2.Text = "Copyright © 2012";
            // 
            // linkLabelMCEController
            // 
            this.linkLabelMCEController.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.linkLabelMCEController.Location = new System.Drawing.Point(24, 136);
            this.linkLabelMCEController.Name = "linkLabelMCEController";
            this.linkLabelMCEController.Size = new System.Drawing.Size(208, 16);
            this.linkLabelMCEController.TabIndex = 1;
            this.linkLabelMCEController.TabStop = true;
            this.linkLabelMCEController.Tag = "http://www.kindel.com/products/mcecontroller";
            this.linkLabelMCEController.Text = "www.kindel.com/products/mcecontroller";
            this.linkLabelMCEController.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelMCEController_LinkClicked);
            // 
            // buttonOK
            // 
            this.buttonOK.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.buttonOK.Location = new System.Drawing.Point(256, 160);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "OK";
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // linkLabelCharlie
            // 
            this.linkLabelCharlie.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.linkLabelCharlie.Location = new System.Drawing.Point(112, 32);
            this.linkLabelCharlie.Name = "linkLabelCharlie";
            this.linkLabelCharlie.Size = new System.Drawing.Size(152, 16);
            this.linkLabelCharlie.TabIndex = 2;
            this.linkLabelCharlie.TabStop = true;
            this.linkLabelCharlie.Tag = "http://www.kindel.com";
            this.linkLabelCharlie.Text = "Kindel Systems, LLC.";
            this.linkLabelCharlie.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelCharlie_LinkClicked);
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(24, 72);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(280, 40);
            this.label4.TabIndex = 3;
            this.label4.Text = "MCE Controller is licensed under the BSD license.  Source code available at http:" +
    "//sourceforge.net/projects/mcecontroller.";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(24, 120);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(184, 16);
            this.label5.TabIndex = 4;
            this.label5.Text = "For more information:";
            // 
            // AboutBox
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(338, 191);
            this.ControlBox = false;
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
            this.Load += new System.EventHandler(this.AboutBox_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private void label1_Click(object sender, System.EventArgs e)
        {
        
        }

        private void AboutBox_Load(object sender, System.EventArgs e)
        {
        
        }

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

        private void label4_Click(object sender, System.EventArgs e)
        {
        
        }
    }
}
