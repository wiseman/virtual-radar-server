namespace VirtualRadar.WinForms.Controls
{
    partial class DataFeedStatusControl
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
            if(disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.labelConnectionStatus = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.labelTotalMessages = new System.Windows.Forms.Label();
            this.toolTipAddress = new System.Windows.Forms.ToolTip(this.components);
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.label2 = new System.Windows.Forms.Label();
            this.labelCountAircraft = new System.Windows.Forms.Label();
            this.labelTotalBadMessages = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "::ConnectionStatus:::";
            // 
            // labelConnectionStatus
            // 
            this.labelConnectionStatus.AutoSize = true;
            this.labelConnectionStatus.Location = new System.Drawing.Point(135, 0);
            this.labelConnectionStatus.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.labelConnectionStatus.Name = "labelConnectionStatus";
            this.labelConnectionStatus.Size = new System.Drawing.Size(88, 13);
            this.labelConnectionStatus.TabIndex = 1;
            this.labelConnectionStatus.Text = "::NotConnected::";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(0, 19);
            this.label3.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(94, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "::TotalMessages:::";
            // 
            // labelTotalMessages
            // 
            this.labelTotalMessages.AutoSize = true;
            this.labelTotalMessages.Location = new System.Drawing.Point(135, 19);
            this.labelTotalMessages.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.labelTotalMessages.Name = "labelTotalMessages";
            this.labelTotalMessages.Size = new System.Drawing.Size(88, 13);
            this.labelTotalMessages.TabIndex = 5;
            this.labelTotalMessages.Text = "999,999,999,999";
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Interval = 500;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(333, 0);
            this.label2.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(97, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "::TrackingAircraft:::";
            // 
            // labelCountAircraft
            // 
            this.labelCountAircraft.AutoSize = true;
            this.labelCountAircraft.Location = new System.Drawing.Point(452, 0);
            this.labelCountAircraft.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.labelCountAircraft.Name = "labelCountAircraft";
            this.labelCountAircraft.Size = new System.Drawing.Size(40, 13);
            this.labelCountAircraft.TabIndex = 7;
            this.labelCountAircraft.Text = "99,999";
            // 
            // labelTotalBadMessages
            // 
            this.labelTotalBadMessages.AutoSize = true;
            this.labelTotalBadMessages.Location = new System.Drawing.Point(452, 19);
            this.labelTotalBadMessages.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.labelTotalBadMessages.Name = "labelTotalBadMessages";
            this.labelTotalBadMessages.Size = new System.Drawing.Size(88, 13);
            this.labelTotalBadMessages.TabIndex = 9;
            this.labelTotalBadMessages.Text = "999,999,999,999";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(333, 19);
            this.label4.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(113, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "::TotalBadMessages:::";
            // 
            // BaseStationStatusControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelTotalBadMessages);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.labelCountAircraft);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.labelTotalMessages);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.labelConnectionStatus);
            this.Controls.Add(this.label1);
            this.Name = "BaseStationStatusControl";
            this.Size = new System.Drawing.Size(637, 37);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelConnectionStatus;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label labelTotalMessages;
        private System.Windows.Forms.ToolTip toolTipAddress;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelCountAircraft;
        private System.Windows.Forms.Label labelTotalBadMessages;
        private System.Windows.Forms.Label label4;
    }
}
