namespace VirtualRadar.WinForms.Controls
{
    partial class RebroadcastStatusControl
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
            if(disposing && (components != null)) {
                components.Dispose();
            }
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
            this.groupBox = new System.Windows.Forms.GroupBox();
            this.listView = new System.Windows.Forms.ListView();
            this.columnHeaderIPAddress = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderPort = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderFormat = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderBytesSent = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label1 = new System.Windows.Forms.Label();
            this.labelDescribeConfiguration = new System.Windows.Forms.Label();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.groupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox
            // 
            this.groupBox.Controls.Add(this.listView);
            this.groupBox.Controls.Add(this.label1);
            this.groupBox.Controls.Add(this.labelDescribeConfiguration);
            this.groupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox.Location = new System.Drawing.Point(0, 0);
            this.groupBox.Name = "groupBox";
            this.groupBox.Size = new System.Drawing.Size(549, 218);
            this.groupBox.TabIndex = 0;
            this.groupBox.TabStop = false;
            this.groupBox.Text = "::RebroadcastServersStatus::";
            // 
            // listView
            // 
            this.listView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderIPAddress,
            this.columnHeaderPort,
            this.columnHeaderFormat,
            this.columnHeaderBytesSent});
            this.listView.FullRowSelect = true;
            this.listView.GridLines = true;
            this.listView.Location = new System.Drawing.Point(10, 37);
            this.listView.MultiSelect = false;
            this.listView.Name = "listView";
            this.listView.Size = new System.Drawing.Size(533, 175);
            this.listView.TabIndex = 2;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.Details;
            // 
            // columnHeaderIPAddress
            // 
            this.columnHeaderIPAddress.Text = "::IPAddress::";
            this.columnHeaderIPAddress.Width = 202;
            // 
            // columnHeaderPort
            // 
            this.columnHeaderPort.Text = "::Port::";
            // 
            // columnHeaderFormat
            // 
            this.columnHeaderFormat.Text = "::Format::";
            this.columnHeaderFormat.Width = 89;
            // 
            // columnHeaderBytesSent
            // 
            this.columnHeaderBytesSent.Text = "::BytesSent::";
            this.columnHeaderBytesSent.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnHeaderBytesSent.Width = 88;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(84, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "::Configuration:::";
            // 
            // labelDescribeConfiguration
            // 
            this.labelDescribeConfiguration.AutoSize = true;
            this.labelDescribeConfiguration.Location = new System.Drawing.Point(136, 20);
            this.labelDescribeConfiguration.Name = "labelDescribeConfiguration";
            this.labelDescribeConfiguration.Size = new System.Drawing.Size(33, 13);
            this.labelDescribeConfiguration.TabIndex = 0;
            this.labelDescribeConfiguration.Text = "None";
            // 
            // timerRefresh
            // 
            this.timerRefresh.Enabled = true;
            this.timerRefresh.Interval = 1000;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            // 
            // RebroadcastStatusControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox);
            this.Name = "RebroadcastStatusControl";
            this.Size = new System.Drawing.Size(549, 218);
            this.groupBox.ResumeLayout(false);
            this.groupBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelDescribeConfiguration;
        private System.Windows.Forms.ListView listView;
        private System.Windows.Forms.ColumnHeader columnHeaderIPAddress;
        private System.Windows.Forms.ColumnHeader columnHeaderPort;
        private System.Windows.Forms.ColumnHeader columnHeaderBytesSent;
        private System.Windows.Forms.ColumnHeader columnHeaderFormat;
        private System.Windows.Forms.Timer timerRefresh;
    }
}
