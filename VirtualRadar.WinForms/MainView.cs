// Copyright © 2010 onwards, Andrew Whewell
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
using VirtualRadar.Interface.View;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Localisation;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.WebServer;
using InterfaceFactory;
using System.Diagnostics;
using VirtualRadar.Interface.Settings;

namespace VirtualRadar.WinForms
{
    /// <summary>
    /// The WinForms implementation of <see cref="IMainView"/>.
    /// </summary>
    public partial class MainView : Form, IMainView
    {
        #region Fields
        /// <summary>
        /// The presenter that is managing this view.
        /// </summary>
        private IMainPresenter _Presenter;

        /// <summary>
        /// The object that's handling online help for us.
        /// </summary>
        private OnlineHelpHelper _OnlineHelp;

        /// <summary>
        /// The current instance of the modeless dialog that displays the FSX connection, if any.
        /// </summary>
        private FlightSimulatorXView _FlightSimulatorXView;

        /// <summary>
        /// The current instance of the modeless dialog that displays the statistics, if any.
        /// </summary>
        private StatisticsView _StatisticsView;

        // Objects that are being held for _Presenter while it doesn't exist.
        private IBaseStationAircraftList _BaseStationAircraftList;
        private ISimpleAircraftList _FlightSimulatorXAircraftList;
        private IUniversalPlugAndPlayManager _UPnpManager;
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
        public ConnectionStatus BaseStationConnectionStatus
        {
            get { return dataFeedStatusControl.ConnectionStatus; }
            set { dataFeedStatusControl.ConnectionStatus = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public long BaseStationTotalMessages
        {
            get { return dataFeedStatusControl.TotalMessages; }
            set { dataFeedStatusControl.TotalMessages = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public long BaseStationTotalBadMessages
        {
            get { return dataFeedStatusControl.TotalBadMessages; }
            set { dataFeedStatusControl.TotalBadMessages = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AircraftCount
        {
            get { return dataFeedStatusControl.TotalAircraft; }
            set { dataFeedStatusControl.TotalAircraft = value; }
        }

        private int _InvalidPluginCount;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public int InvalidPluginCount
        {
            get { return _InvalidPluginCount; }
            set
            {
                _InvalidPluginCount = value;
                toolStripDropDownButtonInvalidPluginCount.Text = String.Format(Strings.CountPluginsCouldNotBeLoaded, value);
                toolStripDropDownButtonInvalidPluginCount.Visible = value != 0;
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string LogFileName { get; set; }

        private bool _NewVersionAvailable;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool NewVersionAvailable
        {
            get { return _NewVersionAvailable; }
            set
            {
                if(InvokeRequired) BeginInvoke(new MethodInvoker(() => { NewVersionAvailable = value; }));
                else {
                    _NewVersionAvailable = value;
                    toolStripDropDownButtonLaterVersionAvailable.Visible = value;
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string NewVersionDownloadUrl { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string RebroadcastServersConfiguration
        {
            get { return rebroadcastStatusControl.Configuration; }
            set { rebroadcastStatusControl.Configuration = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool UPnpEnabled
        {
            get { return webServerStatusControl.UPnpEnabled; }
            set { webServerStatusControl.UPnpEnabled = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool UPnpRouterPresent
        {
            get { return webServerStatusControl.UPnpRouterPresent; }
            set { webServerStatusControl.UPnpRouterPresent = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool UPnpPortForwardingActive
        {
            get { return webServerStatusControl.UPnpPortForwardingActive; }
            set { webServerStatusControl.UPnpPortForwardingActive = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool WebServerIsOnline
        {
            get { return webServerStatusControl.ServerIsListening; }
            set { webServerStatusControl.ServerIsListening = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string WebServerLocalAddress
        {
            get { return webServerStatusControl.LocalAddress; }
            set { webServerStatusControl.LocalAddress = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string WebServerNetworkAddress
        {
            get { return webServerStatusControl.NetworkAddress; }
            set { webServerStatusControl.NetworkAddress = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string WebServerExternalAddress
        {
            get { return webServerStatusControl.InternetAddress; }
            set { webServerStatusControl.InternetAddress = value; }
        }
        #endregion

        #region Events exposed
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler CheckForNewVersion;

        /// <summary>
        /// Raises <see cref="CheckForNewVersion"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnCheckForNewVersion(EventArgs args)
        {
            if(CheckForNewVersion != null) CheckForNewVersion(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ReconnectToBaseStationClicked;

        /// <summary>
        /// Raises <see cref="ReconnectToBaseStationClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnReconnectToBaseStationClicked(EventArgs args)
        {
            if(ReconnectToBaseStationClicked != null) ReconnectToBaseStationClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ToggleServerStatus;

        /// <summary>
        /// Raises <see cref="ToggleServerStatus"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnToggleServerStatus(EventArgs args)
        {
            if(ToggleServerStatus != null) ToggleServerStatus(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ToggleUPnpStatus;

        /// <summary>
        /// Raises <see cref="ToggleUPnpStatus"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnToggleUPnpStatus(EventArgs args)
        {
            if(ToggleUPnpStatus != null) ToggleUPnpStatus(this, args);
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public MainView()
        {
            _MonoAutoScaleMode = new MonoAutoScaleMode(this);
            InitializeComponent();
        }
        #endregion

        #region Initialise
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="uPnpManager"></param>
        /// <param name="baseStationAircraftList"></param>
        /// <param name="flightSimulatorXAircraftList"></param>
        public void Initialise(IUniversalPlugAndPlayManager uPnpManager, IBaseStationAircraftList baseStationAircraftList, ISimpleAircraftList flightSimulatorXAircraftList)
        {
            _BaseStationAircraftList = baseStationAircraftList;
            _FlightSimulatorXAircraftList = flightSimulatorXAircraftList;
            _UPnpManager = uPnpManager;

            webServerStatusControl.UPnpIsSupported = true; // IsSupported was in the original UPnP code but was dropped in the re-write. Keeping UI elements for it in case I decide to put it back.
        }
        #endregion

        #region BubbleExceptionToGui
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="ex"></param>
        public void BubbleExceptionToGui(Exception ex)
        {
            if(!InvokeRequired) throw new ApplicationException("Exception thrown on background thread", ex);
            else                BeginInvoke(new MethodInvoker(() => BubbleExceptionToGui(ex)));
        }
        #endregion

        #region ShowBusy, ShowManualVersionCheckResult
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="isBusy"></param>
        /// <param name="previousState"></param>
        /// <returns></returns>
        public object ShowBusy(bool isBusy, object previousState)
        {
            return BusyViewHelper.ShowBusy(isBusy, previousState);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="newVersionAvailable"></param>
        public void ShowManualVersionCheckResult(bool newVersionAvailable)
        {
            MessageBox.Show(newVersionAvailable ? Strings.LaterVersionAvailable : Strings.LatestVersion, Strings.VersionCheckResult);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="endPointAddress"></param>
        /// <param name="endPointPort"></param>
        /// <param name="connectedToPort"></param>
        /// <param name="portFormat"></param>
        /// <param name="bytesSent"></param>
        public void ShowRebroadcastClientServiced(string endPointAddress, int endPointPort, int connectedToPort, RebroadcastFormat portFormat, int bytesSent)
        {
            rebroadcastStatusControl.UpdateEntry(endPointAddress, endPointPort, connectedToPort, portFormat, bytesSent);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="endPointAddress"></param>
        /// <param name="endPointPort"></param>
        public void ShowRebroadcastClientDisconnected(string endPointAddress, int endPointPort)
        {
            rebroadcastStatusControl.RemoveEntry(endPointAddress, endPointPort);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="url"></param>
        /// <param name="bytesSent"></param>
        public void ShowWebRequestHasBeenServiced(string address, string url, long bytesSent)
        {
            webServerStatusControl.ShowWebRequestHasBeenServiced(address, url, bytesSent);
        }
        #endregion

        #region Events consumed
        /// <summary>
        /// Called when the form is ready for use but not yet on screen.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if(!DesignMode) {
                toolStripDropDownButtonInvalidPluginCount.Visible = false;
                toolStripDropDownButtonLaterVersionAvailable.Visible = false;

                Localise.Form(this);

                _OnlineHelp = new OnlineHelpHelper(this, OnlineHelpAddress.WinFormsMainDialog);

                _Presenter = Factory.Singleton.Resolve<IMainPresenter>();
                _Presenter.Initialise(this);
                _Presenter.BaseStationAircraftList = _BaseStationAircraftList;
                _Presenter.UPnpManager = _UPnpManager;
            }
        }

        private void menuAboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using(var dialog = new AboutView()) {
                dialog.ShowDialog();
            }
        }

        private void menuCheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnCheckForNewVersion(EventArgs.Empty);
        }

        private void menuConnectionClientLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using(var dialog = new ConnectionClientLogView()) {
                dialog.ShowDialog();
            }
        }

        private void menuConnectionSessionLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using(var dialog = new ConnectionSessionLogView()) {
                dialog.ShowDialog();
            }
        }

        private void menuStatisticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(_StatisticsView != null) {
                _StatisticsView.WindowState = FormWindowState.Normal;
                _StatisticsView.Activate();
            } else {
                _StatisticsView = new StatisticsView();
                _StatisticsView.CloseClicked += StatisticsView_CloseClicked;
                _StatisticsView.Show();
            }
        }

        private void menuDownloadDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using(DownloadDataView dialog = new DownloadDataView()) {
                dialog.ShowDialog();
            }
        }

        private void menuExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void menuFlightSimulatorXModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(_FlightSimulatorXView != null) _FlightSimulatorXView.Activate();
            else {
                var webServer = Factory.Singleton.Resolve<IAutoConfigWebServer>().Singleton.WebServer;
                _FlightSimulatorXView = new FlightSimulatorXView();
                _FlightSimulatorXView.CloseClicked += FlightSimulatorXView_CloseClicked;
                _FlightSimulatorXView.Initialise(_BaseStationAircraftList, _FlightSimulatorXAircraftList, webServer);
                _FlightSimulatorXView.Show();
            }
        }

        private void menuOpenVirtualRadarLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(LogFileName);
        }

        private void menuPluginsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using(var dialog = new PluginsView()) {
                dialog.ShowDialog();
            }
        }

        private void menuOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using(var dialog = new OptionsPropertySheetView()) {
                dialog.ShowDialog();
            }
        }

        private void menuReconnectToBaseStationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnReconnectToBaseStationClicked(e);
        }

        private void FlightSimulatorXView_CloseClicked(object sender, EventArgs e)
        {
            if(_FlightSimulatorXView != null) {
                _FlightSimulatorXView.CloseClicked -= FlightSimulatorXView_CloseClicked;
                _FlightSimulatorXView.Close();
                _FlightSimulatorXView.Dispose();
                _FlightSimulatorXView = null;
            }
        }

        private void StatisticsView_CloseClicked(object sender, EventArgs e)
        {
            if(_StatisticsView != null) {
                _StatisticsView.CloseClicked -= StatisticsView_CloseClicked;
                _StatisticsView.Close();
                _StatisticsView.Dispose();
                _StatisticsView = null;
            }
        }

        private void webServerStatusControl_ToggleServerStatus(object sender, EventArgs e)
        {
            OnToggleServerStatus(e);
        }

        private void webServerStatusControl_ToggleUPnpStatus(object sender, EventArgs e)
        {
            OnToggleUPnpStatus(e);
        }

        private void toolStripDropDownButtonInvalidPluginCount_Click(object sender, EventArgs e)
        {
            using(var dialog = new InvalidPluginsView()) {
                dialog.ShowDialog();
            }
        }

        private void toolStripDropDownButtonLaterVersionAvailable_Click(object sender, EventArgs e)
        {
            Process.Start(NewVersionDownloadUrl);
        }
        #endregion
    }
}
