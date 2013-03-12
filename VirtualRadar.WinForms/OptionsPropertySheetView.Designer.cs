namespace VirtualRadar.WinForms
{
    partial class OptionsPropertySheetView
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.listBox = new System.Windows.Forms.ListBox();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.linkLabelUseRecommendedSettings = new System.Windows.Forms.LinkLabel();
            this.linkLabelUseIcaoSettings = new System.Windows.Forms.LinkLabel();
            this.buttonSheetButton = new System.Windows.Forms.Button();
            this.labelValidationMessages = new System.Windows.Forms.Label();
            this.linkLabelResetToDefaults = new System.Windows.Forms.LinkLabel();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer.Location = new System.Drawing.Point(12, 12);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.listBox);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.propertyGrid);
            this.splitContainer.Size = new System.Drawing.Size(860, 439);
            this.splitContainer.SplitterDistance = 193;
            this.splitContainer.TabIndex = 0;
            // 
            // listBox
            // 
            this.listBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox.FormattingEnabled = true;
            this.listBox.IntegralHeight = false;
            this.listBox.Location = new System.Drawing.Point(0, 0);
            this.listBox.Name = "listBox";
            this.listBox.Size = new System.Drawing.Size(193, 439);
            this.listBox.TabIndex = 0;
            this.listBox.SelectedIndexChanged += new System.EventHandler(this.listBox_SelectedIndexChanged);
            // 
            // propertyGrid
            // 
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.propertyGrid.Size = new System.Drawing.Size(663, 439);
            this.propertyGrid.TabIndex = 0;
            this.propertyGrid.ToolbarVisible = false;
            this.propertyGrid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid_PropertyValueChanged);
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(716, 527);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "::OK::";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(797, 527);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "::Cancel::";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.linkLabelUseRecommendedSettings);
            this.groupBox1.Controls.Add(this.linkLabelUseIcaoSettings);
            this.groupBox1.Controls.Add(this.buttonSheetButton);
            this.groupBox1.Controls.Add(this.labelValidationMessages);
            this.groupBox1.Location = new System.Drawing.Point(13, 451);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(859, 59);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            // 
            // linkLabelUseRecommendedSettings
            // 
            this.linkLabelUseRecommendedSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.linkLabelUseRecommendedSettings.AutoSize = true;
            this.linkLabelUseRecommendedSettings.Location = new System.Drawing.Point(505, 29);
            this.linkLabelUseRecommendedSettings.Name = "linkLabelUseRecommendedSettings";
            this.linkLabelUseRecommendedSettings.Size = new System.Drawing.Size(148, 13);
            this.linkLabelUseRecommendedSettings.TabIndex = 3;
            this.linkLabelUseRecommendedSettings.TabStop = true;
            this.linkLabelUseRecommendedSettings.Text = "::UseRecommendedSettings::";
            this.linkLabelUseRecommendedSettings.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelUseRecommendedSettings_LinkClicked);
            // 
            // linkLabelUseIcaoSettings
            // 
            this.linkLabelUseIcaoSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.linkLabelUseIcaoSettings.AutoSize = true;
            this.linkLabelUseIcaoSettings.Location = new System.Drawing.Point(505, 12);
            this.linkLabelUseIcaoSettings.Name = "linkLabelUseIcaoSettings";
            this.linkLabelUseIcaoSettings.Size = new System.Drawing.Size(158, 13);
            this.linkLabelUseIcaoSettings.TabIndex = 2;
            this.linkLabelUseIcaoSettings.TabStop = true;
            this.linkLabelUseIcaoSettings.Text = "::UseIcaoSpecificationSettings::";
            this.linkLabelUseIcaoSettings.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelUseIcaoSettings_LinkClicked);
            // 
            // buttonSheetButton
            // 
            this.buttonSheetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSheetButton.Location = new System.Drawing.Point(703, 19);
            this.buttonSheetButton.Name = "buttonSheetButton";
            this.buttonSheetButton.Size = new System.Drawing.Size(150, 23);
            this.buttonSheetButton.TabIndex = 1;
            this.buttonSheetButton.Text = "buttonSheetButton";
            this.buttonSheetButton.UseVisualStyleBackColor = true;
            this.buttonSheetButton.Click += new System.EventHandler(this.buttonSheetButton_Click);
            // 
            // labelValidationMessages
            // 
            this.labelValidationMessages.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelValidationMessages.ForeColor = System.Drawing.Color.Red;
            this.labelValidationMessages.Location = new System.Drawing.Point(7, 12);
            this.labelValidationMessages.Name = "labelValidationMessages";
            this.labelValidationMessages.Size = new System.Drawing.Size(491, 40);
            this.labelValidationMessages.TabIndex = 0;
            this.labelValidationMessages.Text = "validation message 1\r\nvalidation message 2\r\nvalidation message 3\r\n";
            // 
            // linkLabelResetToDefaults
            // 
            this.linkLabelResetToDefaults.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.linkLabelResetToDefaults.AutoSize = true;
            this.linkLabelResetToDefaults.Location = new System.Drawing.Point(12, 532);
            this.linkLabelResetToDefaults.Name = "linkLabelResetToDefaults";
            this.linkLabelResetToDefaults.Size = new System.Drawing.Size(132, 13);
            this.linkLabelResetToDefaults.TabIndex = 4;
            this.linkLabelResetToDefaults.TabStop = true;
            this.linkLabelResetToDefaults.Text = "::ResetSettingsToDefault::";
            this.linkLabelResetToDefaults.VisitedLinkColor = System.Drawing.Color.Blue;
            this.linkLabelResetToDefaults.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelResetToDefaults_LinkClicked);
            // 
            // OptionsPropertySheetView
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(884, 562);
            this.Controls.Add(this.linkLabelResetToDefaults);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.splitContainer);
            this.HelpButton = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OptionsPropertySheetView";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "::Options::";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ListBox listBox;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelValidationMessages;
        private System.Windows.Forms.Button buttonSheetButton;
        private System.Windows.Forms.LinkLabel linkLabelResetToDefaults;
        private System.Windows.Forms.LinkLabel linkLabelUseRecommendedSettings;
        private System.Windows.Forms.LinkLabel linkLabelUseIcaoSettings;

    }
}