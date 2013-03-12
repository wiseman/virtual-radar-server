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
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.View;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Localisation;
using InterfaceFactory;
using VirtualRadar.Interface;

namespace VirtualRadar.WinForms
{
    /// <summary>
    /// The WinForms implementation of <see cref="IReceiverLocationsView"/>.
    /// </summary>
    public partial class ReceiverLocationsView : Form, IReceiverLocationsView
    {
        #region Fields
        /// <summary>
        /// The object that is controlling this view.
        /// </summary>
        IReceiverLocationsPresenter _Presenter;

        /// <summary>
        /// If true then the selected item changed handler for the list view of receiver locations is not allowed to run.
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
        public List<ReceiverLocation> ReceiverLocations { get; private set; }

        private ReceiverLocation _SelectedReceiverLocation;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public ReceiverLocation SelectedReceiverLocation
        {
            get { return listViewReceiverLocations.SelectedItems.Count == 0 ? null : listViewReceiverLocations.SelectedItems[0].Tag as ReceiverLocation; }
            set
            {
                _SelectedReceiverLocation = value;
                listViewReceiverLocations.SelectedItems.Clear();
                var item = listViewReceiverLocations.Items.OfType<ListViewItem>().Where(r => r.Tag == value).FirstOrDefault();
                if(item != null) {
                    item.Selected = true;
                    item.EnsureVisible();
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public new string Location
        {
            get { return textBoxLocation.Text.Trim(); }
            set { textBoxLocation.Text = value ?? ""; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Latitude
        {
            get { return textBoxLatitude.Text.Trim(); }
            set { textBoxLatitude.Text = value ?? ""; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Longitude
        {
            get { return textBoxLongitude.Text.Trim(); }
            set { textBoxLongitude.Text = value ?? ""; }
        }
        #endregion

        #region Events exposed
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler SelectedLocationChanged;

        /// <summary>
        /// Raises <see cref="SelectedLocationChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnSelectedLocationChanged(EventArgs args)
        {
            if(SelectedLocationChanged != null) SelectedLocationChanged(this, args);
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
        public event EventHandler NewLocationClicked;

        /// <summary>
        /// Raises <see cref="NewLocationClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnNewLocationClicked(EventArgs args)
        {
            if(NewLocationClicked != null) NewLocationClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler DeleteLocationClicked;

        /// <summary>
        /// Raises <see cref="DeleteLocationClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnDeleteLocationClicked(EventArgs args)
        {
            if(DeleteLocationClicked != null) DeleteLocationClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler UpdateFromBaseStationDatabaseClicked;

        /// <summary>
        /// Raises <see cref="UpdateFromBaseStationDatabaseClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnUpdateFromBaseStationDatabaseClicked(EventArgs args)
        {
            if(UpdateFromBaseStationDatabaseClicked != null) UpdateFromBaseStationDatabaseClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler CloseClicked;

        /// <summary>
        /// Raises <see cref="CloseClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnCloseClicked(EventArgs args)
        {
            if(CloseClicked != null) CloseClicked(this, args);
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public ReceiverLocationsView()
        {
            _MonoAutoScaleMode = new MonoAutoScaleMode(this);
            InitializeComponent();

            ReceiverLocations = new List<ReceiverLocation>();
            _ValidationHelper = new ValidationHelper(errorProvider);
            _ValidationHelper.RegisterValidationField(ValidationField.Location, textBoxLocation);
            _ValidationHelper.RegisterValidationField(ValidationField.Latitude, textBoxLatitude);
            _ValidationHelper.RegisterValidationField(ValidationField.Longitude, textBoxLongitude);
        }
        #endregion

        #region PopulateLocations, PopulateListViewItem
        /// <summary>
        /// Fills the locations list view.
        /// </summary>
        private void PopulateLocations()
        {
            var currentSuppressState = _SuppressItemSelectedEventHandler;

            try {
                _SuppressItemSelectedEventHandler = true;
                var selected = SelectedReceiverLocation;

                listViewReceiverLocations.Items.Clear();
                foreach(var location in ReceiverLocations) {
                    var item = new ListViewItem() { Tag = location };
                    PopulateListViewItem(item);
                    listViewReceiverLocations.Items.Add(item);
                }

                if(ReceiverLocations.Contains(selected)) SelectedReceiverLocation = selected;
            } finally {
                _SuppressItemSelectedEventHandler = currentSuppressState;
            }
        }

        /// <summary>
        /// Fills a single list item's text with the values from the associated location.
        /// </summary>
        /// <param name="item"></param>
        private void PopulateListViewItem(ListViewItem item)
        {
            var location = (ReceiverLocation)item.Tag;
            while(item.SubItems.Count < 3) item.SubItems.Add("");
            item.SubItems[0].Text = location.Name;
            item.SubItems[1].Text = location.Latitude.ToString();
            item.SubItems[2].Text = location.Longitude.ToString();
        }
        #endregion

        #region RefreshSelectedLocation, RefreshLocations, FocusOnEditFields, ShowValidationResults
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void RefreshSelectedLocation()
        {
            var selectedLocation = SelectedReceiverLocation;
            var selectedItem = selectedLocation == null ? null : listViewReceiverLocations.Items.OfType<ListViewItem>().Where(r => r.Tag == selectedLocation).FirstOrDefault();
            if(selectedItem != null) PopulateListViewItem(selectedItem);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void RefreshLocations()
        {
            PopulateLocations();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void FocusOnEditFields()
        {
            textBoxLocation.Focus();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="results"></param>
        public void ShowValidationResults(IEnumerable<ValidationResult> results)
        {
            _ValidationHelper.ShowValidationResults(results);
            if(results.Any(r => !r.IsWarning)) DialogResult = DialogResult.None;
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
            PopulateLocations();

            _Presenter = Factory.Singleton.Resolve<IReceiverLocationsPresenter>();
            _Presenter.Initialise(this);

            _OnlineHelp = new OnlineHelpHelper(this, OnlineHelpAddress.WinFormsReceiverLocationsView);

            SelectedReceiverLocation = _SelectedReceiverLocation;
        }

        /// <summary>
        /// Raised when the selected index of receiver locations changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewReceiverLocations_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(!_SuppressItemSelectedEventHandler) OnSelectedLocationChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the new button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonNew_Click(object sender, EventArgs e)
        {
            OnNewLocationClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the delete button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDelete_Click(object sender, EventArgs e)
        {
            OnDeleteLocationClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Raised whenever the individual location name is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxLocation_TextChanged(object sender, EventArgs e)
        {
            OnValueChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised whenever the individual latitude is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxLatitude_TextChanged(object sender, EventArgs e)
        {
            OnValueChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Raised whenever the individual longitude is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxLongitude_TextChanged(object sender, EventArgs e)
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

        /// <summary>
        /// Raised when the Update from Database link is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabelUpdateFromDatabase_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnUpdateFromBaseStationDatabaseClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Raised when the user wants to close the dialog, potentially with a location selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            OnCloseClicked(EventArgs.Empty);
        }
        #endregion
    }
}
