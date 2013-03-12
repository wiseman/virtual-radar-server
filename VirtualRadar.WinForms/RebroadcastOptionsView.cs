// Copyright © 2012 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.View;
using VirtualRadar.Localisation;
using VirtualRadar.Interface.Presenter;
using InterfaceFactory;
using VirtualRadar.Interface;

namespace VirtualRadar.WinForms
{
    /// <summary>
    /// The default WinForms implementation of <see cref="IRebroadcastOptionsView"/>.
    /// </summary>
    public partial class RebroadcastOptionsView : Form, IRebroadcastOptionsView
    {
        #region Private class - FormatItem
        /// <summary>
        /// A private class that converts between a RebroadcastItem and a localised string.
        /// </summary>
        class FormatItem
        {
            public RebroadcastFormat Format { get; set; }

            public string Description { get; set; }

            public override string  ToString()
            {
                return Description ?? "";
            }
        }
        #endregion

        #region Fields
        /// <summary>
        /// The object that is controlling this view.
        /// </summary>
        IRebroadcastOptionsPresenter _Presenter;

        /// <summary>
        /// A list of translated formats.
        /// </summary>
        private readonly FormatItem[] _FormatItems = new FormatItem[] {
            new FormatItem() { Format = RebroadcastFormat.Passthrough, Description = Strings.RebroadcastFormatPassthrough },
            new FormatItem() { Format = RebroadcastFormat.Port30003, Description = Strings.RebroadcastFormatPort30003 },
            new FormatItem() { Format = RebroadcastFormat.Avr, Description = Strings.RebroadcastFormatAvr},
        };

        /// <summary>
        /// If true then the selected item changed handler for the list view of servers is not allowed to run.
        /// </summary>
        private bool _SuppressItemSelectedEventHandler;

        /// <summary>
        /// The object that helps with the display of validation messages.
        /// </summary>
        private ValidationHelper _ValidationHelper;

        /// <summary>
        /// The object that's handling online help for us.
        /// </summary>
        private OnlineHelpHelper _OnlineHelp;
        #endregion

