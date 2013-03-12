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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualRadar.Interface.WebServer;
using System.Net;
using VirtualRadar.Interface.WebSite;
using VirtualRadar.Interface;
using Test.Framework;
using Moq;
using System.Globalization;
using System.Linq.Expressions;
using System.Collections;
using System.IO;
using VirtualRadar.Interface.StandingData;
using System.Reflection;
using System.Threading;
using System.Web;

namespace Test.VirtualRadar.WebSite
{
    // This partial class contains all of the specific tests on the aircraft list JSON files.
    public partial class WebSiteTests
    {
        #region Pages
        private const string AircraftListPage = "/AircraftList.json";
        private const string FlightSimListPage = "/FlightSimList.json";
        #endregion

        #region Private Factory - AircraftListAddress
        /// <summary>
        /// A private class that simplifies the initialisation of a request to represent a request for
        /// an aircraft list, typically from AircraftList.json
        /// </summary>
        class AircraftListAddress
        {
            private Mock<IRequest> _Request;

            public string Page { get; set; }
            public IAircraftList AircraftList { get; set; }
            public double? BrowserLatitude { get; set; }
            public double? BrowserLongitude { get; set; }
            public List<int> PreviousAircraft { get; private set; }
            public long PreviousDataVersion { get; set; }
            public bool ResendTrails { get; set; }
            public bool ShowShortTrail { get; set; }
            public AircraftListFilter Filter { get; set; }
            public List<KeyValuePair<string, string>> SortBy { get; private set; }
            public string JsonpCallback { get; set; }

            public AircraftListAddress(Mock<IRequest> request)
            {
                _Request = request;

                Page = AircraftListPage;
                PreviousAircraft = new List<int>();
                PreviousDataVersion = -1L;
                SortBy = new List<KeyValuePair<string, string>>();
            }

            public string Address
            {
                get
                {
                    Dictionary<string, string> queryValues = new Dictionary<string,string>();

                    if(BrowserLatitude != null) queryValues.Add("lat", BrowserLatitude.Value.ToString(CultureInfo.InvariantCulture));
                    if(BrowserLongitude != null) queryValues.Add("lng", BrowserLongitude.Value.ToString(CultureInfo.InvariantCulture));
                    if(PreviousDataVersion > -1) queryValues.Add("ldv", PreviousDataVersion.ToString(CultureInfo.InvariantCulture));
                    if(ResendTrails) queryValues.Add("refreshTrails", "1");
                    if(ShowShortTrail) queryValues.Add("trFmt", "S");

                    if(Filter != null) Filter.AddToQueryValuesMap(queryValues);

                    if(SortBy.Count > 0) AddSortToQueryValuesMap(queryValues);

                    if(JsonpCallback != null) queryValues.Add("callback", JsonpCallback);

                    var result = new StringBuilder(Page);
                    bool first = true;
                    foreach(var keyValue in queryValues) {
                        result.AppendFormat("{0}{1}={2}",
                            first ? '?' : '&',
                            HttpUtility.UrlEncode(keyValue.Key),
                            HttpUtility.UrlDecode(keyValue.Value));
                        first = false;
                    }

                    var headers = new System.Collections.Specialized.NameValueCollection();
                    _Request.Setup(p => p.Headers).Returns(headers);

                    var previousAircraft = new StringBuilder();
                    if(PreviousAircraft.Count > 0) {
                        for(int i = 0;i < PreviousAircraft.Count;++i) {
                            if(i > 0) previousAircraft.Append("%2C");
                            previousAircraft.Append(PreviousAircraft[i]);
                        }
                    }
                    headers.Add("X-VirtualRadarServer-AircraftIds", previousAircraft.ToString());

                    return result.ToString();
                }
            }

            private void AddSortToQueryValuesMap(Dictionary<string, string> queryValues)
            {
                for(int i = 0;i < Math.Min(2, SortBy.Count);++i) {
                    var sortBy = SortBy[i];
                    queryValues.Add(String.Format("sortBy{0}", i + 1), sortBy.Key);
                    queryValues.Add(String.Format("sortOrder{0}", i + 1), sortBy.Value);
                }
            }
        }
        #endregion

        #region PrivateClass - AircraftListFilter
        /// <summary>
        /// A private class that simplifies the initialisation of a request for the filter portion of
        /// a request for an aircraft list JSON file - see <see cref="AircraftListAddress"/>.
        /// </summary>
        class AircraftListFilter
        {
            public int? AltitudeLower { get; set; }
            public int? AltitudeUpper { get; set; }
            public string CallsignContains { get; set; }
            public double? DistanceLower { get; set; }
            public double? DistanceUpper { get; set; }
            public EngineType? EngineTypeEquals { get; set; }
            public string Icao24CountryContains { get; set; }
            public bool? IsMilitaryEquals { get; set; }
            public bool? IsInterestingEquals { get; set; }
            public bool MustTransmitPosition { get; set; }
            public string OperatorContains { get; set; }
            public Pair<Coordinate> PositionWithin { get; set; }
            public string RegistrationContains { get; set; }
            public Species? SpeciesEquals { get; set; }
            public int? SquawkLower { get; set; }
            public int? SquawkUpper { get; set; }
            public string TypeStartsWith { get; set; }
            public WakeTurbulenceCategory? WakeTurbulenceCategoryEquals { get; set; }

