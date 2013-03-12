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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using InterfaceFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Test.Framework;
using VirtualRadar.Interface;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Database;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.StandingData;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Interface.WebSite;

namespace Test.VirtualRadar.WebSite
{
    [TestClass]
    public partial class WebSiteTests
    {
        #region TestContext, Fields, TestInitialise etc.
        public TestContext TestContext { get; set; }

        private IClassFactory _OriginalContainer;

        private bool _AutoAttached = false;
        private IWebSite _WebSite;
        private Mock<IWebSiteProvider> _Provider;
        private Mock<IWebServer> _WebServer;
        private Mock<IRequest> _Request;
        private Mock<IResponse> _Response;
        private MemoryStream _OutputStream;
        private Mock<IInstallerSettingsStorage> _InstallerSettingsStorage;
        private InstallerSettings _InstallerSettings;
        private Mock<IConfigurationStorage> _ConfigurationStorage;
        private Configuration _Configuration;
        private Mock<IBaseStationAircraftList> _BaseStationAircraftList;
        private Mock<ISimpleAircraftList> _FlightSimulatorAircraftList;
        private List<IAircraft> _BaseStationAircraft;
        private List<IAircraft> _FlightSimulatorAircraft;
        private AircraftListAddress _AircraftListAddress;
        private AircraftListFilter _AircraftListFilter;
        private ReportRowsAddress _ReportRowsAddress;
        private Mock<IBaseStationDatabase> _BaseStationDatabase;
        private Mock<IAutoConfigBaseStationDatabase> _AutoConfigBaseStationDatabase;
        private Mock<IImageFileManager> _ImageFileManager;
        private Mock<IAudio> _Audio;
        private List<BaseStationFlight> _DatabaseFlights;
        private BaseStationAircraft _DatabaseAircraft;
        private List<BaseStationFlight> _DatabaseFlightsForAircraft;
        private Mock<IAircraftPictureManager> _AircraftPictureManager;
        private Mock<IStandingDataManager> _StandingDataManager;
        private Mock<ILog> _Log;
        private Mock<IApplicationInformation> _ApplicationInformation;
        private Mock<IAutoConfigPictureFolderCache> _AutoConfigPictureFolderCache;
        private Mock<IDirectoryCache> _DirectoryCache;
        private Mock<IRuntimeEnvironment> _RuntimeEnvironment;

        // The named colours (Black, Green etc.) don't compare well to the colors returned by Bitmap.GetPixel - e.g.
        // Color.Black == new Color(0, 0, 0) is false even though the ARGB values are equal. Further Color.Green isn't
        // new Color(0, 255, 0). So we declare our own versions of the colours here to make life easier.
        private Color _Black = Color.FromArgb(0, 0, 0);
        private Color _White = Color.FromArgb(255, 255, 255);
        private Color _Red = Color.FromArgb(255, 0, 0);
        private Color _Green = Color.FromArgb(0, 255, 0);
        private Color _Blue = Color.FromArgb(0, 0, 255);
        private Color _Transparent = Color.FromArgb(0, 0, 0, 0);

