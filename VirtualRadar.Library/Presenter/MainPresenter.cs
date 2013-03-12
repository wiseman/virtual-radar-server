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
using System.Diagnostics;
using System.Linq;
using System.Text;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.View;
using VirtualRadar.Interface.WebServer;

namespace VirtualRadar.Library.Presenter
{
    /// <summary>
    /// The default implementation of <see cref="IMainPresenter"/>.
    /// </summary>
    class MainPresenter : IMainPresenter
    {
        #region Private class - DefaultProvider
        /// <summary>
        /// The default implementation of <see cref="IMainPresenterProvider"/>.
        /// </summary>
        class DefaultProvider : IMainPresenterProvider
        {
            public DateTime UtcNow { get { return DateTime.UtcNow; } }
        }
        #endregion

        #region Fields
        /// <summary>
        /// The UTC time that the version check was last performed.
        /// </summary>
        private DateTime _LastVersionCheck;

        /// <summary>
        /// The listener that is handling the connection to the data source.
        /// </summary>
        private IAutoConfigListener _AutoConfigListener;

        /// <summary>
        /// The web server that we're using.
        /// </summary>
        private IWebServer _WebServer;
        #endregion

        #region Properties
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IMainPresenterProvider Provider { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IMainView View { get; private set; }

        private IBaseStationAircraftList _BaseStationAircraftList;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IBaseStationAircraftList BaseStationAircraftList
        {
            get { return _BaseStationAircraftList; }
            set
            {
                _BaseStationAircraftList = value;
                _BaseStationAircraftList.CountChanged += BaseStationAircraftList_CountChanged;
                if(View != null) View.AircraftCount = _BaseStationAircraftList.Count;
            }
        }

        private IUniversalPlugAndPlayManager _UPnpManager;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IUniversalPlugAndPlayManager UPnpManager
        {
            get { return _UPnpManager; }
            set
            {
                _UPnpManager = value;
                _UPnpManager.StateChanged += UPnpManager_StateChanged;
                if(View != null) {
                    View.UPnpEnabled = _UPnpManager.IsEnabled;
                    View.UPnpRouterPresent = _UPnpManager.IsRouterPresent;
                    View.UPnpPortForwardingActive = _UPnpManager.PortForwardingPresent;
                }
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public MainPresenter()
        {
            Provider = new DefaultProvider();
        }
        #endregion

        #region Initialise
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="view"></param>
        public void Initialise(IMainView view)
        {
            if(view == null) throw new ArgumentNullException("view");
            View = view;
            View.CheckForNewVersion += View_CheckForNewVersion;
            View.ReconnectToBaseStationClicked += View_ReconnectToBaseStationClicked;
            View.ToggleServerStatus += View_ToggleServerStatus;
            View.ToggleUPnpStatus += View_ToggleUPnpStatus;
            View.LogFileName = Factory.Singleton.Resolve<ILog>().Singleton.FileName;
            View.InvalidPluginCount = Factory.Singleton.Resolve<IPluginManager>().Singleton.IgnoredPlugins.Count;
            if(_BaseStationAircraftList != null) View.AircraftCount = _BaseStationAircraftList.Count;

            var heartbeatService = Factory.Singleton.Resolve<IHeartbeatService>().Singleton;
            heartbeatService.SlowTick += HeartbeatService_SlowTick;
            heartbeatService.FastTick += HeartbeatService_FastTick;

            var newVersionChecker = Factory.Singleton.Resolve<INewVersionChecker>().Singleton;
            newVersionChecker.NewVersionAvailable += NewVersionChecker_NewVersionAvailable;

            _AutoConfigListener = Factory.Singleton.Resolve<IAutoConfigListener>().Singleton;
            _AutoConfigListener.Listener.ConnectionStateChanged += BaseStationListener_ConnectionStateChanged;
            View.BaseStationTotalMessages = _AutoConfigListener.Listener.TotalMessages;
            View.BaseStationConnectionStatus = _AutoConfigListener.Listener.ConnectionStatus;
            View.BaseStationTotalBadMessages = _AutoConfigListener.Listener.TotalBadMessages;

            _WebServer = Factory.Singleton.Resolve<IAutoConfigWebServer>().Singleton.WebServer;
            _WebServer.ExternalAddressChanged += WebServer_ExternalAddressChanged;
            _WebServer.OnlineChanged += WebServer_OnlineChanged;
            _WebServer.ResponseSent += WebServer_ResponseSent;
            View.WebServerIsOnline = _WebServer.Online;
            View.WebServerLocalAddress = _WebServer.LocalAddress;
            View.WebServerNetworkAddress = _WebServer.NetworkAddress;
            View.WebServerExternalAddress = _WebServer.ExternalAddress;

            var pluginManager = Factory.Singleton.Resolve<IPluginManager>().Singleton;
            foreach(var plugin in pluginManager.LoadedPlugins) {
                plugin.GuiThreadStartup();
            }

            var rebroadcastServerManager = Factory.Singleton.Resolve<IRebroadcastServerManager>().Singleton;
            rebroadcastServerManager.BroadcastSent += RebroadcastServerManager_BroadcastSent;
            rebroadcastServerManager.ClientConnected += RebroadcastServerManager_ClientConnected;
            rebroadcastServerManager.ClientDisconnected += RebroadcastServerManager_ClientDisconnected;

            var configurationStorage = DisplayConfigurationSettings();
            configurationStorage.ConfigurationChanged += ConfigurationStorage_ConfigurationChanged;
        }

        /// <summary>
        /// Updates the display of any configuration settings we're showing on the view.
        /// </summary>
        /// <returns></returns>
        private IConfigurationStorage DisplayConfigurationSettings()
        {
            var result = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton;

            var configuration = result.Load();
            View.RebroadcastServersConfiguration = Describe.RebroadcastSettingsCollection(configuration.RebroadcastSettings);

            return result;
        }
        #endregion

        #region PerformPeriodicChecks
        /// <summary>
        /// Performs checks, usually invoked on a background thread by the heartbeat service.
        /// </summary>
        private void PerformPeriodicChecks()
        {
            var configuration = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton.Load();

            var now = Provider.UtcNow;

            if(configuration.VersionCheckSettings.CheckAutomatically && configuration.VersionCheckSettings.CheckPeriodDays > 0) {
                if((now - _LastVersionCheck).TotalDays >= configuration.VersionCheckSettings.CheckPeriodDays) {
                    try {
                        var newVersionChecker = Factory.Singleton.Resolve<INewVersionChecker>().Singleton;
                        newVersionChecker.CheckForNewVersion();
                    } catch(Exception ex) {
                        Debug.WriteLine(String.Format("MainPresenter.PerformPeriodicChecks caught exception: {0}", ex.ToString()));
                        var log = Factory.Singleton.Resolve<ILog>().Singleton;
                        log.WriteLine("Caught exception while automatically checking for new version: {0}", ex.ToString());
                    }
                    _LastVersionCheck = now;
                }
            }
        }
        #endregion

        #region Do...
        /// <summary>
        /// Runs the check for a new version manually. Unlike the periodic check this will be called on the GUI thread.
        /// </summary>
        private void DoManualCheckForNewVersion()
        {
            bool newVersionAvailable;

            var previousState = View.ShowBusy(true, null);
            try {
                var versionChecker = Factory.Singleton.Resolve<INewVersionChecker>().Singleton;
                newVersionAvailable = versionChecker.CheckForNewVersion();
            } finally {
                View.ShowBusy(false, previousState);
            }

            View.ShowManualVersionCheckResult(newVersionAvailable);
        }
        #endregion

        #region Events consumed
        private void BaseStationAircraftList_CountChanged(object sender, EventArgs args)
        {
            View.AircraftCount = _BaseStationAircraftList.Count;
        }

        private void BaseStationListener_ConnectionStateChanged(object sender, EventArgs args)
        {
            View.BaseStationConnectionStatus = _AutoConfigListener.Listener.ConnectionStatus;
        }

        private void ConfigurationStorage_ConfigurationChanged(object sender, EventArgs args)
        {
            DisplayConfigurationSettings();
        }

        private void HeartbeatService_SlowTick(object sender, EventArgs args)
        {
            PerformPeriodicChecks();
        }

        private void HeartbeatService_FastTick(object sender, EventArgs args)
        {
            View.BaseStationTotalMessages = _AutoConfigListener.Listener.TotalMessages;
            View.BaseStationTotalBadMessages = _AutoConfigListener.Listener.TotalBadMessages;
        }

        private void NewVersionChecker_NewVersionAvailable(object sender, EventArgs args)
        {
            var checker = (INewVersionChecker)sender;
            View.NewVersionDownloadUrl = checker.DownloadUrl;
            View.NewVersionAvailable = checker.IsNewVersionAvailable;
        }

        private void RebroadcastServerManager_BroadcastSent(object sender, BroadcastEventArgs args)
        {
            View.ShowRebroadcastClientServiced(args.EndPoint.Address.ToString(), args.EndPoint.Port, args.Port, args.Format, args.BytesSent);
        }

        private void RebroadcastServerManager_ClientConnected(object sender, BroadcastEventArgs args)
        {
            View.ShowRebroadcastClientServiced(args.EndPoint.Address.ToString(), args.EndPoint.Port, args.Port, args.Format, 0);
        }

        private void RebroadcastServerManager_ClientDisconnected(object sender, BroadcastEventArgs args)
        {
            View.ShowRebroadcastClientDisconnected(args.EndPoint.Address.ToString(), args.EndPoint.Port);
        }

        private void View_CheckForNewVersion(object sender, EventArgs args)
        {
            DoManualCheckForNewVersion();
        }

        private void View_ReconnectToBaseStationClicked(object sender, EventArgs args)
        {
            _AutoConfigListener.Listener.Disconnect();
            _AutoConfigListener.Listener.Connect(false);
        }

        private void View_ToggleServerStatus(object sender, EventArgs args)
        {
            _WebServer.Online = !_WebServer.Online;
        }

        private void View_ToggleUPnpStatus(object sender, EventArgs args)
        {
            if(UPnpManager.PortForwardingPresent) UPnpManager.TakeServerOffInternet();
            else                                  UPnpManager.PutServerOntoInternet();
        }

        private void UPnpManager_StateChanged(object sender, EventArgs args)
        {
            View.UPnpPortForwardingActive = UPnpManager.PortForwardingPresent;
            View.UPnpEnabled = UPnpManager.IsEnabled;
            View.UPnpRouterPresent = UPnpManager.IsRouterPresent;
        }

        private void WebServer_ExternalAddressChanged(object sender, EventArgs args)
        {
            View.WebServerExternalAddress = _WebServer.ExternalAddress;
        }

        private void WebServer_OnlineChanged(object sender, EventArgs args)
        {
            View.WebServerIsOnline = _WebServer.Online;
        }

        private void WebServer_ResponseSent(object sender, ResponseSentEventArgs args)
        {
            View.ShowWebRequestHasBeenServiced(args.UserAddressAndPort, args.UrlRequested, args.BytesSent);
        }
        #endregion
    }
}