            public void AddToQueryValuesMap(Dictionary<string, string> queryValues)
            {
                if(AltitudeLower != null)                   queryValues.Add("fAltL", AltitudeLower.Value.ToString(CultureInfo.InvariantCulture));
                if(AltitudeUpper != null)                   queryValues.Add("fAltU", AltitudeUpper.Value.ToString(CultureInfo.InvariantCulture));
                if(CallsignContains != null)                queryValues.Add("fCall", CallsignContains);
                if(DistanceLower != null)                   queryValues.Add("fDstL", DistanceLower.Value.ToString(CultureInfo.InvariantCulture));
                if(DistanceUpper != null)                   queryValues.Add("fDstU", DistanceUpper.Value.ToString(CultureInfo.InvariantCulture));
                if(EngineTypeEquals != null)                queryValues.Add("fEgt", ((int)EngineTypeEquals).ToString(CultureInfo.InvariantCulture));
                if(Icao24CountryContains != null)           queryValues.Add("fCou", Icao24CountryContains);
                if(IsMilitaryEquals != null)                queryValues.Add("fMil", (bool)IsMilitaryEquals ? "1" : "0");
                if(IsInterestingEquals != null)             queryValues.Add("fInt", (bool)IsInterestingEquals ? "1" : "0");
                if(MustTransmitPosition)                    queryValues.Add("fNoPos", "1");
                if(OperatorContains != null)                queryValues.Add("fOp", OperatorContains);
                if(RegistrationContains != null)            queryValues.Add("fReg", RegistrationContains);
                if(SpeciesEquals != null)                   queryValues.Add("fSpc", ((int)SpeciesEquals).ToString(CultureInfo.InvariantCulture));
                if(SquawkLower != null)                     queryValues.Add("fSqkL", SquawkLower.Value.ToString(CultureInfo.InvariantCulture));
                if(SquawkUpper != null)                     queryValues.Add("fSqkU", SquawkUpper.Value.ToString(CultureInfo.InvariantCulture));
                if(TypeStartsWith != null)                  queryValues.Add("fTyp", TypeStartsWith);
                if(WakeTurbulenceCategoryEquals != null)    queryValues.Add("fWtc", ((int)WakeTurbulenceCategoryEquals).ToString(CultureInfo.InvariantCulture));

                if(PositionWithin != null) {
                    queryValues.Add("fNBnd", PositionWithin.First.Latitude.ToString(CultureInfo.InvariantCulture));
                    queryValues.Add("fWBnd", PositionWithin.First.Longitude.ToString(CultureInfo.InvariantCulture));
                    queryValues.Add("fSBnd", PositionWithin.Second.Latitude.ToString(CultureInfo.InvariantCulture));
                    queryValues.Add("fEBnd", PositionWithin.Second.Longitude.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
        #endregion

        #region Helper Methods
        private void AddBlankAircraft(int count)
        {
            AddBlankAircraft(_BaseStationAircraft, count);
        }

        private void AddBlankFlightSimAircraft(int count)
        {
            AddBlankAircraft(_FlightSimulatorAircraft, count);
        }

        private void AddBlankAircraft(List<IAircraft> list, int count)
        {
            DateTime firstSeen = new DateTime(2010, 1, 1, 12, 0, 0);
            for(int i = 0;i < count;++i) {
                list.Add(new Mock<IAircraft>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties().Object);
                list[i].UniqueId = i;
                list[i].FirstSeen = firstSeen.AddSeconds(-i);  // <-- if no sort order is specified then it should default to sorting by FirstSeen in descending order
            }
        }
        #endregion

        #region Basic aircraft list tests
        [TestMethod]
        public void WebSite_BaseStationAircraftList_Responds_With_ServerError_If_BaseStationAircraftList_Property_Is_Empty()
        {
            _WebSite.BaseStationAircraftList = null;
            SendRequest(_AircraftListAddress.Address);
            Assert.AreEqual(HttpStatusCode.InternalServerError, _Response.Object.StatusCode);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Returns_Empty_AircraftListJson_When_BaseStationAircraftList_Is_Empty()
        {
            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(0, json.Aircraft.Count);
            Assert.AreEqual(0, json.AvailableAircraft);

            Assert.AreEqual(MimeType.Json, _Response.Object.MimeType);
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Responds_With_ServerError_If_FlightSimAircraftList_Property_Is_Empty()
        {
            _AircraftListAddress.Page = FlightSimListPage;

            _WebSite.FlightSimulatorAircraftList = null;
            SendRequest(_AircraftListAddress.Address);

            Assert.AreEqual(HttpStatusCode.InternalServerError, _Response.Object.StatusCode);
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Returns_Empty_AircraftListJson_When_FlightSimAircraftList_Is_Empty()
        {
            _AircraftListAddress.Page = FlightSimListPage;

            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(0, json.Aircraft.Count);
            Assert.AreEqual(0, json.AvailableAircraft);

            Assert.AreEqual(MimeType.Json, _Response.Object.MimeType);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Responds_To_Request_For_JSONP_Correctly()
        {
            _AircraftListAddress.JsonpCallback = "jsonpfunc";

            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, false, "jsonpfunc");
            Assert.AreEqual(0, json.Aircraft.Count);
            Assert.AreEqual(0, json.AvailableAircraft);

            Assert.AreEqual(MimeType.Json, _Response.Object.MimeType);
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Responds_To_Request_For_JSONP_Correctly()
        {
            _AircraftListAddress.Page = FlightSimListPage;
            _AircraftListAddress.JsonpCallback = "jsonpfunc";

            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, false, "jsonpfunc");
            Assert.AreEqual(0, json.Aircraft.Count);
            Assert.AreEqual(0, json.AvailableAircraft);

            Assert.AreEqual(MimeType.Json, _Response.Object.MimeType);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Adds_Cache_Control_Header()
        {
            SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, false);
            _Response.Verify(r => r.AddHeader("Cache-Control", "max-age=0, no-cache, no-store, must-revalidate"), Times.Once());
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Adds_Cache_Control_Header()
        {
            _AircraftListAddress.Page = FlightSimListPage;
            SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, false);
            _Response.Verify(r => r.AddHeader("Cache-Control", "max-age=0, no-cache, no-store, must-revalidate"), Times.Once());
        }
        #endregion

        #region List properties
        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sets_Flag_Dimensions_Correctly()
        {
            // These are currently non-configurable
            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(85, json.FlagWidth);
            Assert.AreEqual(20, json.FlagHeight);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Build_Sets_LastDataVersion_Correctly()
        {
            long o1, o2 = 200;
            _BaseStationAircraftList.Setup(m => m.TakeSnapshot(out o1, out o2)).Returns(_BaseStationAircraft);
            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual("200", json.LastDataVersion);

            o2 = 573;
            _BaseStationAircraftList.Setup(m => m.TakeSnapshot(out o1, out o2)).Returns(_BaseStationAircraft);
            json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual("573", json.LastDataVersion);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Build_Sets_ServerTime_Correctly()
        {
            DateTime timestamp = new DateTime(2001, 12, 31, 14, 27, 32, 194);
            long o1 = timestamp.Ticks, o2;
            _BaseStationAircraftList.Setup(m => m.TakeSnapshot(out o1, out o2)).Returns(_BaseStationAircraft);

            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);

            Assert.AreEqual(JavascriptHelper.ToJavascriptTicks(timestamp), json.ServerTime);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sets_ShowFlags_From_Configuration_Options()
        {
            _Configuration.BaseStationSettings.OperatorFlagsFolder = null;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowFlags);

            _Configuration.BaseStationSettings.OperatorFlagsFolder = "EXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(true, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowFlags);

            _Configuration.BaseStationSettings.OperatorFlagsFolder = "NOTEXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowFlags);
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Sets_ShowFlags_To_False_Regardless_Of_Configuration_Options()
        {
            _AircraftListAddress.Page = FlightSimListPage;

            _Configuration.BaseStationSettings.OperatorFlagsFolder = null;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowFlags);

            _Configuration.BaseStationSettings.OperatorFlagsFolder = "EXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowFlags);

            _Configuration.BaseStationSettings.OperatorFlagsFolder = "NOTEXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowFlags);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sets_ShowPictures_From_Configuration_Options()
        {
            _Configuration.BaseStationSettings.PicturesFolder = null;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowPictures);

            _Configuration.BaseStationSettings.PicturesFolder = "EXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(true, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowPictures);

            _Configuration.BaseStationSettings.PicturesFolder = "NOTEXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowPictures);
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Sets_ShowPictures_To_False_Regardless_Of_Configuration_Options()
        {
            _AircraftListAddress.Page = FlightSimListPage;

            _Configuration.BaseStationSettings.PicturesFolder = null;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowPictures);

            _Configuration.BaseStationSettings.PicturesFolder = "EXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowPictures);

            _Configuration.BaseStationSettings.PicturesFolder = "NOTEXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowPictures);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sets_ShowPictures_Can_Block_Internet_Clients()
        {
            _Configuration.BaseStationSettings.PicturesFolder = "EXISTS";

            _Configuration.InternetClientSettings.CanShowPictures = true;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(true, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, false).ShowPictures);

            _Configuration.InternetClientSettings.CanShowPictures = true;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(true, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, true).ShowPictures);

            _Configuration.InternetClientSettings.CanShowPictures = false;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(true, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, false).ShowPictures);

            _Configuration.InternetClientSettings.CanShowPictures = false;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address, true).ShowPictures);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sets_ShowSilhouettes_From_Configuration_Options()
        {
            _Configuration.BaseStationSettings.SilhouettesFolder = null;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowSilhouettes);

            _Configuration.BaseStationSettings.SilhouettesFolder = "EXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(true, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowSilhouettes);

            _Configuration.BaseStationSettings.SilhouettesFolder = "NOTEXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowSilhouettes);
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Sets_ShowSilhouettes_To_False_Regardless_Of_Configuration_Options()
        {
            _AircraftListAddress.Page = FlightSimListPage;

            _Configuration.BaseStationSettings.SilhouettesFolder = null;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowSilhouettes);

            _Configuration.BaseStationSettings.SilhouettesFolder = "EXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowSilhouettes);

            _Configuration.BaseStationSettings.SilhouettesFolder = "NOTEXISTS";
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(false, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShowSilhouettes);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sets_ShortTrailLength_From_Configuration_Options()
        {
            _AircraftListAddress.ShowShortTrail = true;

            _Configuration.GoogleMapSettings.ShortTrailLengthSeconds = 10;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(10, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShortTrailLengthSeconds);

            _Configuration.GoogleMapSettings.ShortTrailLengthSeconds = 20;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(20, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShortTrailLengthSeconds);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_ShortTrailLength_Sent_Even_If_Browser_Requested_Full_Trails()
        {
            _AircraftListAddress.ShowShortTrail = false;

            _Configuration.GoogleMapSettings.ShortTrailLengthSeconds = 10;
            _ConfigurationStorage.Raise(m => m.ConfigurationChanged += null, EventArgs.Empty);
            Assert.AreEqual(10, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).ShortTrailLengthSeconds);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sets_Source_Correctly()
        {
            _BaseStationAircraftList.Setup(m => m.Source).Returns(AircraftListSource.BaseStation);
            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(1, json.Source);

            _BaseStationAircraftList.Setup(m => m.Source).Returns(AircraftListSource.FlightSimulatorX);
            json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(3, json.Source);
        }
        #endregion

        #region Aircraft list and properties
        [TestMethod]
        public void WebSite_BaseStationAircraftList_Returns_Aircraft_List()
        {
            AddBlankAircraft(2);

            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);

            Assert.AreEqual(2, json.Aircraft.Count);
            Assert.IsTrue(json.Aircraft.Where(a => a.UniqueId == 0).Any());
            Assert.IsTrue(json.Aircraft.Where(a => a.UniqueId == 1).Any());
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Returns_Aircraft_List()
        {
            _AircraftListAddress.Page = FlightSimListPage;
            AddBlankFlightSimAircraft(2);

            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);

            Assert.AreEqual(2, json.Aircraft.Count);
            Assert.IsTrue(json.Aircraft.Where(a => a.UniqueId == 0).Any());
            Assert.IsTrue(json.Aircraft.Where(a => a.UniqueId == 1).Any());
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftJson$")]
        public void WebSite_BaseStationAircraftList_Correctly_Translates_IAircraft_Into_AircraftJson()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var aircraft = new Mock<IAircraft>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _BaseStationAircraft.Add(aircraft.Object);

            var aircraftProperty = typeof(IAircraft).GetProperty(worksheet.String("AircraftProperty"));
            var aircraftValue = TestUtilities.ChangeType(worksheet.EString("AircraftValue"), aircraftProperty.PropertyType, new CultureInfo("en-GB"));
            aircraftProperty.SetValue(aircraft.Object, aircraftValue, null);

            var json = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(1, json.Aircraft.Count);
            var aircraftJson = json.Aircraft[0];

            var jsonProperty = typeof(AircraftJson).GetProperty(worksheet.String("JsonProperty"));

            var expected = TestUtilities.ChangeType(worksheet.EString("JsonValue"), jsonProperty.PropertyType, new CultureInfo("en-GB"));
            var actual = jsonProperty.GetValue(aircraftJson, null);

            Assert.AreEqual(expected, actual, jsonProperty.Name);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Only_Copies_Values_If_They_Have_Changed()
        {
            var queryProperties = from p in typeof(IAircraft).GetProperties()
                                  where !p.Name.EndsWith("Changed") && typeof(IAircraft).GetProperty(String.Format("{0}Changed", p.Name)) != null
                                  select p;

            foreach(var aircraftProperty in queryProperties) {
                var changedProperty = typeof(IAircraft).GetProperty(String.Format("{0}Changed", aircraftProperty.Name));
                var jsonProperty = typeof(AircraftJson).GetProperty(aircraftProperty.Name);
                if(jsonProperty == null) {
                    switch(aircraftProperty.Name) {
                        case "FirstSeen":
                        case "Manufacturer":
                            continue;
                        case "PictureFileName":
                            jsonProperty = typeof(AircraftJson).GetProperty("HasPicture");
                            break;
                        default:
                            Assert.Fail("Need to add code to determine the JSON property for {0}", aircraftProperty.Name);
                            break;
                    }
                }

                var aircraft = new Mock<IAircraft>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
                var stopovers = new List<string>();
                aircraft.Setup(r => r.Stopovers).Returns(stopovers);
                _BaseStationAircraft.Clear();
                _BaseStationAircraft.Add(aircraft.Object);

                aircraft.Object.UniqueId = 1;
                _AircraftListAddress.PreviousAircraft.Add(1);

                object value = AircraftTestHelper.GenerateAircraftPropertyValue(aircraftProperty.PropertyType);
                var propertyAsList = aircraftProperty.GetValue(aircraft.Object, null) as IList;
                if(propertyAsList != null) propertyAsList.Add(value);
                else aircraftProperty.SetValue(aircraft.Object, value, null);

                Coordinate coordinate = value as Coordinate;
                if(coordinate != null) {
                    aircraft.Object.Latitude = coordinate.Latitude;
                    aircraft.Object.Longitude = coordinate.Longitude;
                }

                var parameter = Expression.Parameter(typeof(IAircraft));
                var body = Expression.Property(parameter, changedProperty.Name);
                var lambda = Expression.Lambda<Func<IAircraft, long>>(body, parameter);

                AircraftJson aircraftJson = null;

                // If the browser has never been sent a list before then the property must be returned
                aircraft.Setup(lambda).Returns(0L);
                _AircraftListAddress.PreviousDataVersion = -1L;
                aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];
                Assert.IsNotNull(jsonProperty.GetValue(aircraftJson, null), aircraftProperty.Name);

                aircraft.Setup(lambda).Returns(10L);
                aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];
                Assert.IsNotNull(jsonProperty.GetValue(aircraftJson, null), aircraftProperty.Name);

                // If the browser version is prior to the list version then the property must be returned
                _AircraftListAddress.PreviousDataVersion = 9L;
                aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];
                Assert.IsNotNull(jsonProperty.GetValue(aircraftJson, null), aircraftProperty.Name);

                // If the browser version is the same as the list version then the property must not be returned
                _AircraftListAddress.PreviousDataVersion = 10L;
                aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];
                Assert.IsNull(jsonProperty.GetValue(aircraftJson, null), aircraftProperty.Name);

                // If the browser version is the after the list version then the property must not be returned
                _AircraftListAddress.PreviousDataVersion = 11L;
                aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];
                Assert.IsNull(jsonProperty.GetValue(aircraftJson, null), aircraftProperty.Name);

                // If the browser version is after the list version, but the aircraft has not been seen before, then the property must be returned
                _AircraftListAddress.PreviousAircraft.Clear();
                aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];
                Assert.IsNotNull(jsonProperty.GetValue(aircraftJson, null), aircraftProperty.Name);
            }
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Copes_With_Multiple_Aircraft_Identifiers()
        {
            AddBlankAircraft(3);
            _BaseStationAircraft[0].Callsign = "0";
            _BaseStationAircraft[1].Callsign = "1";
            _BaseStationAircraft[2].Callsign = "2";

            _AircraftListAddress.PreviousDataVersion = _BaseStationAircraft[0].CallsignChanged + 1;

            _AircraftListAddress.PreviousAircraft.Add(0);
            _AircraftListAddress.PreviousAircraft.Add(2);

            var aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);

