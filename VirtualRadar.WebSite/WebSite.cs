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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Database;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.StandingData;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Interface.WebSite;

namespace VirtualRadar.WebSite
{
    /// <summary>
    /// Implements <see cref="IWebSite"/>.
    /// </summary>
    class WebSite : IWebSite
    {
        #region Private class - DefaultProvider
        /// <summary>
        /// The default implementation of <see cref="IWebSiteProvider"/>.
        /// </summary>
        class DefaultProvider : IWebSiteProvider
        {
            public bool DirectoryExists(string folder)  { return Directory.Exists(folder); }
            public DateTime UtcNow                      { get { return DateTime.UtcNow; } }
        }
        #endregion

        #region Fields
        /// <summary>
        /// The object that synchronises threads that are performing authentication tasks for the site.
        /// </summary>
        private object _AuthenticationSyncLock = new object();

        /// <summary>
        /// The user that the server will allow when basic authentication is selected.
        /// </summary>
        private string _BasicAuthenticationUser;

        /// <summary>
        /// The password hash that the server will allow when basic authentication is selected.
        /// </summary>
        private Hash _BasicAuthenticationPasswordHash;

        /// <summary>
        /// A list of objects that can supply content for us.
        /// </summary>
        private List<Page> _Pages = new List<Page>();

        /// <summary>
        /// The page that will deal with requests for aircraft lists.
        /// </summary>
        private AircraftListJsonPage _AircraftListJsonPage = new AircraftListJsonPage();

        /// <summary>
        /// The page that will deal with requests for images.
        /// </summary>
        private ImagePage _ImagePage = new ImagePage();

        /// <summary>
        /// The page that handles requests from the proximity gadget.
        /// </summary>
        private ClosestAircraftJsonPage _ClosestAircraftJsonPage = new ClosestAircraftJsonPage();

        /// <summary>
        /// The page that handles requests for report rows.
        /// </summary>
        private ReportRowsJsonPage _ReportRowsJsonPage = new ReportRowsJsonPage();
        #endregion

        #region Properties
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IWebSiteProvider Provider { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IBaseStationAircraftList BaseStationAircraftList
        {
            get { return _AircraftListJsonPage.BaseStationAircraftList; }
            set
            {
                _AircraftListJsonPage.BaseStationAircraftList = value;
                _ClosestAircraftJsonPage.BaseStationAircraftList = value;
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public ISimpleAircraftList FlightSimulatorAircraftList
        {
            get { return _AircraftListJsonPage.FlightSimulatorAircraftList; }
            set { _AircraftListJsonPage.FlightSimulatorAircraftList = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IBaseStationDatabase BaseStationDatabase
        {
            get { return _ReportRowsJsonPage.BaseStationDatabase; }
            set { _ReportRowsJsonPage.BaseStationDatabase = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IStandingDataManager StandingDataManager
        {
            get { return _ReportRowsJsonPage.StandingDataManager; }
            set { _ReportRowsJsonPage.StandingDataManager = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IWebServer WebServer { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public WebSite()
        {
            Provider = new DefaultProvider();
        }
        #endregion

        #region AttachSiteToServer, LoadConfiguration
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="server"></param>
        public void AttachSiteToServer(IWebServer server)
        {
            if(server == null) throw new ArgumentNullException("server");
            if(WebServer != server) {
                if(WebServer != null) throw new InvalidOperationException("The web site can only be attached to one server");

                var configurationStorage = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton;
                configurationStorage.ConfigurationChanged += ConfigurationStorage_ConfigurationChanged;

                WebServer = server;
                server.Root = "/VirtualRadar";

                var installerSettingsStorage = Factory.Singleton.Resolve<IInstallerSettingsStorage>();
                var installerSettings = installerSettingsStorage.Load();
                server.Port = installerSettings.WebServerPort;

                server.AuthenticationRequired += Server_AuthenticationRequired;

                _Pages.Add(new TextPage());
                _Pages.Add(_AircraftListJsonPage);
                _Pages.Add(_ImagePage);
                _Pages.Add(new AudioPage());
                _Pages.Add(new FaviconPage());
                _Pages.Add(_ReportRowsJsonPage);
                _Pages.Add(_ClosestAircraftJsonPage);

                foreach(var page in _Pages) {
                    page.Provider = Provider;
                }

                LoadConfiguration();

                server.RequestReceived += Server_RequestReceived;
            }
        }

        /// <summary>
        /// Loads and applies the configuration from disk.
        /// </summary>
        /// <returns>True if the server should be restarted because of changes to the configuration.</returns>
        private bool LoadConfiguration()
        {
            var configuration = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton.Load();

            bool result = false;
            lock(_AuthenticationSyncLock) {
                _BasicAuthenticationUser = configuration.WebServerSettings.BasicAuthenticationUser;
                _BasicAuthenticationPasswordHash = configuration.WebServerSettings.BasicAuthenticationPasswordHash;
                if(WebServer.AuthenticationScheme != configuration.WebServerSettings.AuthenticationScheme) {
                    result = true;
                    WebServer.AuthenticationScheme = configuration.WebServerSettings.AuthenticationScheme;
                }
            }

            foreach(var page in _Pages) {
                page.LoadConfiguration(configuration);
            }

            return result;
        }
        #endregion

        #region Events consumed
        /// <summary>
        /// Handles changes to the configuration.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ConfigurationStorage_ConfigurationChanged(object sender, EventArgs args)
        {
            if(WebServer != null && LoadConfiguration()) {
                WebServer.Online = false;
                WebServer.Online = true;
            }
        }

        /// <summary>
        /// Handles the authentication events from the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Server_AuthenticationRequired(object sender, AuthenticationRequiredEventArgs args)
        {
            lock(_AuthenticationSyncLock) {
                if(!args.IsHandled && WebServer.AuthenticationScheme == AuthenticationSchemes.Basic) {
                    args.IsAuthenticated = args.User != null && args.User.Equals(_BasicAuthenticationUser, StringComparison.OrdinalIgnoreCase);
                    if(args.IsAuthenticated) args.IsAuthenticated = _BasicAuthenticationPasswordHash.PasswordMatches(args.Password);
                    args.IsHandled = true;
                }
            }
        }

        /// <summary>
        /// Handles the request for content by a server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Server_RequestReceived(object sender, RequestReceivedEventArgs args)
        {
            foreach(var page in _Pages) {
                page.HandleRequest(sender, args);
            }
        }
        #endregion
    }
}