        [TestInitialize]
        public void TestInitialise()
        {
            _OriginalContainer = Factory.TakeSnapshot();

            _WebServer = new Mock<IWebServer>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();

            _Request = new Mock<IRequest>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _Response = new Mock<IResponse>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();

            _Request.Setup(m => m.ContentLength64).Returns(() => { return _Request.Object.InputStream == null ? 0L : _Request.Object.InputStream.Length; });

            _OutputStream = new MemoryStream();
            _Response.Setup(m => m.OutputStream).Returns(_OutputStream);

            _InstallerSettingsStorage = TestUtilities.CreateMockImplementation<IInstallerSettingsStorage>();
            _InstallerSettings = new InstallerSettings();
            _InstallerSettingsStorage.Setup(m => m.Load()).Returns(_InstallerSettings);

            _ConfigurationStorage = TestUtilities.CreateMockSingleton<IConfigurationStorage>();
            _Configuration = new Configuration();
            _ConfigurationStorage.Setup(m => m.Load()).Returns(_Configuration);

            _RuntimeEnvironment = TestUtilities.CreateMockSingleton<IRuntimeEnvironment>();
            _RuntimeEnvironment.Setup(r => r.IsMono).Returns(false);

            _BaseStationAircraftList = new Mock<IBaseStationAircraftList>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _BaseStationAircraft = new List<IAircraft>();
            long o1, o2;
            _BaseStationAircraftList.Setup(m => m.TakeSnapshot(out o1, out o2)).Returns(_BaseStationAircraft);

            _FlightSimulatorAircraftList = new Mock<ISimpleAircraftList>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _FlightSimulatorAircraft = new List<IAircraft>();
            long of1, of2;
            _FlightSimulatorAircraftList.Setup(m => m.TakeSnapshot(out of1, out of2)).Returns(_FlightSimulatorAircraft);

            _AircraftListAddress = new AircraftListAddress(_Request);
            _AircraftListFilter = new AircraftListFilter();

            _ReportRowsAddress = new ReportRowsAddress();
            _ApplicationInformation = TestUtilities.CreateMockImplementation<IApplicationInformation>();

            _BaseStationDatabase = new Mock<IBaseStationDatabase>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _AutoConfigBaseStationDatabase = TestUtilities.CreateMockSingleton<IAutoConfigBaseStationDatabase>();
            _AutoConfigBaseStationDatabase.Setup(a => a.Database).Returns(_BaseStationDatabase.Object);

            _DatabaseFlights = new List<BaseStationFlight>();
            _BaseStationDatabase.Setup(db => db.GetCountOfFlights(It.IsAny<SearchBaseStationCriteria>())).Returns(_DatabaseFlights.Count);
            _BaseStationDatabase.Setup(db => db.GetFlights(It.IsAny<SearchBaseStationCriteria>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(_DatabaseFlights);
            _DatabaseFlightsForAircraft = new List<BaseStationFlight>();
            _BaseStationDatabase.Setup(db => db.GetCountOfFlightsForAircraft(It.IsAny<BaseStationAircraft>(), It.IsAny<SearchBaseStationCriteria>())).Returns(_DatabaseFlightsForAircraft.Count);
            _BaseStationDatabase.Setup(db => db.GetFlightsForAircraft(It.IsAny<BaseStationAircraft>(), It.IsAny<SearchBaseStationCriteria>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(_DatabaseFlightsForAircraft);
            _DatabaseAircraft = new BaseStationAircraft();

            _ImageFileManager = TestUtilities.CreateMockImplementation<IImageFileManager>();
            _ImageFileManager.Setup(i => i.LoadFromFile(It.IsAny<string>())).Returns((string fileName) => {
                return Bitmap.FromFile(fileName);
            });

            _Audio = TestUtilities.CreateMockImplementation<IAudio>();
            _AircraftPictureManager = TestUtilities.CreateMockSingleton<IAircraftPictureManager>();
            _AutoConfigPictureFolderCache = TestUtilities.CreateMockSingleton<IAutoConfigPictureFolderCache>();
            _DirectoryCache = new Mock<IDirectoryCache>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _AutoConfigPictureFolderCache.Setup(a => a.DirectoryCache).Returns(_DirectoryCache.Object);
            _StandingDataManager = TestUtilities.CreateMockSingleton<IStandingDataManager>();
            _Log = TestUtilities.CreateMockSingleton<ILog>();

            // Initialise this last, just in case the constructor uses any of the mocks
            _WebSite = Factory.Singleton.Resolve<IWebSite>();
            _WebSite.BaseStationAircraftList = _BaseStationAircraftList.Object;
            _WebSite.FlightSimulatorAircraftList = _FlightSimulatorAircraftList.Object;
            _WebSite.BaseStationDatabase = _BaseStationDatabase.Object;
            _WebSite.StandingDataManager = _StandingDataManager.Object;
            _AutoAttached = false;

            _Provider = new Mock<IWebSiteProvider>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _Provider.Setup(m => m.UtcNow).Returns(DateTime.UtcNow);
            _Provider.Setup(m => m.DirectoryExists(It.IsAny<string>())).Returns((string folder) => {
                switch(folder.ToUpper()) {
                    case null:          throw new ArgumentNullException();
                    case "NOTEXISTS":   return false;
                    default:            return true;
                }
            });
            _WebSite.Provider = _Provider.Object;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Factory.RestoreSnapshot(_OriginalContainer);
            if(_OutputStream != null) _OutputStream.Dispose();
            _OutputStream = null;
        }
        #endregion

        #region Helper Methods - Attach, SendRequest, SendJsonRequest
        /// <summary>
        /// Calls AttachToServer on _WebSite, but only if it has not already been called for this test.
        /// </summary>
        private void Attach()
        {
            if(!_AutoAttached) {
                _WebSite.AttachSiteToServer(_WebServer.Object);
                _AutoAttached = true;
            }
        }

        /// <summary>
        /// Attaches to the webServer with <see cref="Attach"/> and then raises an event on the webServer to simulate a request coming
        /// in for the path and file specified. The request is not flagged as coming from the Internet.
        /// </summary>
        /// <param name="pathAndFile"></param>
        private void SendRequest(string pathAndFile)
        {
            SendRequest(pathAndFile, false);
        }

        /// <summary>
        /// Attaches to the webServer with <see cref="Attach"/> and then raises an event on the webServer to simulate a request coming
        /// in for the path and file specified and with the origin optionally coming from the Internet.
        /// </summary>
        /// <param name="pathAndFile"></param>
        /// <param name="isInternetClient"></param>
        private void SendRequest(string pathAndFile, bool isInternetClient)
        {
            Attach();
            _WebServer.Raise(m => m.RequestReceived += null, RequestReceivedEventArgsHelper.Create(_Request, _Response, pathAndFile, isInternetClient));
        }

        /// <summary>
        /// Attaches to the webServer with <see cref="Attach"/> and then raises an event on the webServer to simulate a request for a
        /// JSON file. The output is parsed into a JSON file of the type specified.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pathAndFile"></param>
        /// <param name="isInternetClient"></param>
        /// <param name="jsonPCallback"></param>
        /// <returns></returns>
        private T SendJsonRequest<T>(string pathAndFile, bool isInternetClient = false, string jsonPCallback = null)
            where T: class
        {
            return (T)SendJsonRequest(typeof(T), pathAndFile, isInternetClient, jsonPCallback);
        }

        /// <summary>
        /// Attaches to the webServer with <see cref="Attach"/> and then raises an event on the webServer to simulate a request for a
        /// JSON file. The output is parsed into a JSON file of the type specified.
        /// </summary>
        /// <param name="jsonType"></param>
        /// <param name="pathAndFile"></param>
        /// <param name="isInternetClient"></param>
        /// <param name="jsonPCallback"></param>
        /// <returns></returns>
        private object SendJsonRequest(Type jsonType, string pathAndFile, bool isInternetClient = false, string jsonPCallback = null)
        {
            _OutputStream.SetLength(0);
            SendRequest(pathAndFile, isInternetClient);

            DataContractJsonSerializer serialiser = new DataContractJsonSerializer(jsonType);
            object result = null;

            var text = Encoding.UTF8.GetString(_OutputStream.ToArray());
            if(!String.IsNullOrEmpty(text)) {
                if(jsonPCallback != null) {
                    var jsonpStart = String.Format("{0}(", jsonPCallback);
                    var jsonpEnd = ")";
                    Assert.IsTrue(text.StartsWith(jsonpStart), text);
                    Assert.IsTrue(text.EndsWith(jsonpEnd), text);

                    text = text.Substring(jsonpStart.Length, text.Length - (jsonpStart.Length + jsonpEnd.Length));
                }

                try {
                    using(var stream = new MemoryStream(Encoding.UTF8.GetBytes(text))) {
                        result = serialiser.ReadObject(stream);
                    }
                } catch(Exception ex) {
                    Assert.Fail(@"Could not parse output stream into JSON file: {0}, text was ""{1}""", ex.Message, text);
                }
            }

            return result;
        }

        private void EnsureFileDoesNotExist(string fileName)
        {
            var path = Path.Combine(TestContext.TestDeploymentDir, fileName);
            if(File.Exists(path)) File.Delete(path);
        }

        private void AssertFilesAreIdentical(string fileName, byte[] expectedContent, string message = "")
        {
            var actualContent = File.ReadAllBytes(Path.Combine(TestContext.TestDeploymentDir, fileName));
            Assert.IsTrue(expectedContent.SequenceEqual(actualContent), message);
        }
        #endregion

        #region Helper Methods - AddBlankDatabaseFlights
        private void AddBlankDatabaseFlights(int count)
        {
            for(var i = 0;i < count;++i) {
                var flight = new BaseStationFlight();
                flight.FlightID = i + 1;

                var aircraft = new BaseStationAircraft();
                aircraft.AircraftID = i + 101;

                flight.Aircraft = aircraft;
                _DatabaseFlights.Add(flight);
            }
        }

        private void AddBlankDatabaseFlightsForAircraft(int count)
        {
            for(var i = 0;i < count;++i) {
                var flight = new BaseStationFlight();
                flight.FlightID = i + 1;
                flight.Aircraft = _DatabaseAircraft;

                _DatabaseFlightsForAircraft.Add(flight);
            }
        }
        #endregion

        #region Constructors and Properties
        [TestMethod]
        public void WebSite_Constructor_Initialises_To_Known_State_And_Properties_Work()
        {
            _WebSite = Factory.Singleton.Resolve<IWebSite>();
            Assert.IsNull(_WebSite.WebServer);
            Assert.IsNotNull(_WebSite.Provider);

            TestUtilities.TestProperty(_WebSite, "Provider", _WebSite.Provider, _Provider.Object);
            TestUtilities.TestProperty(_WebSite, "BaseStationAircraftList", null, _BaseStationAircraftList.Object);
            TestUtilities.TestProperty(_WebSite, "BaseStationDatabase", null, _BaseStationDatabase.Object);
            TestUtilities.TestProperty(_WebSite, "FlightSimulatorAircraftList", null, _FlightSimulatorAircraftList.Object);
            TestUtilities.TestProperty(_WebSite, "StandingDataManager", null, _StandingDataManager.Object);
        }
        #endregion

        #region AttachSiteToServer
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WebSite_AttachSiteToServer_Throws_If_Passed_Null()
        {
            _WebSite.AttachSiteToServer(null);
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Server_Property()
        {
            _WebSite.AttachSiteToServer(_WebServer.Object);
            Assert.AreSame(_WebServer.Object, _WebSite.WebServer);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void WebSite_AttachSiteToServer_Can_Only_Be_Called_Once()
        {
            _WebSite.AttachSiteToServer(_WebServer.Object);
            _WebSite.AttachSiteToServer(new Mock<IWebServer>().Object);
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Copes_If_Attached_To_The_Same_Server_Twice()
        {
            _WebSite.AttachSiteToServer(_WebServer.Object);
            _WebSite.AttachSiteToServer(_WebServer.Object);
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Server_Root()
        {
            _WebSite.AttachSiteToServer(_WebServer.Object);
            Assert.AreEqual("/VirtualRadar", _WebServer.Object.Root);
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Server_Port()
        {
            _InstallerSettings.WebServerPort = 9876;
            _WebSite.AttachSiteToServer(_WebServer.Object);
            Assert.AreEqual(9876, _WebServer.Object.Port);
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Correct_Authentication_On_Server()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Anonymous;
            _WebSite.AttachSiteToServer(_WebServer.Object);
            Assert.AreEqual(AuthenticationSchemes.Anonymous, _WebServer.Object.AuthenticationScheme);
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Correct_Map_Mode_For_GoogleMap_html()
        {
            SendRequest("/GoogleMap.html");

            string content = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsFalse(content.Contains("__MAP_MODE__"));
            Assert.IsTrue(content.Contains("var _MapMode = MapMode.normal;"));
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Correct_Map_Mode_For_FlightSim_html()
        {
            SendRequest("/FlightSim.html");

            string content = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsFalse(content.Contains("__MAP_MODE__"));
            Assert.IsTrue(content.Contains("var _MapMode = MapMode.flightSim;"));
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Correct_Name_For_RegReport_html()
        {
            SendRequest("/RegReport.html");

            string content = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsFalse(content.Contains("%%NAME%%"));
            Assert.IsTrue(content.Contains("<script type=\"text/javascript\" src=\"RegReport.js\">"));
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Correct_Name_For_IcaoReport_html()
        {
            SendRequest("/IcaoReport.html");

            string content = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsFalse(content.Contains("%%NAME%%"));
            Assert.IsTrue(content.Contains("<script type=\"text/javascript\" src=\"IcaoReport.js\">"));
        }

        [TestMethod]
        public void WebSite_AttachSiteToServer_Sets_Correct_Name_For_DateReport_html()
        {
            SendRequest("/DateReport.html");

            string content = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsFalse(content.Contains("%%NAME%%"));
            Assert.IsTrue(content.Contains("<script type=\"text/javascript\" src=\"DateReport.js\">"));
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AttachSiteToServer$")]
        public void WebSite_AttachSiteToServer_Causes_Pages_To_Be_Served_For_Correct_Addresses()
        {
            ExcelWorksheetData worksheet = new ExcelWorksheetData(TestContext);
            string pathAndFile = worksheet.String("PathAndFile");

            _WebSite.AttachSiteToServer(_WebServer.Object);

            var args = RequestReceivedEventArgsHelper.Create(_Request, _Response, pathAndFile, false);
            _WebServer.Raise(m => m.RequestReceived += null, args);
            Assert.AreEqual(true, args.Handled, pathAndFile);
            Assert.AreEqual(worksheet.String("MimeType"), _Response.Object.MimeType, pathAndFile);
            Assert.AreEqual(worksheet.ParseEnum<ContentClassification>("Classification"), args.Classification);

            args = RequestReceivedEventArgsHelper.Create(_Request, _Response, pathAndFile.ToLower(), false);
            _WebServer.Raise(m => m.RequestReceived += null, args);
            Assert.AreEqual(true, args.Handled, "Lowercase version", pathAndFile);

            args = RequestReceivedEventArgsHelper.Create(_Request, _Response, pathAndFile.ToUpper(), false);
            _WebServer.Raise(m => m.RequestReceived += null, args);
            Assert.AreEqual(true, args.Handled, "Uppercase version", pathAndFile);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AttachSiteToServer$")]
        public void WebSite_AttachSiteToServer_Only_Hooks_The_RequestReceived_Event()
        {
            ExcelWorksheetData worksheet = new ExcelWorksheetData(TestContext);
            string pathAndFile = worksheet.String("PathAndFile");

            _WebSite.AttachSiteToServer(_WebServer.Object);

            var args = RequestReceivedEventArgsHelper.Create(_Request, _Response, pathAndFile, false);

            _WebServer.Raise(m => m.BeforeRequestReceived += null, args);
            Assert.AreEqual(false, args.Handled, pathAndFile);

            _WebServer.Raise(m => m.AfterRequestReceived += null, args);
            Assert.AreEqual(false, args.Handled, pathAndFile);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AttachSiteToServer$")]
        public void WebSite_AttachSiteToServer_Does_Not_Send_More_Data_On_Requests_That_Have_Already_Been_Handled()
        {
            ExcelWorksheetData worksheet = new ExcelWorksheetData(TestContext);
            string pathAndFile = worksheet.String("PathAndFile");

            _WebSite.AttachSiteToServer(_WebServer.Object);

            var args = RequestReceivedEventArgsHelper.Create(_Request, _Response, pathAndFile, false);
            args.Handled = true;

            _WebServer.Raise(m => m.RequestReceived += null, args);
            Assert.AreEqual(0, _OutputStream.Length);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "DefaultPage$")]
        public void WebSite_AttachSiteToServer_Causes_Correct_Default_Page_To_Be_Served()
        {
            ExcelWorksheetData worksheet = new ExcelWorksheetData(TestContext);

            _WebSite.AttachSiteToServer(_WebServer.Object);
            _WebServer.Object.Root = worksheet.String("Root");

            var endPoint = new IPEndPoint(IPAddress.Parse("192.168.0.200"), 1234);
            var url = new Uri(worksheet.String("Url"));
            _Request.Setup(r => r.RemoteEndPoint).Returns(endPoint);
            _Request.Setup(r => r.Url).Returns(url);
            _Request.Setup(r => r.RawUrl).Returns(worksheet.String("RawUrl"));

            if(worksheet.Bool("IsAndroid")) RequestReceivedEventArgsHelper.SetAndroidUserAgent(_Request);
            if(worksheet.Bool("IsIPad")) RequestReceivedEventArgsHelper.SetIPadUserAgent(_Request);
            if(worksheet.Bool("IsIPhone")) RequestReceivedEventArgsHelper.SetIPhoneUserAgent(_Request);
            if(worksheet.Bool("IsIPod")) RequestReceivedEventArgsHelper.SetIPodUserAgent(_Request);

            var args = new RequestReceivedEventArgs(_Request.Object, _Response.Object, _WebServer.Object.Root);
            _WebServer.Raise(m => m.RequestReceived += null, args);
            Assert.AreEqual(worksheet.Bool("Handled"), args.Handled);

            if(worksheet.String("RedirectUrl") == null) _Response.Verify(r => r.Redirect(It.IsAny<string>()), Times.Never());
            else {
                _Response.Verify(r => r.Redirect(It.IsAny<string>()), Times.Once());
                _Response.Verify(r => r.Redirect(worksheet.String("RedirectUrl")), Times.Once());
                Assert.AreEqual(0L, _OutputStream.Length);
            }
        }
        #endregion

        #region Authentication
        [TestMethod]
        public void WebSite_Authentication_Accepts_Basic_Authentication_Credentials_From_Configuration()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs("Deckard", "B26354");

            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsTrue(args.IsAuthenticated);
            Assert.IsTrue(args.IsHandled);
        }

        [TestMethod]
        public void WebSite_Authentication_Rejects_Wrong_User_For_Basic_Authentication()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs("Zhora", "B26354");

            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsFalse(args.IsAuthenticated);
            Assert.IsTrue(args.IsHandled);
        }

        [TestMethod]
        public void WebSite_Authentication_Rejects_Null_User_For_Basic_Authentication()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs(null, "B26354");

            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsFalse(args.IsAuthenticated);
            Assert.IsTrue(args.IsHandled);
        }

        [TestMethod]
        public void WebSite_Authentication_Basic_Authentication_User_Is_Not_Case_Sensitive()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs("DECKARD", "B26354");

            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsTrue(args.IsAuthenticated);
            Assert.IsTrue(args.IsHandled);
        }

        [TestMethod]
        public void WebSite_Authentication_Rejects_Wrong_Password_For_Basic_Authentication()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs("Deckard", "b26354");

            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsFalse(args.IsAuthenticated);
            Assert.IsTrue(args.IsHandled);
        }

        [TestMethod]
        public void WebSite_Authentication_Ignores_Unknown_Authentication_Schemes()
        {
            foreach(AuthenticationSchemes scheme in Enum.GetValues(typeof(AuthenticationSchemes))) {
                if(scheme == AuthenticationSchemes.Basic) continue;

                _Configuration.WebServerSettings.AuthenticationScheme = scheme;
                _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
                _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
                _WebSite.AttachSiteToServer(_WebServer.Object);
                var args = new AuthenticationRequiredEventArgs("Deckard", "B26354");

                _WebServer.Raise(m => m.AuthenticationRequired += null, args);

                Assert.IsFalse(args.IsAuthenticated);
                Assert.IsFalse(args.IsHandled);
            }
        }

        [TestMethod]
        public void WebSite_Authentication_Ignores_Events_That_Have_Already_Been_Handled()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs("Deckard", "B26354") { IsHandled = true };

            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsFalse(args.IsAuthenticated);
        }
        #endregion

        #region Configuration changes
        [TestMethod]
        public void WebSite_Configuration_Changes_Do_Not_Crash_WebSite_If_Server_Not_Attached()
        {
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
        }

        [TestMethod]
        public void WebSite_Configuration_Picks_Up_Change_In_Basic_Authentication_User()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "JFSebastian";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs("Deckard", "B26354");
            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            args.IsHandled = false;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsTrue(args.IsAuthenticated);
        }

        [TestMethod]
        public void WebSite_Configuration_Picks_Up_Change_In_Basic_Authentication_Password()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _Configuration.WebServerSettings.BasicAuthenticationUser = "Deckard";
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("16417");
            _WebSite.AttachSiteToServer(_WebServer.Object);
            var args = new AuthenticationRequiredEventArgs("Deckard", "B26354");
            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            args.IsHandled = false;
            _Configuration.WebServerSettings.BasicAuthenticationPasswordHash = new Hash("B26354");
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            _WebServer.Raise(m => m.AuthenticationRequired += null, args);

            Assert.IsTrue(args.IsAuthenticated);
        }

        [TestMethod]
        public void WebSite_Configuration_Picks_Up_Change_In_Authentication_Scheme()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Anonymous;
            _WebSite.AttachSiteToServer(_WebServer.Object);

            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);

            Assert.AreEqual(AuthenticationSchemes.Basic, _WebServer.Object.AuthenticationScheme);
        }

        [TestMethod]
        public void WebSite_Configuration_Restarts_Server_If_Authentication_Scheme_Changes()
        {
            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Anonymous;
            _WebSite.AttachSiteToServer(_WebServer.Object);
            _WebServer.Object.Online = true;

            _Configuration.WebServerSettings.AuthenticationScheme = AuthenticationSchemes.Basic;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);

            _WebServer.VerifySet(m => m.Online = false, Times.Once());
            _WebServer.VerifySet(m => m.Online = true, Times.Exactly(2));
        }

        [TestMethod]
        public void WebSite_IsMono_Correctly_Set_In_ServerConfig_Js_When_Running_Under_Mono()
        {
            _RuntimeEnvironment.Setup(r => r.IsMono).Returns(true);
            SendRequest("/ServerConfig.js");

            string output = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.AreNotEqual(-1, output.IndexOf("this.isMono = true;"));
        }

        [TestMethod]
        public void WebSite_IsMono_Correctly_Set_In_ServerConfig_Js_When_Running_Under_DotNet()
        {
            _RuntimeEnvironment.Setup(r => r.IsMono).Returns(false);
            SendRequest("/ServerConfig.js");

            string output = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.AreNotEqual(-1, output.IndexOf("this.isMono = false;"));
        }

        [TestMethod]
        public void WebSite_Configuration_VrsVersion_Set_In_ServerConfig_Js()
        {
            _ApplicationInformation.Setup(i => i.ShortVersion).Returns("1.2.3");
            SendRequest("/ServerConfig.js");

            string content = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsFalse(content.Contains("__VRS_VERSION"));
            Assert.IsTrue(content.Contains("vrsVersion = '1.2.3';"));
        }

        [TestMethod]
        public void WebSite_Configuration_Is_Reflected_In_Output_From_ReportMap_Js()
        {
            SendRequest("/ReportMap.js");

            string output = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsFalse(output.Contains("__"));
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "SubstituteConfiguration$")]
        public void WebSite_Configuration_Changes_Correctly_Substituted_Into_Web_Pages()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            _WebSite.AttachSiteToServer(_WebServer.Object);

            var configObjectProperty = _Configuration.GetType().GetProperty(worksheet.String("ConfigObject"));
            var configObject = configObjectProperty.GetValue(_Configuration, null);
            var configProperty = configObjectProperty.PropertyType.GetProperty(worksheet.String("ConfigProperty"));
            var value = worksheet.String("Value");
            object setValue = null;
            if(value.StartsWith("DistanceUnit")) setValue = (DistanceUnit)Enum.Parse(typeof(DistanceUnit), value.Substring(value.IndexOf('.') + 1));
            else if(value.StartsWith("HeightUnit")) setValue = (HeightUnit)Enum.Parse(typeof(HeightUnit), value.Substring(value.IndexOf('.') + 1));
            else if(value.StartsWith("SpeedUnit")) setValue = (SpeedUnit)Enum.Parse(typeof(SpeedUnit), value.Substring(value.IndexOf('.') + 1));
            else setValue = TestUtilities.ChangeType(value, configProperty.PropertyType, CultureInfo.InvariantCulture);
            configProperty.SetValue(configObject, setValue, null);

            using(var switcher = new CultureSwitcher(worksheet.String("Culture"))) {
                _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            }

            _WebServer.Raise(m => m.RequestReceived += null, RequestReceivedEventArgsHelper.Create(_Request, _Response, worksheet.String("PathAndFile"), false));

            var output = Encoding.UTF8.GetString(_OutputStream.ToArray());
            Assert.IsTrue(output.Contains(worksheet.String("SubstituteText")));
        }
        #endregion

        #region IsLocal
        [TestMethod]
        public void WebSite_ServerConfig_Js_Substitutes_IsLocal_Correctly()
        {
            SendRequest("/ServerConfig.js", false);
            var output1 = Encoding.UTF8.GetString(_OutputStream.ToArray());

            SendRequest("/ServerConfig.js", true);
            var output2 = Encoding.UTF8.GetString(_OutputStream.ToArray());

            Assert.IsFalse(output1.Contains("__IS_LOCAL_ADDRESS"));
            Assert.IsTrue(output1.Contains("isLocalAddress = true;"));

            Assert.IsFalse(output2.Contains("__IS_LOCAL_ADDRESS"));
            Assert.IsTrue(output2.Contains("isLocalAddress = false;"));
        }
        #endregion

        #region Audio
        [TestMethod]
        public void WebSite_Audio_Can_Synthesise_Text_Correctly()
        {
            var wavAudio = new byte[] { 0x01, 0x02, 0x03 };
            _Audio.Setup(p => p.SpeechToWavBytes(It.IsAny<string>())).Returns(wavAudio);

            SendRequest("/Audio?cmd=say&line=Hello%20World!");

            _Audio.Verify(a => a.SpeechToWavBytes("Hello World!"), Times.Once());

            Assert.AreEqual(3, _Response.Object.ContentLength);
            Assert.AreEqual(MimeType.WaveAudio, _Response.Object.MimeType);
            Assert.AreEqual(HttpStatusCode.OK, _Response.Object.StatusCode);

            Assert.AreEqual(3, _OutputStream.Length);
            Assert.IsTrue(_OutputStream.ToArray().SequenceEqual(wavAudio));
        }

        [TestMethod]
        public void WebSite_Audio_Ignores_Unknown_Commands()
        {
            SendRequest("/Audio?line=Hello%20World!");

            Assert.AreEqual((HttpStatusCode)0, _Response.Object.StatusCode);
            Assert.AreEqual(0, _Response.Object.ContentLength);
            Assert.AreEqual(0, _OutputStream.Length);
        }

        [TestMethod]
        public void WebSite_Audio_Ignores_Speak_Command_With_No_Speech_Text()
        {
            SendRequest("/Audio?cmd=say");

            Assert.AreEqual((HttpStatusCode)0, _Response.Object.StatusCode);
            Assert.AreEqual(0, _Response.Object.ContentLength);
            Assert.AreEqual(0, _OutputStream.Length);
        }

        [TestMethod]
        public void WebSite_Audio_Honours_Configuration_Options()
        {
            _Audio.Setup(a => a.SpeechToWavBytes(It.IsAny<string>())).Returns(new byte[] { 0x01, 0xff });

            _Configuration.InternetClientSettings.CanPlayAudio = false;

            SendRequest("/Audio?cmd=say&line=whatever", true);
            Assert.AreEqual(0, _OutputStream.Length);

            SendRequest("/Audio?cmd=say&line=whatever", false);
            Assert.AreEqual(2, _OutputStream.Length);
            _OutputStream.SetLength(0);

            _Configuration.InternetClientSettings.CanPlayAudio = true;
            _ConfigurationStorage.Raise(e => e.ConfigurationChanged += null, EventArgs.Empty);

            SendRequest("/Audio?cmd=say&line=whatever", true);
            Assert.AreEqual(2, _OutputStream.Length);
            _OutputStream.SetLength(0);

            SendRequest("/Audio?cmd=say&line=whatever", false);
            Assert.AreEqual(2, _OutputStream.Length);
        }
        #endregion

        #region FavIcon
        [TestMethod]
        public void WebSite_FavIcon_Is_Served_When_Requested()
        {
            SendRequest("/favicon.ico");

            Assert.AreEqual(HttpStatusCode.OK, _Response.Object.StatusCode);
            Assert.AreEqual(_OutputStream.Length, _Response.Object.ContentLength);
            Assert.AreEqual(MimeType.IconImage, _Response.Object.MimeType);
            AssertFilesAreIdentical("favicon.ico", _OutputStream.ToArray());
        }
        #endregion

        #region ClosestAircraft.json
        [TestMethod]
        public void WebSite_ClosestAircraft_Serves_Correct_Json_Object()
        {
            AddBlankAircraft(1);
            var aircraft = _BaseStationAircraft[0];
            aircraft.Latitude = aircraft.Longitude = 1;

            var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");

            Assert.IsNotNull(json);
            Assert.IsNotNull(json.ClosestAircraft);
        }

        [TestMethod]
        public void WebSite_ClosestAircraft_Warns_If_Location_Not_Specified()
        {
            var warning = "Position not supplied";

            var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json");
            Assert.AreEqual(warning, json.WarningMessage);

            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1");
            Assert.AreEqual(warning, json.WarningMessage);

            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lng=1");
            Assert.AreEqual(warning, json.WarningMessage);

            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
            Assert.AreEqual(null, json.WarningMessage);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "ClosestAircraftJson$")]
        public void WebSite_ClosestAircraft_Fills_ClosestAircraft_Details_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            AddBlankAircraft(1);
            var aircraft = _BaseStationAircraft[0];
            aircraft.Latitude = aircraft.Longitude = 1;

            var aircraftProperty = typeof(IAircraft).GetProperty(worksheet.String("AircraftProperty"));
            var aircraftValue = TestUtilities.ChangeType(worksheet.EString("AircraftValue"), aircraftProperty.PropertyType, new CultureInfo("en-GB"));
            aircraftProperty.SetValue(aircraft, aircraftValue, null);

            var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
            Assert.IsNotNull(json.ClosestAircraft);
            var aircraftJson = (ProximityGadgetClosestAircraftJson)json.ClosestAircraft;

            var jsonProperty = typeof(ProximityGadgetClosestAircraftJson).GetProperty(worksheet.String("JsonProperty"));

            var expected = TestUtilities.ChangeType(worksheet.EString("JsonValue"), jsonProperty.PropertyType, new CultureInfo("en-GB"));
            var actual = jsonProperty.GetValue(aircraftJson, null);

            Assert.AreEqual(expected, actual, jsonProperty.Name);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "ClosestAircraftJson$")]
        public void WebSite_ClosestAircraft_Fills_ClosestAircraft_Details_Correctly_In_Foreign_Regions()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            using(var switcher = new CultureSwitcher("de-DE")) {
                AddBlankAircraft(1);
                var aircraft = _BaseStationAircraft[0];
                aircraft.Latitude = aircraft.Longitude = 1;

                var aircraftProperty = typeof(IAircraft).GetProperty(worksheet.String("AircraftProperty"));
                var aircraftValue = TestUtilities.ChangeType(worksheet.EString("AircraftValue"), aircraftProperty.PropertyType, new CultureInfo("en-GB"));
                aircraftProperty.SetValue(aircraft, aircraftValue, null);

                var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
                Assert.IsNotNull(json.ClosestAircraft);
                var aircraftJson = (ProximityGadgetClosestAircraftJson)json.ClosestAircraft;

                var jsonProperty = typeof(ProximityGadgetClosestAircraftJson).GetProperty(worksheet.String("JsonProperty"));

                var expected = TestUtilities.ChangeType(worksheet.EString("JsonValue"), jsonProperty.PropertyType, new CultureInfo("en-GB"));
                var actual = jsonProperty.GetValue(aircraftJson, null);

                Assert.AreEqual(expected, actual, String.Format("{0}/{1}", jsonProperty.Name, switcher.CultureName));
            }
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "ClosestAircraftSelection$")]
        public void WebSite_ClosestAircraft_Returns_The_Closest_Aircraft()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var latitude = worksheet.NDouble("GadgetLat");
            var longitude = worksheet.NDouble("GadgetLng");

            AddBlankAircraft(2);
            for(int i = 1;i <= 2;++i) {
                _BaseStationAircraft[i - 1].Callsign = i.ToString();
                _BaseStationAircraft[i - 1].Latitude = worksheet.NFloat(String.Format("AC{0}Lat", i));
                _BaseStationAircraft[i - 1].Longitude = worksheet.NFloat(String.Format("AC{0}Lng", i));
            }

            var location = new StringBuilder();
            if(latitude != null) location.AppendFormat("?lat={0}", latitude);
            if(longitude != null) {
                location.Append(latitude == null ? "?" : "&");
                location.AppendFormat("lng={0}", longitude);
            }
            var json = SendJsonRequest<ProximityGadgetAircraftJson>(String.Format("/ClosestAircraft.json{0}", location));

            if(worksheet.String("Closest") == null) Assert.IsNotInstanceOfType(json.ClosestAircraft, typeof(ProximityGadgetClosestAircraftJson));
            else Assert.AreEqual(worksheet.String("Closest"), ((ProximityGadgetClosestAircraftJson)json.ClosestAircraft).Callsign);
        }

        [TestMethod]
        public void WebSite_ClosestAircraft_Returns_List_Of_Aircraft_Transmitting_An_Emergency_Squawk()
        {
            AddBlankAircraft(2);

            var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
            Assert.AreEqual(0, json.EmergencyAircraft.Count);

            _BaseStationAircraft[0].Emergency = true;
            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
            Assert.AreEqual(1, json.EmergencyAircraft.Count);

            _BaseStationAircraft[1].Emergency = true;
            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
            Assert.AreEqual(2, json.EmergencyAircraft.Count);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "ClosestAircraftJson$")]
        public void WebSite_ClosestAircraft_Fills_EmergencyAircraft_Details_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            if(worksheet.String("AircraftProperty") != "Emergency") {
                AddBlankAircraft(1);
                var aircraft = _BaseStationAircraft[0];
                aircraft.Emergency = true;

                var aircraftProperty = typeof(IAircraft).GetProperty(worksheet.String("AircraftProperty"));
                var aircraftValue = TestUtilities.ChangeType(worksheet.EString("AircraftValue"), aircraftProperty.PropertyType, new CultureInfo("en-GB"));
                aircraftProperty.SetValue(aircraft, aircraftValue, null);

                var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
                var aircraftJson = (ProximityGadgetClosestAircraftJson)json.EmergencyAircraft[0];

                var jsonProperty = typeof(ProximityGadgetClosestAircraftJson).GetProperty(worksheet.String("JsonProperty"));

                var expected = TestUtilities.ChangeType(worksheet.EString("JsonValue"), jsonProperty.PropertyType, new CultureInfo("en-GB"));
                var actual = jsonProperty.GetValue(aircraftJson, null);

                Assert.AreEqual(expected, actual, jsonProperty.Name);
            }
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "ClosestAircraftJson$")]
        public void WebSite_ClosestAircraft_Fills_EmergencyAircraft_Details_Correctly_For_Foreign_Regions()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            if(worksheet.String("AircraftProperty") != "Emergency") {
                using(var switcher = new CultureSwitcher("de-DE")) {
                    AddBlankAircraft(1);
                    var aircraft = _BaseStationAircraft[0];
                    aircraft.Emergency = true;

                    var aircraftProperty = typeof(IAircraft).GetProperty(worksheet.String("AircraftProperty"));
                    var aircraftValue = TestUtilities.ChangeType(worksheet.EString("AircraftValue"), aircraftProperty.PropertyType, new CultureInfo("en-GB"));
                    aircraftProperty.SetValue(aircraft, aircraftValue, null);

                    var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json?lat=1&lng=1");
                    var aircraftJson = (ProximityGadgetClosestAircraftJson)json.EmergencyAircraft[0];

                    var jsonProperty = typeof(ProximityGadgetClosestAircraftJson).GetProperty(worksheet.String("JsonProperty"));

                    var expected = TestUtilities.ChangeType(worksheet.EString("JsonValue"), jsonProperty.PropertyType, new CultureInfo("en-GB"));
                    var actual = jsonProperty.GetValue(aircraftJson, null);

                    Assert.AreEqual(expected, actual, jsonProperty.Name);
                }
            }
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "ClosestAircraftTrigonometry$")]
        public void WebSite_ClosestAircraft_Fills_Calculated_Details_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var address = String.Format("/ClosestAircraft.json?lat={0}&lng={1}", worksheet.String("GadgetLat"), worksheet.String("GadgetLng"));

            AddBlankAircraft(1);
            var aircraft = _BaseStationAircraft[0];
            aircraft.Emergency = true;
            bool hasPosition = worksheet.String("AircraftLat") != null;
            if(hasPosition) {
                aircraft.Latitude = worksheet.NFloat("AircraftLat");
                aircraft.Longitude = worksheet.NFloat("AircraftLng");
            }

            var json = SendJsonRequest<ProximityGadgetAircraftJson>(address);

            Assert.AreEqual(worksheet.EString("Bearing"), json.EmergencyAircraft[0].BearingFromHere);
            Assert.AreEqual(worksheet.EString("Distance"), json.EmergencyAircraft[0].DistanceFromHere);
            if(hasPosition) {
                var closestAircraft = (ProximityGadgetClosestAircraftJson)json.ClosestAircraft;
                Assert.AreEqual(worksheet.EString("Distance"), closestAircraft.DistanceFromHere);
                Assert.AreEqual(worksheet.EString("Bearing"),  closestAircraft.BearingFromHere);
            }
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "ClosestAircraftTrigonometry$")]
        public void WebSite_ClosestAircraft_Fills_Calculated_Details_Correctly_For_Foreign_Regions()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var address = String.Format("/ClosestAircraft.json?lat={0}&lng={1}", worksheet.String("GadgetLat"), worksheet.String("GadgetLng"));

            AddBlankAircraft(1);
            var aircraft = _BaseStationAircraft[0];
            aircraft.Emergency = true;
            bool hasPosition = worksheet.String("AircraftLat") != null;
            if(hasPosition) {
                aircraft.Latitude = worksheet.NFloat("AircraftLat");
                aircraft.Longitude = worksheet.NFloat("AircraftLng");
            }

            using(var switcher = new CultureSwitcher("de-DE")) {
                var json = SendJsonRequest<ProximityGadgetAircraftJson>(address);

                Assert.AreEqual(worksheet.EString("Bearing"), json.EmergencyAircraft[0].BearingFromHere);
                Assert.AreEqual(worksheet.EString("Distance"), json.EmergencyAircraft[0].DistanceFromHere);
                if(hasPosition) {
                    var closestAircraft = (ProximityGadgetClosestAircraftJson)json.ClosestAircraft;
                    Assert.AreEqual(worksheet.EString("Distance"), closestAircraft.DistanceFromHere);
                    Assert.AreEqual(worksheet.EString("Bearing"),  closestAircraft.BearingFromHere);
                }
            }
        }

        [TestMethod]
        public void WebSite_ClosestAircraft_Honours_Configuration_Settings()
        {
            _Configuration.InternetClientSettings.AllowInternetProximityGadgets = false;

            var json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json", true);
            Assert.IsNull(json);

            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json", false);
            Assert.IsNotNull(json);

            _Configuration.InternetClientSettings.AllowInternetProximityGadgets = true;
            _ConfigurationStorage.Raise(g => g.ConfigurationChanged += null, EventArgs.Empty);

            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json", true);
            Assert.IsNotNull(json);

            json = SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json", false);
            Assert.IsNotNull(json);
        }

        [TestMethod]
        public void WebSite_ClosestAircraft_Sends_Correct_Cache_Control_Header()
        {
            SendJsonRequest<ProximityGadgetAircraftJson>("/ClosestAircraft.json", false);
            _Response.Verify(r => r.AddHeader("Cache-Control", "max-age=0, no-cache, no-store, must-revalidate"), Times.Once());
        }
        #endregion
    }
}