        #region Properties
        private MonoAutoScaleMode _MonoAutoScaleMode;
        /// <summary>
        /// Gets or sets the AutoScaleMode.
        /// </summary>
        /// <remarks>Works around Mono's weirdness over AutoScaleMode and anchoring / docking - see the comments against MonoAutoScaleMode.</remarks>
        public new AutoScaleMode AutoScaleMode
        {
            get { return _MonoAutoScaleMode.AutoScaleMode; }
            set { _MonoAutoScaleMode.AutoScaleMode = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public List<RebroadcastSettings> RebroadcastSettings { get; private set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public RebroadcastSettings SelectedRebroadcastSettings
        {
            get { return listView.SelectedItems.Count == 0 ? null : listView.SelectedItems[0].Tag as RebroadcastSettings; }
            set
            {
                listView.SelectedIndices.Clear();
                var item = listView.Items.OfType<ListViewItem>().Where(r => r.Tag == value).FirstOrDefault();
                if(item != null) {
                    item.Selected = true;
                    item.EnsureVisible();
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool ServerEnabled
        {
            get { return checkBoxEnabled.Checked; }
            set { checkBoxEnabled.Checked = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string ServerName
        {
            get { return textBoxName.Text.Trim(); }
            set { textBoxName.Text = value ?? ""; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public RebroadcastFormat ServerFormat
        {
            get { return comboBoxFormat.SelectedItem == null ? RebroadcastFormat.None : ((FormatItem)comboBoxFormat.SelectedItem).Format; }
            set { comboBoxFormat.SelectedItem = value == RebroadcastFormat.None ? null : _FormatItems.Single(r => r.Format == value); }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int ServerPort
        {
            get { return (int)numericUpDownPort.Value; }
            set { numericUpDownPort.Value = value; }
        }
        #endregion

        #region Events exposed
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler SelectedServerChanged;

        /// <summary>
        /// Raises <see cref="SelectedServerChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnSelectedServerChanged(EventArgs args)
        {
            if(SelectedServerChanged != null) SelectedServerChanged(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ResetClicked;

        /// <summary>
        /// Raises <see cref="ResetClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnResetClicked(EventArgs args)
        {
            if(ResetClicked != null) ResetClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ValueChanged;

        /// <summary>
        /// Raises <see cref="ValueChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnValueChanged(EventArgs args)
        {
            if(ValueChanged != null) ValueChanged(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler NewServerClicked;

        /// <summary>
        /// Raises <see cref="NewServerClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnNewServerClicked(EventArgs args)
        {
            if(NewServerClicked != null) NewServerClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler DeleteServerClicked;

        /// <summary>
        /// Raises <see cref="DeleteServerClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnDeleteServerClicked(EventArgs args)
        {
            if(DeleteServerClicked != null) DeleteServerClicked(this, args);
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public RebroadcastOptionsView()
        {
            _MonoAutoScaleMode = new MonoAutoScaleMode(this);
            InitializeComponent();

            RebroadcastSettings = new List<RebroadcastSettings>();
            _ValidationHelper = new ValidationHelper(errorProvider);
            _ValidationHelper.RegisterValidationField(ValidationField.Name, textBoxName);
            _ValidationHelper.RegisterValidationField(ValidationField.Format, comboBoxFormat);
            _ValidationHelper.RegisterValidationField(ValidationField.BaseStationPort, numericUpDownPort);
        }
        #endregion

        #region PopulateServers, PopulateListViewItem
        /// <summary>
        /// Fills the servers list view.
        /// </summary>
        private void PopulateServers()
        {
            var currentSuppressState = _SuppressItemSelectedEventHandler;

            try {
                _SuppressItemSelectedEventHandler = true;
                var selected = SelectedRebroadcastSettings;

                listView.Items.Clear();
                foreach(var settings in RebroadcastSettings) {
                    var item = new ListViewItem() { Tag = settings };
                    PopulateListViewItem(item);
                    listView.Items.Add(item);
                }

                if(RebroadcastSettings.Contains(selected)) SelectedRebroadcastSettings = selected;
            } finally {
                _SuppressItemSelectedEventHandler = currentSuppressState;
            }
        }

        /// <summary>
        /// Fills a single list item's text with the values from the associated server.
        /// </summary>
        /// <param name="item"></param>
        private void PopulateListViewItem(ListViewItem item)
        {
            var server = (RebroadcastSettings)item.Tag;
            var format = _FormatItems.Where(r => r.Format == server.Format).FirstOrDefault();

            while(item.SubItems.Count < 4) item.SubItems.Add("");
            item.SubItems[0].Text = server.Enabled ? Strings.Yes : Strings.No;
            item.SubItems[1].Text = server.Name;
            item.SubItems[2].Text = format == null ? "" : format.Description;
            item.SubItems[3].Text = server.Port.ToString();
        }
        #endregion

        #region RefreshSelectedServer, RefreshServers
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void RefreshSelectedServer()
        {
            var selectedServer = SelectedRebroadcastSettings;
            var selectedItem = selectedServer == null ? null : listView.Items.OfType<ListViewItem>().Where(r => r.Tag == selectedServer).FirstOrDefault();
            if(selectedItem != null) PopulateListViewItem(selectedItem);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void RefreshServers()
        {
            PopulateServers();
        }
        #endregion

        #region FocusOnEditFields, ShowValidationResults
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void FocusOnEditFields()
        {
            textBoxName.Focus();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="results"></param>
        public void ShowValidationResults(IEnumerable<ValidationResult> results)
        {
            _ValidationHelper.ShowValidationResults(results);
        }
        #endregion

        #region Events subscribed
        /// <summary>
        /// Called after the view has been created but before the user sees anything.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Localise.Form(this);
            comboBoxFormat.Items.Clear();
            foreach(var formatItem in _FormatItems) {
                comboBoxFormat.Items.Add(formatItem);
            }

            PopulateServers();

            _OnlineHelp = new OnlineHelpHelper(this, OnlineHelpAddress.WinFormsRebroadcastOptionsView);

            _Presenter = Factory.Singleton.Resolve<IRebroadcastOptionsPresenter>();
            _Presenter.Initialise(this);
        }

        /// <summary>
        /// Called when a user selects an item in the list of servers.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(!_SuppressItemSelectedEventHandler) OnSelectedServerChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the new button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonNew_Click(object sender, EventArgs e)
        {
            OnNewServerClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the delete button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDelete_Click(object sender, EventArgs e)
        {
            OnDeleteServerClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the enabled setting is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxEnabled_CheckedChanged(object sender, EventArgs e)
        {
            OnValueChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the name is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxName_TextChanged(object sender, EventArgs e)
        {
            OnValueChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the format is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            OnValueChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the port number is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numericUpDownPort_ValueChanged(object sender, EventArgs e)
        {
            OnValueChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised whenever the reset button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonReset_Click(object sender, EventArgs e)
        {
            OnResetClicked(EventArgs.Empty);
        }
        #endregion
    }
}
