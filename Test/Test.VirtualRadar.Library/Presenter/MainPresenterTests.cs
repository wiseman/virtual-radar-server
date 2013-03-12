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
using System.Linq;
using System.Text;
using InterfaceFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Test.Framework;
using VirtualRadar.Interface;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.View;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Library;
using System.Net;

namespace Test.VirtualRadar.Library.Presenter
{
    [TestClass]
    public class MainPresenterTests
    {
        #region TestContext, Fields, TestInitialise etc.
        public TestContext TestContext { get; set; }

        private IClassFactory _OriginalFactory;
        private IMainPresenter _Presenter;
        private Mock<IMainView> _View;
        private Mock<IMainPresenterProvider> _Provider;
        private Mock<IAutoConfigListener> _SelfConfiguringListener;
        private Mock<IListener> _Listener;
        private Mock<IWebServer> _WebServer;
        private Mock<IAutoConfigWebServer> _AutoConfigWebServer;
        private Mock<IUniversalPlugAndPlayManager> _UPnpManager;
        private Mock<IHeartbeatService> _HeartbeatService;
        private Configuration _Configuration;
        private Mock<IConfigurationStorage> _ConfigurationStorage;
        private Mock<INewVersionChecker> _NewVersionChecker;
        private EventRecorder<EventArgs> _HeartbeatTick;
        private Mock<ILog> _Log;
        private Mock<IBaseStationAircraftList> _BaseStationAircraftList;
        private Mock<IPluginManager> _PluginManager;
        private Mock<IRebroadcastServerManager> _RebroadcastServerManager;

        [TestInitialize]
        public void TestInitialise()
        {
            _OriginalFactory = Factory.TakeSnapshot();

            _ConfigurationStorage = TestUtilities.CreateMockSingleton<IConfigurationStorage>();
            _Configuration = new Configuration();
            _ConfigurationStorage.Setup(cs => cs.Load()).Returns(_Configuration);

            _Log = TestUtilities.CreateMockSingleton<ILog>();
            _HeartbeatService = TestUtilities.CreateMockSingleton<IHeartbeatService>();
            _WebServer = new Mock<IWebServer>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _AutoConfigWebServer = TestUtilities.CreateMockSingleton<IAutoConfigWebServer>();
            _AutoConfigWebServer.Setup(s => s.WebServer).Returns(_WebServer.Object);
            _UPnpManager = new Mock<IUniversalPlugAndPlayManager>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _NewVersionChecker = TestUtilities.CreateMockSingleton<INewVersionChecker>();
            _HeartbeatTick = new EventRecorder<EventArgs>();
            _BaseStationAircraftList = new Mock<IBaseStationAircraftList>();
            _PluginManager = TestUtilities.CreateMockSingleton<IPluginManager>();
            _RebroadcastServerManager = TestUtilities.CreateMockSingleton<IRebroadcastServerManager>();

            _SelfConfiguringListener = TestUtilities.CreateMockSingleton<IAutoConfigListener>();
            _Listener = new Mock<IListener>(MockBehavior.Default) { DefaultValue = DefaultValue.Mock };
            _SelfConfiguringListener.Setup(r => r.Listener).Returns(_Listener.Object);

            _Presenter = Factory.Singleton.Resolve<IMainPresenter>();

            _Provider = new Mock<IMainPresenterProvider>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _Provider.Setup(p => p.UtcNow).Returns(DateTime.UtcNow);
            _Presenter.Provider = _Provider.Object;

            _View = new Mock<IMainView>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Factory.RestoreSnapshot(_OriginalFactory);
        }
        #endregion

        #region Constructor and Properties
        [TestMethod]
        public void MainPresenter_Constructor_Initialises_To_Known_State_And_Properties_Work()
        {
            Assert.IsNull(_Presenter.View);
            Assert.IsNotNull(_Presenter.Provider);
            TestUtilities.TestProperty(_Presenter, r => r.Provider, _Presenter.Provider, _Provider.Object);
            TestUtilities.TestProperty(_Presenter, r => r.UPnpManager, null, _UPnpManager.Object);
        }

        [TestMethod]
        public void MainPresenter_BaseStationAircraftList_Sets_AircraftCount_On_View_If_Already_Initialised()
        {
            _BaseStationAircraftList.Setup(r => r.Count).Returns(7);

            _Presenter.Initialise(_View.Object);
            _Presenter.BaseStationAircraftList = _BaseStationAircraftList.Object;

            Assert.AreEqual(7, _View.Object.AircraftCount);
        }
        #endregion