            // We have 3 aircraft and we've told the list builder that we know about the 1st and 3rd entries in the list and we already know the
            // callsigns. The second one is unknown to us, so it must send us everything it knows about it

            Assert.IsNull(aircraftJson.Aircraft.Where(a => a.UniqueId == 0).First().Callsign);
            Assert.AreEqual("1", aircraftJson.Aircraft.Where(a => a.UniqueId == 1).First().Callsign);
            Assert.IsNull(aircraftJson.Aircraft.Where(a => a.UniqueId == 2).First().Callsign);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListBearing$")]
        public void WebSite_BaseStationAircraftList_Calculates_Bearing_From_Browser_To_Aircraft_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var aircraft = new Mock<IAircraft>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _BaseStationAircraft.Add(aircraft.Object);

            aircraft.Object.Latitude = worksheet.NFloat("AircraftLatitude");
            aircraft.Object.Longitude = worksheet.NFloat("AircraftLongitude");

            _AircraftListAddress.BrowserLatitude = worksheet.NDouble("BrowserLatitude");
            _AircraftListAddress.BrowserLongitude = worksheet.NDouble("BrowserLongitude");

            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(1, list.Aircraft.Count);
            var aircraftJson = list.Aircraft[0];

            double? expected = worksheet.NDouble("Bearing");
            if(expected == null) Assert.IsNull(aircraftJson.BearingFromHere);
            else Assert.AreEqual((double)expected, (double)aircraftJson.BearingFromHere);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListDistance$")]
        public void WebSite_BaseStationAircraftList_Calculates_Distances_From_Browser_To_Aircraft_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            var aircraft = new Mock<IAircraft>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _BaseStationAircraft.Add(aircraft.Object);