        #region Initialise
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void MainPresenter_Initialise_Throws_If_View_IsNull()
        {
            _Presenter.Initialise(null);
        }

        [TestMethod]
        public void MainPresenter_Initialise_Sets_View_Property()
        {
            _Presenter.Initialise(_View.Object);
            Assert.AreSame(_View.Object, _Presenter.View);
        }

        [TestMethod]
        public void MainPresenter_Initialise_Sets_LogFileName_On_View()
        {
            string expectedFileName = @"c:\users\me\appsdata\local\vrs\log.txt";
            _Log.Setup(g => g.FileName).Returns(expectedFileName);

            _Presenter.Initialise(_View.Object);
            Assert.AreEqual(expectedFileName, _View.Object.LogFileName);
        }

        [TestMethod]
        public void MainPresenter_Initialise_Sets_InvalidPluginCount_On_View()
        {
            _PluginManager.Setup(p => p.IgnoredPlugins).Returns(new Dictionary<string, string>() { { "a", "a" }, { "b", "c" } });

            _Presenter.Initialise(_View.Object);

            Assert.AreEqual(2, _View.Object.InvalidPluginCount);
        }

        [TestMethod]
        public void MainPresenter_Initialise_Sets_AircraftCount_On_View()
        {
            _Presenter.BaseStationAircraftList = _BaseStationAircraftList.Object;
            _BaseStationAircraftList.Setup(r => r.Count).Returns(7);

            _Presenter.Initialise(_View.Object);

            Assert.AreEqual(7, _View.Object.AircraftCount);
        }

        [TestMethod]
        public void MainPresenter_Initialise_Calls_GuiThreadStartup_On_All_Plugins()
        {
            var plugin = new Mock<IPlugin>();
            _PluginManager.Setup(p => p.LoadedPlugins).Returns(new List<IPlugin>() { plugin.Object });

            _Presenter.Initialise(_View.Object);

            plugin.Verify(p => p.GuiThreadStartup(), Times.Once());
        }
        #endregion

        #region Listener
        [TestMethod]
        public void MainPresenter_Listener_Updates_Message_Count_On_View()
        {
            _Listener.Setup(r => r.TotalMessages).Returns(20L);
            _Presenter.Initialise(_View.Object);

            Assert.AreEqual(20L, _View.Object.BaseStationTotalMessages);

            _Listener.Setup(r => r.TotalMessages).Returns(900L);
            _HeartbeatService.Raise(s => s.FastTick += null, EventArgs.Empty);

            Assert.AreEqual(900L, _View.Object.BaseStationTotalMessages);
        }

        [TestMethod]
        public void MainPresenter_Listener_Updates_Bad_Message_Count_On_View()
        {
            _Listener.Setup(r => r.TotalBadMessages).Returns(20L);
            _Presenter.Initialise(_View.Object);

            Assert.AreEqual(20L, _View.Object.BaseStationTotalBadMessages);

            _Listener.Setup(r => r.TotalBadMessages).Returns(900L);
            _HeartbeatService.Raise(s => s.FastTick += null, EventArgs.Empty);

            Assert.AreEqual(900L, _View.Object.BaseStationTotalBadMessages);
        }

        [TestMethod]
        public void MainPresenter_Listener_Updates_ConnectionStatus_On_View()
        {
            _Listener.Setup(r => r.ConnectionStatus).Returns(ConnectionStatus.CannotConnect);
            _Presenter.Initialise(_View.Object);

            Assert.AreEqual(ConnectionStatus.CannotConnect, _View.Object.BaseStationConnectionStatus);

            _Listener.Setup(r => r.ConnectionStatus).Returns(ConnectionStatus.Reconnecting);
            _Listener.Raise(r => r.ConnectionStateChanged += null, EventArgs.Empty);

            Assert.AreEqual(ConnectionStatus.Reconnecting, _View.Object.BaseStationConnectionStatus);
        }
        #endregion

        #region WebServer
        [TestMethod]
        public void MainPresenter_WebServer_Initial_Online_State_Is_Copied_To_View()
        {
            foreach(var state in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _WebServer.Object.Online = state;
                _Presenter.Initialise(_View.Object);

                Assert.AreEqual(state, _View.Object.WebServerIsOnline);
            }
        }

        [TestMethod]
        public void MainPresenter_WebServer_Local_Address_Is_Copied_To_View()
        {
            _WebServer.Setup(s => s.LocalAddress).Returns("This is local");
            _Presenter.Initialise(_View.Object);

            Assert.AreEqual("This is local", _View.Object.WebServerLocalAddress);
        }

        [TestMethod]
        public void MainPresenter_WebServer_Network_Address_Is_Copied_To_View()
        {
            _WebServer.Setup(s => s.NetworkAddress).Returns("This is network");
            _Presenter.Initialise(_View.Object);

            Assert.AreEqual("This is network", _View.Object.WebServerNetworkAddress);
        }

        [TestMethod]
        public void MainPresenter_WebServer_External_Address_Is_Copied_To_View()
        {
            _WebServer.Setup(s => s.ExternalAddress).Returns("This is external");
            _Presenter.Initialise(_View.Object);

            Assert.AreEqual("This is external", _View.Object.WebServerExternalAddress);
        }

        [TestMethod]
        public void MainPresenter_WebServer_External_Address_Is_Updated_If_Server_Reports_A_Change()
        {
            _WebServer.Setup(s => s.ExternalAddress).Returns("Original");
            _Presenter.Initialise(_View.Object);

            _WebServer.Setup(s => s.ExternalAddress).Returns("New");

            Assert.AreNotEqual("New", _View.Object.WebServerExternalAddress);
            _WebServer.Raise(s => s.ExternalAddressChanged += null, EventArgs.Empty);
            Assert.AreEqual("New", _View.Object.WebServerExternalAddress);
        }

        [TestMethod]
        public void MainPresenter_WebServer_Reflects_Changes_In_Online_State_In_View()
        {
            foreach(var initialState in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _WebServer.Object.Online = initialState;
                _Presenter.Initialise(_View.Object);

                _WebServer.Object.Online = !initialState;
                _WebServer.Raise(s => s.OnlineChanged += null, EventArgs.Empty);

                Assert.AreEqual(!initialState, _View.Object.WebServerIsOnline);
            }
        }

        [TestMethod]
        public void MainPresenter_WebServer_Toggles_WebServer_State_On_User_Request()
        {
            foreach(var initialState in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _WebServer.Object.Online = initialState;
                _Presenter.Initialise(_View.Object);

                _View.Raise(e => e.ToggleServerStatus += null, EventArgs.Empty);

                Assert.AreEqual(!initialState, _WebServer.Object.Online);
            }
        }

        [TestMethod]
        public void MainPresenter_WebServer_Updates_Display_With_Information_About_Serviced_Requests()
        {
            _Presenter.Initialise(_View.Object);

            var args = new ResponseSentEventArgs("url goes here", "192.168.0.44:58301", "127.0.3.4", 10203, ContentClassification.Image, null, 0, 0);
            _WebServer.Raise(s => s.ResponseSent += null, args);
            _View.Verify(v => v.ShowWebRequestHasBeenServiced("192.168.0.44:58301", "url goes here", 10203), Times.Once());
        }
        #endregion

        #region RebroadcastServerManager
        [TestMethod]
        public void MainPresenter_RebroadcastServerManager_Configuration_Shown_On_Initialise()
        {
            _Configuration.RebroadcastSettings.Add(new RebroadcastSettings() { Enabled = true });

            _Presenter.Initialise(_View.Object);

            Assert.AreEqual(Describe.RebroadcastSettingsCollection(_Configuration.RebroadcastSettings), _View.Object.RebroadcastServersConfiguration);
        }

        [TestMethod]
        public void MainPresenter_RebroadcastServerManager_Configuration_Shown_On_Configuration_Update()
        {
            _Presenter.Initialise(_View.Object);

            _Configuration.RebroadcastSettings.Add(new RebroadcastSettings() { Enabled = true });
            _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);

            Assert.AreEqual(Describe.RebroadcastSettingsCollection(_Configuration.RebroadcastSettings), _View.Object.RebroadcastServersConfiguration);
        }