            aircraft.Object.Latitude = worksheet.NFloat("AircraftLatitude");
            aircraft.Object.Longitude = worksheet.NFloat("AircraftLongitude");

            _AircraftListAddress.BrowserLatitude = worksheet.NDouble("BrowserLatitude");
            _AircraftListAddress.BrowserLongitude = worksheet.NDouble("BrowserLongitude");

            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(1, list.Aircraft.Count);
            var aircraftJson = list.Aircraft[0];

            double? expected = worksheet.NDouble("Distance");
            if(expected == null) Assert.IsNull(aircraftJson.DistanceFromHere);
            else Assert.AreEqual((double)expected, (double)aircraftJson.DistanceFromHere);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListDistance$")]
        public void WebSite_BaseStationAircraftList_Calculates_Distances_From_Browser_To_Aircraft_Correctly_When_Culture_Is_Not_UK()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            using(var switcher = new CultureSwitcher("de-DE")) {
                var aircraft = new Mock<IAircraft>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
                _BaseStationAircraft.Add(aircraft.Object);

                aircraft.Object.Latitude = worksheet.NFloat("AircraftLatitude");
                aircraft.Object.Longitude = worksheet.NFloat("AircraftLongitude");

                string address = String.Format("{0}?lat={1}&lng={2}", _AircraftListAddress.Page, worksheet.String("BrowserLatitude"), worksheet.String("BrowserLongitude"));

                var list = SendJsonRequest<AircraftListJson>(address);
                Assert.AreEqual(1, list.Aircraft.Count);
                var aircraftJson = list.Aircraft[0];

                double? expected = worksheet.NDouble("Distance");
                if(expected == null) Assert.IsNull(aircraftJson.DistanceFromHere);
                else Assert.AreEqual((double)expected, (double)aircraftJson.DistanceFromHere);
            }
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Does_Not_Calculate_Distances()
        {
            _AircraftListAddress.Page = FlightSimListPage;
            AddBlankFlightSimAircraft(1);
            var aircraft = _FlightSimulatorAircraft[0];

            aircraft.Latitude = aircraft.Longitude = 1f;

            _AircraftListAddress.BrowserLatitude = _AircraftListAddress.BrowserLongitude = 2.0;

            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.IsNull(list.Aircraft[0].DistanceFromHere);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Copies_Stopovers_Array_From_IAircraft_To_AircraftJson()
        {
            var aircraft = new Mock<IAircraft>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            var stopovers = new List<string>();
            aircraft.Setup(r => r.Stopovers).Returns(stopovers);

            _BaseStationAircraft.Add(aircraft.Object);

            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.IsNull(list.Aircraft[0].Stopovers);

            stopovers.Add("Stop 1");
            stopovers.Add("Stop 2");
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(2, list.Aircraft[0].Stopovers.Count);
            Assert.AreEqual("Stop 1", list.Aircraft[0].Stopovers[0]);
            Assert.AreEqual("Stop 2", list.Aircraft[0].Stopovers[1]);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListCoordinates$")]
        public void WebSite_BaseStationAircraftList_Builds_Arrays_Of_Trail_Coordinates_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            AddBlankAircraft(1);
            var aircraft = _BaseStationAircraft[0];
            Mock<IAircraft> mockAircraft = Mock.Get(aircraft);

            aircraft.Latitude = worksheet.NFloat("ACLat");
            aircraft.Longitude = worksheet.NFloat("ACLng");
            aircraft.Track = worksheet.NFloat("ACTrk");
            aircraft.FirstCoordinateChanged = worksheet.Long("ACFirstCoCh");
            aircraft.LastCoordinateChanged = worksheet.Long("ACLastCoCh");
            aircraft.PositionTime = new DateTime(1970, 1, 1, 0, 0, 0, worksheet.Int("ACPosTimeCh"));
            mockAircraft.Setup(m => m.PositionTimeChanged).Returns(worksheet.Long("ACPosTimeCh"));

            for(int i = 1;i <= 2;++i) {
                var dataVersion = String.Format("Coord{0}DV", i);
                var tick = String.Format("Coord{0}Tick", i);
                var latitude = String.Format("Coord{0}Lat", i);
                var longitude = String.Format("Coord{0}Lng", i);
                var track = String.Format("Coord{0}Trk", i);
                if(worksheet.String(dataVersion) != null) {
                    DateTime dotNetDate = new DateTime(1970, 1, 1, 0, 0, 0, worksheet.Int(tick));
                    var coordinate = new Coordinate(worksheet.Long(dataVersion), dotNetDate.Ticks, worksheet.Float(latitude), worksheet.Float(longitude), worksheet.NFloat(track));
                    aircraft.FullCoordinates.Add(coordinate);
                    aircraft.ShortCoordinates.Add(coordinate);
                }
            }

            _AircraftListAddress.PreviousDataVersion = worksheet.Long("ArgsPrevDV");
            if(worksheet.Bool("ArgsIsPrevAC")) _AircraftListAddress.PreviousAircraft.Add(0);
            _AircraftListAddress.ShowShortTrail = worksheet.Bool("ArgsShort");
            _AircraftListAddress.ResendTrails = worksheet.Bool("ArgsResend");

            var aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];

            var count = worksheet.Int("Count");
            if(count == 0) {
                Assert.IsNull(aircraftJson.ShortCoordinates);
                Assert.IsNull(aircraftJson.FullCoordinates);
            } else {
                var list = worksheet.Bool("IsShort") ? aircraftJson.ShortCoordinates : aircraftJson.FullCoordinates;
                Assert.AreEqual(count, list.Count);
                for(int i = 0;i < count;++i) {
                    var column = String.Format("R{0}", i);
                    Assert.AreEqual(worksheet.NDouble(column), list[i], "Element {0}", i);
                }
            }

            Assert.AreEqual(worksheet.Bool("ResetTrail"), aircraftJson.ResetTrail);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Calculates_SecondsTracked_From_Server_Time_And_FirstSeen_Property()
        {
            AddBlankAircraft(1);
            var aircraft = _BaseStationAircraft[0];
            Mock<IAircraft> mockAircraft = Mock.Get(aircraft);
            mockAircraft.Setup(a => a.FirstSeen).Returns(new DateTime(2001, 1, 1, 1, 2, 0));
            _Provider.Setup(p => p.UtcNow).Returns(new DateTime(2001, 1, 1, 1, 3, 17));

            var aircraftJson = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft[0];

            Assert.AreEqual(77L, aircraftJson.SecondsTracked);
        }
        #endregion

        #region Filtering of list
        #region Individual filters - reflection tests
        [TestMethod]
        public void WebSite_BaseStationAircraftList_Applies_AircraftListFilters_When_Supplied()
        {
            foreach(var filterProperty in typeof(AircraftListFilter).GetProperties()) {
                _AircraftListFilter = new AircraftListFilter();
                _AircraftListAddress.Filter = _AircraftListFilter;

                _BaseStationAircraft.Clear();

                if(filterProperty.Name == "DistanceLower" || filterProperty.Name == "DistanceUpper") continue; // <-- we have a separate test for these
                else if(filterProperty.Name == "MustTransmitPosition") TestMustTransmitPositionFilter();
                else if(filterProperty.Name == "PositionWithin") continue; // <-- we have a separate spreadsheet test for these
                else if(filterProperty.Name.EndsWith("Contains")) TestContainsFilter(filterProperty);
                else if(filterProperty.Name.EndsWith("Equals")) TestEqualsFilter(filterProperty);
                else if(filterProperty.Name.EndsWith("Lower")) TestLowerFilter(filterProperty);
                else if(filterProperty.Name.EndsWith("StartsWith")) TestStartsWithFilter(filterProperty);
                else if(filterProperty.Name.EndsWith("Upper")) TestUpperFilter(filterProperty);
                else Assert.Fail("Need to add code to test the {0} filter", filterProperty.Name);
            }
        }

        private void ExtractPropertiesFromFilterProperty(PropertyInfo filterProperty, string filterNameSuffix, out PropertyInfo aircraftProperty, out PropertyInfo jsonProperty)
        {
            var aircraftPropertyName = filterProperty.Name.Substring(0, filterProperty.Name.Length - filterNameSuffix.Length);
            aircraftProperty = typeof(IAircraft).GetProperty(aircraftPropertyName);
            Assert.IsNotNull(aircraftProperty, filterProperty.Name);

            var jsonPropertyName = aircraftProperty.Name;
            jsonProperty = typeof(AircraftJson).GetProperty(jsonPropertyName);
            Assert.IsNotNull(jsonProperty, filterProperty.Name);
        }

        private void TestContainsFilter(PropertyInfo filterProperty)
        {
            PropertyInfo aircraftProperty, jsonProperty;
            ExtractPropertiesFromFilterProperty(filterProperty, "Contains", out aircraftProperty, out jsonProperty);

            AddBlankAircraft(3);
            aircraftProperty.SetValue(_BaseStationAircraft[0], null, null);
            aircraftProperty.SetValue(_BaseStationAircraft[1], "ABC", null);
            aircraftProperty.SetValue(_BaseStationAircraft[2], "XYZ", null);

            filterProperty.SetValue(_AircraftListFilter, null, null);
            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(3, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(3, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "B", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(1, list.Count, filterProperty.Name);
            Assert.AreEqual("ABC", jsonProperty.GetValue(list[0], null), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "b", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(1, list.Count, filterProperty.Name);
            Assert.AreEqual("ABC", jsonProperty.GetValue(list[0], null), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "W", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(0, list.Count, filterProperty.Name);
        }

        private void TestEqualsFilter(PropertyInfo filterProperty)
        {
            // If the type is an Enum then this will only work if there are at least 2 entries in the enum and they are numbered 0, & 1. If this is
            // not the case then write an explicit test for the filter.

            PropertyInfo aircraftProperty, jsonProperty;
            ExtractPropertiesFromFilterProperty(filterProperty, "Equals", out aircraftProperty, out jsonProperty);

            AddBlankAircraft(3);
            aircraftProperty.SetValue(_BaseStationAircraft[0], null, null);
            aircraftProperty.SetValue(_BaseStationAircraft[1], TestUtilities.ChangeType(0, aircraftProperty.PropertyType, CultureInfo.InvariantCulture), null);
            aircraftProperty.SetValue(_BaseStationAircraft[2], TestUtilities.ChangeType(1, aircraftProperty.PropertyType, CultureInfo.InvariantCulture), null);

            filterProperty.SetValue(_AircraftListFilter, null, null);
            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(3, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, TestUtilities.ChangeType(1, filterProperty.PropertyType, CultureInfo.InvariantCulture), null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(1, list.Count, filterProperty.Name);
            Assert.AreEqual(TestUtilities.ChangeType(1, jsonProperty.PropertyType, CultureInfo.InvariantCulture), jsonProperty.GetValue(list[0], null));
        }

        private void TestLowerFilter(PropertyInfo filterProperty)
        {
            PropertyInfo aircraftProperty, jsonProperty;
            ExtractPropertiesFromFilterProperty(filterProperty, "Lower", out aircraftProperty, out jsonProperty);

            AddBlankAircraft(3);
            aircraftProperty.SetValue(_BaseStationAircraft[0], null, null);
            aircraftProperty.SetValue(_BaseStationAircraft[1], TestUtilities.ChangeType(99, aircraftProperty.PropertyType, CultureInfo.InvariantCulture), null);
            aircraftProperty.SetValue(_BaseStationAircraft[2], TestUtilities.ChangeType(100, aircraftProperty.PropertyType, CultureInfo.InvariantCulture), null);

            filterProperty.SetValue(_AircraftListFilter, null, null);
            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(3, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, TestUtilities.ChangeType(99, filterProperty.PropertyType, CultureInfo.InvariantCulture), null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(2, list.Count, filterProperty.Name);
            Assert.AreEqual(99, TestUtilities.ChangeType(jsonProperty.GetValue(list[0], null), typeof(int), CultureInfo.InvariantCulture), filterProperty.Name);
            Assert.AreEqual(100, TestUtilities.ChangeType(jsonProperty.GetValue(list[1], null), typeof(int), CultureInfo.InvariantCulture), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, TestUtilities.ChangeType(100, filterProperty.PropertyType, CultureInfo.InvariantCulture), null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(1, list.Count, filterProperty.Name);
            Assert.AreEqual(100, TestUtilities.ChangeType(jsonProperty.GetValue(list[0], null), typeof(int), CultureInfo.InvariantCulture), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, TestUtilities.ChangeType(101, filterProperty.PropertyType, CultureInfo.InvariantCulture), null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(0, list.Count, filterProperty.Name);
        }

        private void TestMustTransmitPositionFilter()
        {
            AddBlankAircraft(4);
            _BaseStationAircraft[0].Latitude = null;
            _BaseStationAircraft[0].Longitude = null;
            _BaseStationAircraft[1].Latitude = 1f;
            _BaseStationAircraft[1].Longitude = null;
            _BaseStationAircraft[2].Latitude = null;
            _BaseStationAircraft[2].Longitude = 2f;
            _BaseStationAircraft[3].Latitude = 3f;
            _BaseStationAircraft[3].Longitude = 3f;

            _AircraftListFilter.MustTransmitPosition = true;

            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);
            Assert.AreEqual(1, list.Aircraft.Count, "MustTransmitPosition");
            Assert.AreEqual(3f, list.Aircraft[0].Latitude, "MustTransmitPosition");
        }

        private void TestStartsWithFilter(PropertyInfo filterProperty)
        {
            PropertyInfo aircraftProperty, jsonProperty;
            ExtractPropertiesFromFilterProperty(filterProperty, "StartsWith", out aircraftProperty, out jsonProperty);

            AddBlankAircraft(3);
            aircraftProperty.SetValue(_BaseStationAircraft[0], null, null);
            aircraftProperty.SetValue(_BaseStationAircraft[1], "ABC", null);
            aircraftProperty.SetValue(_BaseStationAircraft[2], "XYZ", null);

            filterProperty.SetValue(_AircraftListFilter, null, null);
            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(3, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(3, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "A", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(1, list.Count, filterProperty.Name);
            Assert.AreEqual("ABC", jsonProperty.GetValue(list[0], null), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "B", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(0, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "a", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(1, list.Count, filterProperty.Name);
            Assert.AreEqual("ABC", jsonProperty.GetValue(list[0], null), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, "W", null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(0, list.Count, filterProperty.Name);
        }

        private void TestUpperFilter(PropertyInfo filterProperty)
        {
            PropertyInfo aircraftProperty, jsonProperty;
            ExtractPropertiesFromFilterProperty(filterProperty, "Upper", out aircraftProperty, out jsonProperty);

            AddBlankAircraft(3);
            aircraftProperty.SetValue(_BaseStationAircraft[0], null, null);
            aircraftProperty.SetValue(_BaseStationAircraft[1], TestUtilities.ChangeType(99, aircraftProperty.PropertyType, CultureInfo.InvariantCulture), null);
            aircraftProperty.SetValue(_BaseStationAircraft[2], TestUtilities.ChangeType(100, aircraftProperty.PropertyType, CultureInfo.InvariantCulture), null);

            filterProperty.SetValue(_AircraftListFilter, null, null);
            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(3, list.Count, filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, TestUtilities.ChangeType(100, filterProperty.PropertyType, CultureInfo.InvariantCulture), null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(2, list.Count, filterProperty.Name);
            Assert.AreEqual(99, TestUtilities.ChangeType(jsonProperty.GetValue(list[0], null), typeof(int), CultureInfo.InvariantCulture), filterProperty.Name);
            Assert.AreEqual(100, TestUtilities.ChangeType(jsonProperty.GetValue(list[1], null), typeof(int), CultureInfo.InvariantCulture), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, TestUtilities.ChangeType(99, filterProperty.PropertyType, CultureInfo.InvariantCulture), null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(1, list.Count, filterProperty.Name);
            Assert.AreEqual(99, TestUtilities.ChangeType(jsonProperty.GetValue(list[0], null), typeof(int), CultureInfo.InvariantCulture), filterProperty.Name);

            filterProperty.SetValue(_AircraftListFilter, TestUtilities.ChangeType(98, filterProperty.PropertyType, CultureInfo.InvariantCulture), null);
            list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;
            Assert.AreEqual(0, list.Count, filterProperty.Name);
        }
        #endregion

        #region Individual filters - other tests
        [TestMethod]
        public void WebSite_BaseStationAircraftList_Filters_Are_Culture_Agnostic()
        {
            var cultureNames = new string[] { 
                "en-GB",
                "en-US",
                "de-DE",
                "fr-FR",
                "nn-NO",
                "el-GR",
                "ru-RU",
                "zh-CHT",
            };

            _AircraftListAddress.BrowserLatitude = _AircraftListAddress.BrowserLongitude = 0;

            var currentCulture = Thread.CurrentThread.CurrentCulture;
            foreach(var cultureName in cultureNames) {
                string filterName = null;

                using(var switcher = new CultureSwitcher(cultureName)) {
                    foreach(var filterProperty in typeof(AircraftListFilter).GetProperties()) {
                        _BaseStationAircraft.Clear();
                        AddBlankAircraft(2);
                        var aircraft0 = _BaseStationAircraft[0];
                        var aircraft1 = _BaseStationAircraft[1];

                        _AircraftListFilter = new AircraftListFilter();
                        _AircraftListAddress.Filter = _AircraftListFilter;

                        filterName = filterProperty.Name;
                        var tailText = filterName.EndsWith("Upper") ? "Upper" :
                                       filterName.EndsWith("Lower") ? "Lower" :
                                       filterName.EndsWith("Within") ? "Within" :
                                       "";
                        if(tailText == "") continue;

                        string aircraftPropertyName = filterName.Substring(0, filterName.Length - tailText.Length);
                        switch(aircraftPropertyName) {
                            case "Altitude":
                                aircraft0.Altitude = -9; aircraft1.Altitude = 9;
                                filterProperty.SetValue(_AircraftListFilter, -5, null);
                                break;
                            case "Distance":
                                aircraft0.Latitude = aircraft0.Longitude = 1; aircraft1.Latitude = aircraft1.Longitude = 2;
                                filterProperty.SetValue(_AircraftListFilter, 200.25, null);
                                break;
                            case "Position":
                                aircraft0.Latitude = aircraft0.Longitude = -9.5f; aircraft1.Latitude = aircraft1.Longitude = 9.5f;
                                _AircraftListFilter.PositionWithin = new Pair<Coordinate>(new Coordinate(5.5f, -15.5f), new Coordinate(-15.5f, 5.5f));
                                break;
                            case "Squawk":
                                aircraft0.Squawk = -9; aircraft1.Squawk = 9;
                                filterProperty.SetValue(_AircraftListFilter, -5, null);
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;

                        var expectedAircraftId = tailText == "Upper" || tailText == "Within" ? 0 : 1;

                        Assert.AreEqual(1, list.Count, switcher.CultureName);
                        Assert.AreEqual(expectedAircraftId, list[0].UniqueId, switcher.CultureName);
                    }
                }
            }
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListDistanceFilter$")]
        public void WebSite_BaseStationAircraftList_AircraftListFilter_Distance_AircraftListFiltered_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            _AircraftListAddress.Filter = _AircraftListFilter;
            _AircraftListAddress.BrowserLatitude = worksheet.Float("BrowserLatitude");
            _AircraftListAddress.BrowserLongitude = worksheet.Float("BrowserLongitude");

            AddBlankAircraft(1);
            _BaseStationAircraft[0].Latitude = worksheet.NFloat("AircraftLatitude");
            _BaseStationAircraft[0].Longitude = worksheet.NFloat("AircraftLongitude");

            _AircraftListFilter.DistanceLower = worksheet.NDouble("DistanceLower");
            _AircraftListFilter.DistanceUpper = worksheet.NDouble("DistanceUpper");

            bool passed = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft.Count == 1;

            Assert.AreEqual(worksheet.Bool("Passes"), passed);
        }

        [TestMethod]
        [DataSource("Data Source='WebSiteTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "AircraftListPositionWithin$")]
        public void WebSite_BaseStationAircraftList_AircraftListFilter_PositionWithin_Works_Correctly()
        {
            var worksheet = new ExcelWorksheetData(TestContext);

            _AircraftListAddress.Filter = _AircraftListFilter;

            AddBlankAircraft(1);
            _BaseStationAircraft[0].Latitude = worksheet.NFloat("Latitude");
            _BaseStationAircraft[0].Longitude = worksheet.NFloat("Longitude");

            _AircraftListFilter.PositionWithin = new Pair<Coordinate>(
                new Coordinate(worksheet.Float("Top"), worksheet.Float("Left")),
                new Coordinate(worksheet.Float("Bottom"), worksheet.Float("Right"))
            );

            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);

            Assert.AreEqual(worksheet.Bool("IsInBounds") ? 1 : 0, list.Aircraft.Count);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_AircraftListFilter_Icao24CountryContains_Works_With_Mixed_Case_Aircraft_Property()
        {
            _AircraftListAddress.Filter = _AircraftListFilter;
            _AircraftListFilter.Icao24CountryContains = "B";

            AddBlankAircraft(1);
            _BaseStationAircraft[0].Icao24Country = "b";

            Assert.AreEqual(1, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft.Count);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_AircraftListFilter_OperatorContains_Works_With_Mixed_Case_Aircraft_Property()
        {
            _AircraftListAddress.Filter = _AircraftListFilter;
            _AircraftListFilter.OperatorContains = "B";

            AddBlankAircraft(1);
            _BaseStationAircraft[0].Operator = "b";

            Assert.AreEqual(1, SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft.Count);
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_AircraftListFilter_Sets_AvailableAircraft_Correctly()
        {
            _AircraftListAddress.Filter = _AircraftListFilter;
            _AircraftListFilter.RegistrationContains = "ABC";

            AddBlankAircraft(2);
            _BaseStationAircraft[0].Registration = "ABC";
            _BaseStationAircraft[1].Registration = "XYZ";

            var result = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);

            Assert.AreEqual(1, result.Aircraft.Count);
            Assert.AreEqual(2, result.AvailableAircraft);
        }

        [TestMethod]
        public void WebSite_FlightSimAircraftList_Ignores_All_Filters()
        {
            _AircraftListAddress.Page = FlightSimListPage;
            _AircraftListAddress.Filter = _AircraftListFilter;

            _AircraftListFilter.RegistrationContains = "ABC";

            AddBlankFlightSimAircraft(2);
            _FlightSimulatorAircraft[0].Registration = "ABC";
            _FlightSimulatorAircraft[1].Registration = "XYZ";

            var result = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address);

            Assert.AreEqual(2, result.Aircraft.Count);
        }
        #endregion

        #region Combinations of filters
        [TestMethod]
        public void WebSite_BaseStationAircraftList_Combinations_Of_AircraftListFilters_Work_Together()
        {
            foreach(var firstFilterProperty in typeof(AircraftListFilter).GetProperties()) {
                foreach(var secondFilterProperty in typeof(AircraftListFilter).GetProperties()) {
                    // Ignore combinations that are invalid or just a pain to automate
                    if(firstFilterProperty == secondFilterProperty) continue;
                    if(IsUpperLowerPair(firstFilterProperty.Name, secondFilterProperty.Name)) continue;
                    if(PropertiesAreCombinationOf(firstFilterProperty.Name, secondFilterProperty.Name, "DistanceLower", "MustTransmitPosition")) continue;
                    if(PropertiesAreCombinationOf(firstFilterProperty.Name, secondFilterProperty.Name, "DistanceUpper", "MustTransmitPosition")) continue;
                    if(PropertiesAreCombinationOf(firstFilterProperty.Name, secondFilterProperty.Name, "DistanceLower", "PositionWithin")) continue;
                    if(PropertiesAreCombinationOf(firstFilterProperty.Name, secondFilterProperty.Name, "DistanceUpper", "PositionWithin")) continue;
                    if(PropertiesAreCombinationOf(firstFilterProperty.Name, secondFilterProperty.Name, "PositionWithin", "MustTransmitPosition")) continue;

                    // pass 0 = neither filter passes, pass 1 = first filter passes, pass 2 = second filter passes, pass 3 = both pass
                    // Only pass 3 should allow the aircraft to appear in the aircraft list
                    for(int pass = 0;pass < 4;++pass) {
                        TestCleanup();
                        TestInitialise();

                        _AircraftListAddress.Filter = _AircraftListFilter;

                        AddBlankAircraft(1);

                        PrepareForCombinationTest(firstFilterProperty, _BaseStationAircraft[0], pass == 1 || pass == 3);
                        PrepareForCombinationTest(secondFilterProperty, _BaseStationAircraft[0], pass >= 2);

                        bool present = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft.Count == 1;

                        Assert.AreEqual(pass == 3, present, "Filter on {0} and {1}, pass {2}", firstFilterProperty.Name, secondFilterProperty.Name, pass);
                    }
                }

                break; // Comparing one property against all the others is enough. If we loop through every combination it takes quite a while...
            }
        }

        /// <summary>
        /// Returns true if the two property names passed across represent a pair of filters on the same aircraft property - e.g.
        /// 'AltitudeLower' and 'AltitudeUpper'.
        /// </summary>
        /// <param name="propertyName1"></param>
        /// <param name="propertyName2"></param>
        /// <returns></returns>
        private bool IsUpperLowerPair(string propertyName1, string propertyName2)
        {
            return propertyName1.Length == propertyName2.Length &&
                   (propertyName1.EndsWith("Lower") || propertyName1.EndsWith("Upper")) &&
                   (propertyName2.EndsWith("Lower") || propertyName2.EndsWith("Upper")) &&
                   propertyName1.StartsWith(propertyName2.Substring(0, propertyName2.Length - 5));
        }

        
        /// <summary>
        /// Returns true if the two property names are the two names passed across.
        /// </summary>
        /// <param name="propertyName1"></param>
        /// <param name="propertyName2"></param>
        /// <param name="name1"></param>
        /// <param name="name2"></param>
        /// <returns></returns>
        private bool PropertiesAreCombinationOf(string propertyName1, string propertyName2, string name1, string name2)
        {
            return (propertyName1 == name1 || propertyName1 == name2) && (propertyName2 == name1 || propertyName2 == name2);
        }

        /// <summary>
        /// Sets up the filters, build arguments and aircraft for a test that confirms that combinations of filters behave correctly.
        /// </summary>
        /// <param name="filterProperty"></param>
        /// <param name="aircraft"></param>
        /// <param name="complies"></param>
        private void PrepareForCombinationTest(PropertyInfo filterProperty, IAircraft aircraft, bool complies)
        {
            switch(filterProperty.Name) {
                case "AltitudeLower":
                    _AircraftListFilter.AltitudeLower = 10000;
                    aircraft.Altitude = complies ? 15000 : 1000;
                    break;
                case "AltitudeUpper":
                    _AircraftListFilter.AltitudeUpper = 20000;
                    aircraft.Altitude = complies ? 15000 : 30000;
                    break;
                case "CallsignContains":
                    _AircraftListFilter.CallsignContains = "ABC";
                    aircraft.Callsign = complies ? "ABC123" : "XYZ987";
                    break;
                case "DistanceLower":
                    _AircraftListFilter.DistanceLower = 15;
                    _AircraftListAddress.BrowserLatitude = _AircraftListAddress.BrowserLongitude = 0;
                    aircraft.Latitude = aircraft.Longitude = complies ? 0.15F : 0.01F;
                    break;
                case "DistanceUpper":
                    _AircraftListFilter.DistanceUpper = 30;
                    _AircraftListAddress.BrowserLatitude = _AircraftListAddress.BrowserLongitude = 0;
                    aircraft.Latitude = aircraft.Longitude = complies ? 0.15F : 0.2F;
                    break;
                case "EngineTypeEquals":
                    _AircraftListFilter.EngineTypeEquals = EngineType.Piston;
                    aircraft.EngineType = complies ? EngineType.Piston : EngineType.Jet;
                    break;
                case "Icao24CountryContains":
                    _AircraftListFilter.Icao24CountryContains = "UNITED";
                    aircraft.Icao24Country = complies ? "UNITED KINGDOM" : "BELGIUM";
                    break;
                case "IsInterestingEquals":
                    _AircraftListFilter.IsInterestingEquals = true;
                    aircraft.IsInteresting = complies ? true : false;
                    break;
                case "IsMilitaryEquals":
                    _AircraftListFilter.IsMilitaryEquals = true;
                    aircraft.IsMilitary = complies ? true : false;
                    break;
                case "MustTransmitPosition":
                    _AircraftListFilter.MustTransmitPosition = true;
                    aircraft.Latitude = aircraft.Longitude = complies ? 1F : (float?)null;
                    break;
                case "OperatorContains":
                    _AircraftListFilter.OperatorContains = "TRU";
                    aircraft.Operator = complies ? "ERMENUTRUDE AIRLINES" : "DOOGAL INTERNATIONAL";
                    break;
                case "PositionWithin":
                    _AircraftListFilter.PositionWithin = new Pair<Coordinate>(new Coordinate(4F, 1F), new Coordinate(1F, 4F));
                    aircraft.Latitude = aircraft.Longitude = complies ? 3F : 6F;
                    break;
                case "RegistrationContains":
                    _AircraftListFilter.RegistrationContains = "GLU";
                    aircraft.Registration = complies ? "G-GLUE" : "G-LUUU";
                    break;
                case "SpeciesEquals":
                    _AircraftListFilter.SpeciesEquals = Species.Helicopter;
                    aircraft.Species = complies ? Species.Helicopter : Species.Landplane;
                    break;
                case "SquawkLower":
                    _AircraftListFilter.SquawkLower = 2000;
                    aircraft.Squawk = complies ? 7654 : 1234;
                    break;
                case "SquawkUpper":
                    _AircraftListFilter.SquawkUpper = 4000;
                    aircraft.Squawk = complies ? 2345 : 4567;
                    break;
                case "TypeStartsWith":
                    _AircraftListFilter.TypeStartsWith = "A38";
                    aircraft.Type = complies ? "A380" : "A340";
                    break;
                case "WakeTurbulenceCategoryEquals":
                    _AircraftListFilter.WakeTurbulenceCategoryEquals = WakeTurbulenceCategory.Heavy;
                    aircraft.WakeTurbulenceCategory = complies ? WakeTurbulenceCategory.Heavy : WakeTurbulenceCategory.Medium;
                    break;
                default:
                    Assert.Fail("Need to add code to prepare objects for {0} filter", filterProperty.Name);
                    break;
            }
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Combinations_Of_Upper_Lower_AircraftListFilters_Work_Together()
        {
            foreach(var lowerProperty in typeof(AircraftListFilter).GetProperties().Where(p => p.Name.EndsWith("Lower"))) {
                // pass 0 = under lower limit, pass 1 = between lower and upper, pass 2 = above upper.
                // Only pass 1 should allow the aircraft to appear in the aircraft list
                for(int pass = 0;pass < 3;++pass) {
                    TestCleanup();
                    TestInitialise();

                    _AircraftListAddress.Filter = _AircraftListFilter;

                    AddBlankAircraft(1);
                    var aircraft = _BaseStationAircraft[0];

                    switch(lowerProperty.Name) {
                        case "AltitudeLower":
                            _AircraftListFilter.AltitudeLower = 10000;
                            _AircraftListFilter.AltitudeUpper = 20000;
                            aircraft.Altitude = pass == 0 ? 5000 : pass == 1 ? 15000 : 25000;
                            break;
                        case "DistanceLower":
                            _AircraftListFilter.DistanceLower = 15;
                            _AircraftListFilter.DistanceUpper = 30;
                            _AircraftListAddress.BrowserLatitude = _AircraftListAddress.BrowserLongitude = 0;
                            aircraft.Latitude = aircraft.Longitude = pass == 0 ? 0.01F : pass == 1 ? 0.15F : 0.2F;
                            break;
                        case "SquawkLower":
                            _AircraftListFilter.SquawkLower = 1000;
                            _AircraftListFilter.SquawkUpper = 2000;
                            aircraft.Squawk = pass == 0 ? 500 : pass == 1 ? 1500 : 2500;
                            break;
                    }

                    bool present = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft.Count == 1;

                    Assert.AreEqual(pass == 1, present, "Filter on {0}, pass {1}", lowerProperty.Name, pass);
                }
            }
        }
        #endregion
        #endregion

        #region Sorting of list
        [TestMethod]
        public void WebSite_BaseStationAircraftList_Defaults_To_Sorting_By_FirstSeen_Descending()
        {
            AddBlankAircraft(2);

            DateTime baseTime = new DateTime(2001, 1, 2, 0, 0, 0);
            DateTime loTime = baseTime.AddSeconds(1);
            DateTime hiTime = baseTime.AddSeconds(2);

            for(int order = 0;order < 2;++order) {
                _BaseStationAircraft[0].FirstSeen = order == 0 ? hiTime : loTime;
                _BaseStationAircraft[1].FirstSeen = order == 0 ? loTime : hiTime;

                var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;

                Assert.AreEqual(hiTime, list[0].FirstSeen);
                Assert.AreEqual(loTime, list[1].FirstSeen);
            }
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Will_Sort_List_By_Single_Column()
        {
            _AircraftListAddress.BrowserLatitude = _AircraftListAddress.BrowserLongitude = 0;

            foreach(var sortColumnField in typeof(AircraftComparerColumn).GetFields()) {
                var sortColumn = (string)sortColumnField.GetValue(null);

                for(int initialOrder = 0;initialOrder < 2;++initialOrder) {
                    for(int sortDescending = 0;sortDescending < 2;++sortDescending) {
                        // pass 0: LHS < RHS
                        // pass 1: LHS > RHS
                        // pass 2: LHS is default < RHS is not
                        // pass 3: LHS is not  > RHS is default
                        // Order is reversed if sortDescending == 1
                        for(int pass = 0;pass < 4;++pass) {
                            var failedMessage = String.Format("{0}, sortDescending = {1}, initialOrder = {2}, pass = {3}", sortColumn, sortDescending, initialOrder, pass);

                            _AircraftListAddress.SortBy.Clear();
                            _AircraftListAddress.SortBy.Add(new KeyValuePair<string,string>(sortColumn, sortDescending == 0 ? "ASC" : "DESC"));

                            _BaseStationAircraft.Clear();
                            AddBlankAircraft(2);
                            var lhs = _BaseStationAircraft[initialOrder == 0 ? 0 : 1];
                            var rhs = _BaseStationAircraft[initialOrder == 0 ? 1 : 0];
                            if(sortColumn == AircraftComparerColumn.FirstSeen) lhs.FirstSeen = rhs.FirstSeen = DateTime.MinValue;

                            if(pass != 2) PrepareForSortTest(sortColumn, lhs, pass != 1);
                            if(pass != 3) PrepareForSortTest(sortColumn, rhs, pass != 0);

                            var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;

                            bool expectLhsFirst = pass == 0 || pass == 2;
                            if(sortDescending != 0) expectLhsFirst = !expectLhsFirst;

                            if(expectLhsFirst) {
                                Assert.AreEqual(lhs.UniqueId, list[0].UniqueId, failedMessage);
                                Assert.AreEqual(rhs.UniqueId, list[1].UniqueId, failedMessage);
                            } else {
                                Assert.AreEqual(rhs.UniqueId, list[0].UniqueId, failedMessage);
                                Assert.AreEqual(lhs.UniqueId, list[1].UniqueId, failedMessage);
                            }
                        }
                    }
                }
            }
        }

        private void PrepareForSortTest(string sortColumn, IAircraft aircraft, bool setLow)
        {
            switch(sortColumn) {
                case AircraftComparerColumn.Altitude:                   aircraft.Altitude = setLow ? 1 : 2; break;
                case AircraftComparerColumn.Callsign:                   aircraft.Callsign = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.Destination:                aircraft.Destination = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.DistanceFromHere:           aircraft.Latitude = aircraft.Longitude = setLow ? 1 : 2; break;
                case AircraftComparerColumn.FirstSeen:                  aircraft.FirstSeen = setLow ? new DateTime(2001, 1, 1, 0, 0, 0) : new DateTime(2001, 1, 1, 0, 0, 1); break;
                case AircraftComparerColumn.FlightsCount:               aircraft.FlightsCount = setLow ? 1 : 2; break;
                case AircraftComparerColumn.GroundSpeed:                aircraft.GroundSpeed = setLow ? 1 : 2; break;
                case AircraftComparerColumn.Icao24:                     aircraft.Icao24 = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.Icao24Country:              aircraft.Icao24Country = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.Model:                      aircraft.Model = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.NumberOfEngines:            aircraft.NumberOfEngines = setLow ? "1" : "2"; break;
                case AircraftComparerColumn.Operator:                   aircraft.Operator = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.OperatorIcao:               aircraft.OperatorIcao = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.Origin:                     aircraft.Origin = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.Registration:               aircraft.Registration = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.Species:                    aircraft.Species = setLow ? Species.Amphibian : Species.Helicopter; break;
                case AircraftComparerColumn.Squawk:                     aircraft.Squawk = setLow ? 1 : 2; break;
                case AircraftComparerColumn.Type:                       aircraft.Type = setLow ? "A" : "B"; break;
                case AircraftComparerColumn.VerticalRate:               aircraft.VerticalRate = setLow ? 1 : 2; break;
                case AircraftComparerColumn.WakeTurbulenceCategory:     aircraft.WakeTurbulenceCategory = setLow ? WakeTurbulenceCategory.Light : WakeTurbulenceCategory.Medium; break;
                default:                                                throw new NotImplementedException();
            }
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Will_Sort_List_By_Two_Columns()
        {
            _AircraftListAddress.SortBy.Add(new KeyValuePair<string,string>(AircraftComparerColumn.Altitude, "ASC"));
            _AircraftListAddress.SortBy.Add(new KeyValuePair<string,string>(AircraftComparerColumn.Registration, "ASC"));

            AddBlankAircraft(2);

            for(int order = 0;order < 2;++order) {
                _BaseStationAircraft[0].Registration = order == 0 ? "A" : "B";
                _BaseStationAircraft[1].Registration = order == 0 ? "B" : "A";

                var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;

                Assert.AreEqual("A", list[0].Registration);
                Assert.AreEqual("B", list[1].Registration);
            }
        }

        [TestMethod]
        public void WebSite_BaseStationAircraftList_Sort_Column_Name_Is_Case_Insensitive()
        {
            AddBlankAircraft(2);

            for(int caseStyle = 0;caseStyle < 2;++caseStyle) {
                _AircraftListAddress.SortBy.Clear();
                _AircraftListAddress.SortBy.Add(new KeyValuePair<string,string>(
                    caseStyle == 0 ? AircraftComparerColumn.Registration.ToLower() : AircraftComparerColumn.Registration.ToUpper(),
                    caseStyle == 0 ? "asc" : "ASC"));

                for(int order = 0;order < 2;++order) {
                    _BaseStationAircraft[0].Registration = order == 0 ? "A" : "B";
                    _BaseStationAircraft[1].Registration = order == 0 ? "B" : "A";

                    var list = SendJsonRequest<AircraftListJson>(_AircraftListAddress.Address).Aircraft;

                    Assert.AreEqual("A", list[0].Registration);
                    Assert.AreEqual("B", list[1].Registration);
                }
            }
        }
        #endregion
    }
}