        [TestMethod]
        public void MainPresenter_RebroadcastServerManager_Updates_View_When_Client_Connects()
        {
            _Presenter.Initialise(_View.Object);

            var ipAddress = IPAddress.Parse("192.168.0.1");
            var ipEndPoint = new IPEndPoint(ipAddress, 50400);
            _RebroadcastServerManager.Raise(r => r.ClientConnected += null, new BroadcastEventArgs(ipEndPoint, 0, 30001, RebroadcastFormat.Port30003));

            _View.Verify(r => r.ShowRebroadcastClientServiced("192.168.0.1", 50400, 30001, RebroadcastFormat.Port30003, 0), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_RebroadcastServerManager_Updates_View_When_Bytes_Are_Sent_To_Client()
        {
            _Presenter.Initialise(_View.Object);

            var ipAddress = IPAddress.Parse("192.168.0.1");
            var ipEndPoint = new IPEndPoint(ipAddress, 50400);
            _RebroadcastServerManager.Raise(r => r.BroadcastSent += null, new BroadcastEventArgs(ipEndPoint, 1000, 30001, RebroadcastFormat.Port30003));

            _View.Verify(r => r.ShowRebroadcastClientServiced("192.168.0.1", 50400, 30001, RebroadcastFormat.Port30003, 1000), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_RebroadcastServerManager_Updates_View_When_Client_Disconnects()
        {
            _Presenter.Initialise(_View.Object);

            var ipAddress = IPAddress.Parse("192.168.0.1");
            var ipEndPoint = new IPEndPoint(ipAddress, 50400);
            _RebroadcastServerManager.Raise(r => r.ClientDisconnected += null, new BroadcastEventArgs(ipEndPoint, 0, 30001, RebroadcastFormat.Port30003));

            _View.Verify(r => r.ShowRebroadcastClientDisconnected("192.168.0.1", 50400), Times.Once());
        }
        #endregion

        #region BaseStation Aircraft List
        [TestMethod]
        public void MainPresenter_BaseStationAircraftList_Changes_To_Count_Are_Copied_To_View()
        {
            _Presenter.Initialise(_View.Object);
            _Presenter.BaseStationAircraftList = _BaseStationAircraftList.Object;

            _BaseStationAircraftList.Setup(b => b.Count).Returns(42);
            _BaseStationAircraftList.Raise(b => b.CountChanged += null, EventArgs.Empty);

            Assert.AreEqual(42, _View.Object.AircraftCount);
        }
        #endregion

        #region Universal Plug & Play Manager
        [TestMethod]
        public void MainPresenter_UPnpManager_Initial_Port_Forwarding_State_Is_Copied_To_View()
        {
            foreach(var state in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                _UPnpManager.Setup(m => m.PortForwardingPresent).Returns(state);
                _Presenter.UPnpManager = _UPnpManager.Object;
                Assert.AreEqual(state, _View.Object.UPnpPortForwardingActive);
            }
        }

        [TestMethod]
        public void MainPresenter_UPnpManager_Initial_Enabled_State_Is_Copied_To_View()
        {
            foreach(var state in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                _UPnpManager.Setup(m => m.IsEnabled).Returns(state);
                _Presenter.UPnpManager = _UPnpManager.Object;
                Assert.AreEqual(state, _View.Object.UPnpEnabled);
            }
        }

        [TestMethod]
        public void MainPresenter_UPnpManager_Initial_Router_Present_State_Is_Copied_To_View()
        {
            foreach(var state in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                _UPnpManager.Setup(m => m.IsRouterPresent).Returns(state);
                _Presenter.UPnpManager = _UPnpManager.Object;
                Assert.AreEqual(state, _View.Object.UPnpRouterPresent);
            }
        }

        [TestMethod]
        public void MainPresenter_UPnpManager_Port_Forwarding_State_Is_Updated_If_UPnpManager_Reports_Change()
        {
            _Presenter.Initialise(_View.Object);

            _UPnpManager.Setup(m => m.PortForwardingPresent).Returns(false);
            _Presenter.UPnpManager = _UPnpManager.Object;

            _UPnpManager.Setup(m => m.PortForwardingPresent).Returns(true);
            _UPnpManager.Raise(m => m.StateChanged += null, EventArgs.Empty);

            Assert.AreEqual(true, _View.Object.UPnpPortForwardingActive);
        }

        [TestMethod]
        public void MainPresenter_UPnpManager_Enabled_State_Is_Updated_If_UPnpManager_Reports_Change()
        {
            _Presenter.Initialise(_View.Object);

            _UPnpManager.Setup(m => m.IsEnabled).Returns(false);
            _Presenter.UPnpManager = _UPnpManager.Object;

            _UPnpManager.Setup(m => m.IsEnabled).Returns(true);
            _UPnpManager.Raise(m => m.StateChanged += null, EventArgs.Empty);

            Assert.AreEqual(true, _View.Object.UPnpEnabled);
        }

        [TestMethod]
        public void MainPresenter_UPnpManager_Router_Present_State_Is_Updated_If_UPnpManager_Reports_Change()
        {
            _Presenter.Initialise(_View.Object);

            _UPnpManager.Setup(m => m.IsRouterPresent).Returns(false);
            _Presenter.UPnpManager = _UPnpManager.Object;

            _UPnpManager.Setup(m => m.IsRouterPresent).Returns(true);
            _UPnpManager.Raise(m => m.StateChanged += null, EventArgs.Empty);

            Assert.AreEqual(true, _View.Object.UPnpRouterPresent);
        }

        [TestMethod]
        public void MainPresenter_UPnpManager_Toggles_Port_Forwarding_State_On_User_Request()
        {
            _UPnpManager.Setup(m => m.IsEnabled).Returns(true);
            _UPnpManager.Setup(m => m.IsRouterPresent).Returns(true);

            foreach(var initialState in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                _UPnpManager.Setup(m => m.PortForwardingPresent).Returns(initialState);
                _Presenter.UPnpManager = _UPnpManager.Object;
                _View.Raise(e => e.ToggleUPnpStatus += null, EventArgs.Empty);

                _UPnpManager.Verify(m => m.PutServerOntoInternet(), initialState ? Times.Never() : Times.Once());
                _UPnpManager.Verify(m => m.TakeServerOffInternet(), initialState ? Times.Once() : Times.Never());
            }
        }
        #endregion

        #region Version Checker
        [TestMethod]
        public void MainPresenter_VersionChecker_Invoked_When_Heartbeat_Makes_First_Tick()
        {
            _Presenter.Initialise(_View.Object);

            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Never());
            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Invoked_On_First_Heartbeat_After_Check_Period_Has_Elapsed()
        {
            _Presenter.Initialise(_View.Object);

            _Configuration.VersionCheckSettings.CheckPeriodDays = 10;

            DateTime now = new DateTime(2010, 1, 1);
            _Provider.Setup(p => p.UtcNow).Returns(now);

            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Once());

            _Provider.Setup(p => p.UtcNow).Returns(now.AddDays(9).AddHours(23).AddMinutes(59).AddSeconds(59).AddMilliseconds(999));
            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Once());

            _Provider.Setup(p => p.UtcNow).Returns(now.AddDays(10));
            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Exactly(2));
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Not_Invoked_If_CheckPeriodDays_Is_Too_Low()
        {
            _Presenter.Initialise(_View.Object);

            _Configuration.VersionCheckSettings.CheckPeriodDays = 0;

            DateTime now = new DateTime(2010, 1, 1);
            _Provider.Setup(p => p.UtcNow).Returns(now);

            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Never());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Not_Invoked_If_Automatic_Updating_Is_Prohibited()
        {
            _Presenter.Initialise(_View.Object);

            _Configuration.VersionCheckSettings.CheckAutomatically = false;

            DateTime now = new DateTime(2010, 1, 1);
            _Provider.Setup(p => p.UtcNow).Returns(now);

            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Never());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Not_Invoked_If_Automatic_Updating_Is_Prohibited_After_First_Tick()
        {
            _Presenter.Initialise(_View.Object);

            _Configuration.VersionCheckSettings.CheckPeriodDays = 10;

            DateTime now = new DateTime(2010, 1, 1);
            _Provider.Setup(p => p.UtcNow).Returns(now);

            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Once());

            _Configuration.VersionCheckSettings.CheckAutomatically = false;
            _ConfigurationStorage.Raise(c => c.ConfigurationChanged += null, EventArgs.Empty);

            _Provider.Setup(p => p.UtcNow).Returns(now.AddDays(10));
            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Not_Invoked_If_Automatic_Updating_Is_Permitted_After_First_Tick()
        {
            _Presenter.Initialise(_View.Object);

            _Configuration.VersionCheckSettings.CheckAutomatically = false;
            _Configuration.VersionCheckSettings.CheckPeriodDays = 10;

            DateTime now = new DateTime(2010, 1, 1);
            _Provider.Setup(p => p.UtcNow).Returns(now);

            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Never());

            _Configuration.VersionCheckSettings.CheckAutomatically = true;
            _ConfigurationStorage.Raise(c => c.ConfigurationChanged += null, EventArgs.Empty);

            _Provider.Setup(p => p.UtcNow).Returns(now.AddDays(10));
            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Logs_Exceptions()
        {
            _Presenter.Initialise(_View.Object);

            var exception = new InvalidOperationException("Exception text here");
            _Log.Verify(o => o.WriteLine(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _NewVersionChecker.Setup(c => c.CheckForNewVersion()).Callback(() => { throw exception; });

            _HeartbeatService.Raise(h => h.SlowTick += null, EventArgs.Empty);

            _Log.Verify(o => o.WriteLine(It.IsAny<string>(), exception.ToString()), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Sets_View_If_New_Version_Is_Available()
        {
            _Presenter.Initialise(_View.Object);

            Assert.IsFalse(_View.Object.NewVersionAvailable);
            _NewVersionChecker.Setup(c => c.IsNewVersionAvailable).Returns(true);
            _NewVersionChecker.Raise(c => c.NewVersionAvailable += null, EventArgs.Empty);
            Assert.IsTrue(_View.Object.NewVersionAvailable);
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Sets_DownloadUrl()
        {
            _Presenter.Initialise(_View.Object);

            Assert.IsNull(_View.Object.NewVersionDownloadUrl);
            _NewVersionChecker.Setup(c => c.DownloadUrl).Returns("My url");
            _NewVersionChecker.Raise(c => c.NewVersionAvailable += null, EventArgs.Empty);
            Assert.AreEqual("My url", _View.Object.NewVersionDownloadUrl);
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Can_Be_Invoked_Manually()
        {
            _Presenter.Initialise(_View.Object);

            _View.Raise(v => v.CheckForNewVersion += null, EventArgs.Empty);
            _NewVersionChecker.Verify(c => c.CheckForNewVersion(), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Shows_Wait_Cursor_When_Invoked_Manually()
        {
            _Presenter.Initialise(_View.Object);

            object previousState = new object();
            _View.Setup(v => v.ShowBusy(true, null)).Returns(previousState);
            _NewVersionChecker.Setup(c => c.CheckForNewVersion()).Callback(() => _View.Verify(v => v.ShowBusy(true, null), Times.Once()));

            _View.Raise(v => v.CheckForNewVersion += null, EventArgs.Empty);
            _View.Verify(v => v.ShowBusy(true, null), Times.Once());
            _View.Verify(v => v.ShowBusy(false, previousState), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Removes_Wait_Cursor_When_Invoked_Manually_Even_If_Exception_Occurs()
        {
            _Presenter.Initialise(_View.Object);

            object previousState = new object();
            _View.Setup(v => v.ShowBusy(true, null)).Returns(previousState);
            _NewVersionChecker.Setup(c => c.CheckForNewVersion()).Callback(() => { throw new InvalidOperationException(); });

            try {
                _View.Raise(v => v.CheckForNewVersion += null, EventArgs.Empty);
            } catch(InvalidOperationException) {
            }

            _View.Verify(v => v.ShowBusy(false, previousState), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_VersionChecker_Shows_Result_Of_Manual_Check()
        {
            _Presenter.Initialise(_View.Object);

            _NewVersionChecker.Setup(c => c.CheckForNewVersion()).Returns(true);
            _View.Raise(v => v.CheckForNewVersion += null, EventArgs.Empty);
            _View.Verify(v => v.ShowManualVersionCheckResult(true), Times.Once());
            _View.Verify(v => v.ShowManualVersionCheckResult(false), Times.Never());

            _NewVersionChecker.Setup(c => c.CheckForNewVersion()).Returns(false);
            _View.Raise(v => v.CheckForNewVersion += null, EventArgs.Empty);
            _View.Verify(v => v.ShowManualVersionCheckResult(true), Times.Once());
            _View.Verify(v => v.ShowManualVersionCheckResult(false), Times.Once());
        }
        #endregion

        #region Reconnect To BaseStation
        [TestMethod]
        public void MainPresenter_ReconnectToBaseStation_Disconnects_And_Then_Reconnects()
        {
            _Presenter.Initialise(_View.Object);

            _View.Raise(v => v.ReconnectToBaseStationClicked += null, EventArgs.Empty);

            _Listener.Verify(v => v.Disconnect(), Times.Once());
            _Listener.Verify(v => v.Connect(false), Times.Once());
        }

        [TestMethod]
        public void MainPresenter_ReconnectToBaseStation_Ignores_AutoReconnectAtStartup_Setting()
        {
            _Presenter.Initialise(_View.Object);

            _Configuration.BaseStationSettings.AutoReconnectAtStartup = true;

            _View.Raise(v => v.ReconnectToBaseStationClicked += null, EventArgs.Empty);

            _Listener.Verify(v => v.Disconnect(), Times.Once());
            _Listener.Verify(v => v.Connect(false), Times.Once());
        }
        #endregion
    }
}
