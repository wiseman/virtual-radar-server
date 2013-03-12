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
using VirtualRadar.Interface.Database;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Interface.WebSite;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.StandingData;
using InterfaceFactory;
using System.Diagnostics;

namespace VirtualRadar.WebSite
{
    /// <summary>
    /// Serves pages of report rows.
    /// </summary>
    class ReportRowsJsonPage : Page
    {
        #region Private class - Parameters
        /// <summary>
        /// A private class that holds the parameters passed to us by the Javascript via query strings etc.
        /// on the URL.
        /// </summary>
        class Parameters : SearchBaseStationCriteria
        {
            /// <summary>
            /// Gets or sets the type of report that is requesting data rows.
            /// </summary>
            public string ReportType { get; set; }

            /// <summary>
            /// Gets or sets the first row in the set to return or -1 for the first row.
            /// </summary>
            public int FromRow { get; set; }

            /// <summary>
            /// Gets or sets the last row in the set to return or -1 for the last row.
            /// </summary>
            public int ToRow { get; set; }

            /// <summary>
            /// Gets or sets the first field to sort on or null for no sorting.
            /// </summary>
            public string SortField1 { get; set; }

            /// <summary>
            /// Gets or sets a value indicating that the first field should be sorted in ascending order.
            /// </summary>
            public bool SortAscending1 { get; set; }

            /// <summary>
            /// Gets or sets the second field to sort on or null for no sorting.
            /// </summary>
            public string SortField2 { get; set; }

            /// <summary>
            /// Gets or sets a value indicating that the second field should be sorted in ascending order.
            /// </summary>
            public bool SortAscending2 { get; set; }

            /// <summary>
            /// Gets or sets a value indicating that the report only include military aircraft. This is not information
            /// that is stored in the database so it can increase the time required to gather the requested rows.
            /// </summary>
            public bool IsMilitary { get; set; }

            /// <summary>
            /// Gets or sets the wake turbulence category that the report is interested in. This is not information
            /// that is stored in the database so it can increase the time required to gather the requested rows.
            /// </summary>
            public WakeTurbulenceCategory? WakeTurbulenceCategory { get; set; }

            /// <summary>
            /// Gets or sets the species of aircraft that the report is interested in. This is not information
            /// that is stored in the database so it can increase the time required to gather the requested rows.
            /// </summary>
            public Species? Species { get; set; }
        }
        #endregion

        #region Fields
        /// <summary>
        /// The object that can manage aircraft pictures for us.
        /// </summary>
        IAircraftPictureManager _PictureManager;

        /// <summary>
        /// The object that keeps track of files in the picture folder.
        /// </summary>
        IDirectoryCache _PictureFolderCache;

        /// <summary>
        /// Mirrors the configuration setting.
        /// </summary>
        bool _InternetClientCanRunReports;

        /// <summary>
        /// Mirrors the configuration setting of the same name (almost).
        /// </summary>
        bool _InternetClientCanSeePictures;

        /// <summary>
        /// True if operator flags are available to the reports.
        /// </summary>
        bool _ShowOperatorFlags;

        /// <summary>
        /// True if silhouettes are available to the reports.
        /// </summary>
        bool _ShowSilhouettes;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the database that will be used to read records for the reports.
        /// </summary>
        public IBaseStationDatabase BaseStationDatabase { get; set; }

        /// <summary>
        /// Gets or sets the object that can read entries from the standing data for us.
        /// </summary>
        public IStandingDataManager StandingDataManager { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public ReportRowsJsonPage() : base()
        {
            _PictureManager = Factory.Singleton.Resolve<IAircraftPictureManager>().Singleton;
            _PictureFolderCache = Factory.Singleton.Resolve<IAutoConfigPictureFolderCache>().Singleton.DirectoryCache;
        }
        #endregion

        #region DoLoadConfiguration
        /// <summary>
        /// See base class.
        /// </summary>
        /// <param name="configuration"></param>
        protected override void DoLoadConfiguration(Configuration configuration)
        {
            base.DoLoadConfiguration(configuration);
            _InternetClientCanRunReports = configuration.InternetClientSettings.CanRunReports;
            _InternetClientCanSeePictures = configuration.InternetClientSettings.CanShowPictures;
            _ShowOperatorFlags = !String.IsNullOrEmpty(configuration.BaseStationSettings.OperatorFlagsFolder) && Provider.DirectoryExists(configuration.BaseStationSettings.OperatorFlagsFolder);
            _ShowSilhouettes = !String.IsNullOrEmpty(configuration.BaseStationSettings.SilhouettesFolder) && Provider.DirectoryExists(configuration.BaseStationSettings.SilhouettesFolder);
        }
        #endregion

        #region DoHandleRequest
        /// <summary>
        /// See base class.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected override bool DoHandleRequest(IWebServer server, RequestReceivedEventArgs args)
        {
            bool result = false;

            if(args.PathAndFile.Equals("/ReportRows.json", StringComparison.OrdinalIgnoreCase) && (_InternetClientCanRunReports || !args.IsInternetRequest)) {
                result = true;

                ReportRowsJson json = null;
                var startTime = Provider.UtcNow;

                Type expectedJsonType = ExpectedJsonType(args);

                try {
                    var parameters = ExtractParameters(args);

                    LimitDatesWhenNoStrongCriteriaPresent(parameters, args.IsInternetRequest);
                    if(parameters.ToDate.Year != 9999) parameters.ToDate = parameters.ToDate.AddDays(1).AddMilliseconds(-1);

                    switch(parameters.ReportType) {
                        case "DATE":    json = CreateManyAircraftReport(args, parameters); break;
                        case "ICAO":    json = CreateSingleAircraftReport(args, parameters, true); break;
                        case "REG":     json = CreateSingleAircraftReport(args, parameters, false); break;
                        default:        throw new NotImplementedException();
                    }

                    if(json != null) json.GroupBy = parameters.SortField1 ?? parameters.SortField2 ?? "";
                } catch(Exception ex) {
                    Debug.WriteLine(String.Format("ReportRowsJsonPage.DoHandleRequest caught exception {0}", ex.ToString()));
                    ILog log = Factory.Singleton.Resolve<ILog>().Singleton;
                    log.WriteLine("An exception was encountered during the processing of a report: {0}", ex.ToString());
                    if(json == null) json = (ReportRowsJson)Activator.CreateInstance(expectedJsonType);
                    json.ErrorText = String.Format("An exception was encounted during the processing of the report, see log for full details: {0}", ex.Message);
                }

                if(json == null) json = (ReportRowsJson)Activator.CreateInstance(expectedJsonType);
                json.ProcessingTime = String.Format("{0:N3}", (Provider.UtcNow - startTime).TotalSeconds);
                json.OperatorFlagsAvailable = _ShowOperatorFlags;
                json.SilhouettesAvailable = _ShowSilhouettes;
                Responder.SendJson(args.Response, json, null);
                args.Classification = ContentClassification.Json;
            }

            return result;
        }

        /// <summary>
        /// Returns the type of JSON that the report implies should be returned. The function tries
        /// not to throw any exceptions.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Type ExpectedJsonType(RequestReceivedEventArgs args)
        {
            Type result = typeof(AircraftReportJson);

            var reportType = args.QueryString["rep"];
            if(!String.IsNullOrEmpty(reportType)) {
                switch(reportType.ToUpper()) {
                    case "DATE":    result = typeof(FlightReportJson); break;
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts the parameters from the query string portion of the URL.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Parameters ExtractParameters(RequestReceivedEventArgs args)
        {
            var result = new Parameters() {
                ReportType = QueryString(args, "rep", true),
                FromRow = QueryInt(args, "fromrow", -1),
                ToRow = QueryInt(args, "torow", -1),
                SortField1 = QueryString(args, "sort1", false),
                SortField2 = QueryString(args, "sort2", false),
                SortAscending1 = QueryString(args, "sort1dir", true) != "DESC",
                SortAscending2 = QueryString(args, "sort2dir", true) != "DESC",
                Callsign = QueryString(args, "callsign", true),
                Country = QueryString(args, "country", false),
                FromDate = QueryDate(args, "fromdate", DateTime.MinValue, false, true),
                Icao = QueryString(args, "icao", true),
                IsEmergency = QueryBool(args, "isemergency", false),
                Operator = QueryString(args, "operator", false),
                Registration = QueryString(args, "reg", true),
                ToDate = QueryDate(args, "todate", DateTime.MaxValue, false, true),
                IsMilitary = QueryBool(args, "isMilitary", false),
                WakeTurbulenceCategory = QueryNEnum<WakeTurbulenceCategory>(args, "wtc"),
                Species = QueryNEnum<Species>(args, "species"),
            };

            if(result.ToDate < result.FromDate) {
                var fromDate = result.FromDate;
                result.FromDate = result.ToDate;
                result.ToDate = fromDate;
            }

            return result;
        }

        /// <summary>
        /// Builds up rows for a report that wants information on flights for many aircraft simultaneously.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private FlightReportJson CreateManyAircraftReport(RequestReceivedEventArgs args, Parameters parameters)
        {
            FlightReportJson json = new FlightReportJson();

            json.FromDate = FormatReportDate(parameters.FromDate);
            json.ToDate = FormatReportDate(parameters.ToDate);

            bool hasNonDatabaseCriteria = parameters.IsMilitary || parameters.WakeTurbulenceCategory != null || parameters.Species != null;

            if(!hasNonDatabaseCriteria) json.CountRows = BaseStationDatabase.GetCountOfFlights(parameters);

            var dbFlights = BaseStationDatabase.GetFlights(
                parameters,
                hasNonDatabaseCriteria ? -1 : parameters.FromRow, 
                hasNonDatabaseCriteria ? -1 : parameters.ToRow,
                parameters.SortField1, parameters.SortAscending1,
                parameters.SortField2, parameters.SortAscending2);

            if(hasNonDatabaseCriteria) {
                dbFlights = dbFlights.Where(f => {
                    bool matches = f.Aircraft != null;
                    if(matches) {
                        if(parameters.IsMilitary) {
                            var codeBlock = StandingDataManager.FindCodeBlock(f.Aircraft.ModeS);
                            matches = matches && codeBlock != null && codeBlock.IsMilitary;
                        }
                        if(parameters.Species != null || parameters.WakeTurbulenceCategory != null) {
                            var aircraftType = StandingDataManager.FindAircraftType(f.Aircraft.ICAOTypeCode);
                            if(parameters.Species != null) matches = matches && aircraftType != null && aircraftType.Species == parameters.Species;
                            if(parameters.WakeTurbulenceCategory != null) matches = matches && aircraftType != null && aircraftType.WakeTurbulenceCategory == parameters.WakeTurbulenceCategory;
                        }
                    }
                    return matches;
                }).ToList();

                json.CountRows = dbFlights.Count;

                int limit = parameters.ToRow == -1 || parameters.ToRow < parameters.FromRow ? int.MaxValue : (parameters.ToRow - Math.Max(0, parameters.FromRow)) + 1;
                int offset = parameters.FromRow < 0 ? 0 : parameters.FromRow;
                dbFlights = dbFlights.Skip(offset).Take(limit).ToList();
            }

            TranscribeDatabaseRecordsToJson(dbFlights, json.Flights, json.Aircraft, json.Airports, json.Routes, args, parameters);

            return json;
        }

        private void TranscribeDatabaseRecordsToJson(List<BaseStationFlight> dbFlights, List<ReportFlightJson> jsonFlights, List<ReportAircraftJson> jsonAircraft, List<ReportAirportJson> jsonAirports, List<ReportRouteJson> jsonRoutes, RequestReceivedEventArgs args, Parameters parameters)
        {
            var aircraftIdMap = new Dictionary<int, int>();
            var airportMap = new Dictionary<string, int>();
            var routeMap = new Dictionary<string, int>();

            int rowNumber = parameters.FromRow < 1 ? 1 : parameters.FromRow;
            foreach(var dbFlight in dbFlights) {
                var jsonFlight = AddReportFlightJson(dbFlight, jsonFlights, ref rowNumber);

                if(jsonAircraft != null) {
                    var dbAircraft = dbFlight.Aircraft;
                    if(dbAircraft == null) {
                        jsonFlight.AircraftIndex = jsonAircraft.Count;
                        jsonAircraft.Add(new ReportAircraftJson() { IsUnknown = true });
                    } else {
                        int aircraftIndex;
                        if(!aircraftIdMap.TryGetValue(dbAircraft.AircraftID, out aircraftIndex)) {
                            aircraftIndex = jsonAircraft.Count;
                            aircraftIdMap.Add(dbAircraft.AircraftID, aircraftIndex);
                            jsonAircraft.Add(CreateReportAircraftJson(dbAircraft, args));
                        }
                        jsonFlight.AircraftIndex = aircraftIndex;
                    }
                }

                int routeIndex = -1;
                if(!String.IsNullOrEmpty(dbFlight.Callsign) && !routeMap.TryGetValue(dbFlight.Callsign, out routeIndex)) {
                    var sdmRoute = StandingDataManager.FindRoute(dbFlight.Callsign);
                    if(sdmRoute == null) routeIndex = -1;
                    else {
                        var jsonRoute = new ReportRouteJson() {
                            FromIndex = BuildAirportJson(sdmRoute.From, airportMap, jsonAirports),
                            ToIndex = BuildAirportJson(sdmRoute.To, airportMap, jsonAirports),
                        };
                        foreach(var stopover in sdmRoute.Stopovers) {
                            int index = BuildAirportJson(stopover, airportMap, jsonAirports);
                            if(index != -1) jsonRoute.StopoversIndex.Add(index);
                        }

                        routeIndex = jsonRoutes.Count;
                        jsonRoutes.Add(jsonRoute);
                        routeMap.Add(dbFlight.Callsign, routeIndex);
                    }
                }
                jsonFlight.RouteIndex = routeIndex;
            }
        }

        /// <summary>
        /// Creates the JSON for a report that describes a single aircraft and the flights it has undertaken.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="parameters"></param>
        /// <param name="findByIcao"></param>
        /// <returns></returns>
        private AircraftReportJson CreateSingleAircraftReport(RequestReceivedEventArgs args, Parameters parameters, bool findByIcao)
        {
            AircraftReportJson json = new AircraftReportJson() {
                CountRows = 0,
                GroupBy = "",
            };

            var aircraftIdentifier = findByIcao ? parameters.Icao : parameters.Registration;
            if(!String.IsNullOrEmpty(aircraftIdentifier)) {
                var aircraft = findByIcao ? BaseStationDatabase.GetAircraftByCode(aircraftIdentifier)
                                          : BaseStationDatabase.GetAircraftByRegistration(aircraftIdentifier);
                if(aircraft != null) {
                    // Remove all criteria that is used to identify an aircraft
                    parameters.Icao = null;
                    parameters.Registration = null;
                    parameters.Operator = null;
                    parameters.Country = null;

                    json.Aircraft = CreateReportAircraftJson(aircraft, args);

                    json.CountRows = BaseStationDatabase.GetCountOfFlightsForAircraft(aircraft, parameters);
                    var dbFlights = BaseStationDatabase.GetFlightsForAircraft(aircraft, parameters,
                        parameters.FromRow, parameters.ToRow,
                        parameters.SortField1, parameters.SortAscending1,
                        parameters.SortField2, parameters.SortAscending2);

                    TranscribeDatabaseRecordsToJson(dbFlights, json.Flights, null, json.Airports, json.Routes, args, parameters);
                }
            }

            if(json.Aircraft == null) {
                json.Aircraft = new ReportAircraftJson() {
                    IsUnknown = true,
                };
            }

            return json;
        }

        /// <summary>
        /// Creates a JSON representation of the database flight and adds it to an existing list of flights.
        /// </summary>
        /// <param name="flight"></param>
        /// <param name="flightList"></param>
        /// <param name="rowNumber"></param>
        /// <returns></returns>
        private ReportFlightJson AddReportFlightJson(BaseStationFlight flight, List<ReportFlightJson> flightList, ref int rowNumber)
        {
            var result = new ReportFlightJson() {
                RowNumber = rowNumber++,
                Callsign = flight.Callsign,
                StartTime = flight.StartTime,
                EndTime = flight.EndTime.GetValueOrDefault(),
                FirstAltitude = flight.FirstAltitude.GetValueOrDefault(),
                FirstGroundSpeed = (int)flight.FirstGroundSpeed.GetValueOrDefault(),
                FirstIsOnGround = flight.FirstIsOnGround,
                FirstLatitude = flight.FirstLat.GetValueOrDefault(),
                FirstLongitude = flight.FirstLon.GetValueOrDefault(),
                FirstSquawk = flight.FirstSquawk.GetValueOrDefault(),
                FirstTrack = flight.FirstTrack.GetValueOrDefault(),
                FirstVerticalRate = flight.FirstVerticalRate.GetValueOrDefault(),
                HadAlert = flight.HadAlert,
                HadEmergency = flight.HadEmergency,
                HadSpi = flight.HadSpi,
                LastAltitude = flight.LastAltitude.GetValueOrDefault(),
                LastGroundSpeed = (int)flight.LastGroundSpeed.GetValueOrDefault(),
                LastIsOnGround = flight.LastIsOnGround,
                LastLatitude = flight.LastLat.GetValueOrDefault(),
                LastLongitude = flight.LastLon.GetValueOrDefault(),
                LastSquawk = flight.LastSquawk.GetValueOrDefault(),
                LastTrack = flight.LastTrack.GetValueOrDefault(),
                LastVerticalRate = flight.LastVerticalRate.GetValueOrDefault(),
                NumADSBMsgRec = flight.NumADSBMsgRec.GetValueOrDefault(),
                NumModeSMsgRec = flight.NumModeSMsgRec.GetValueOrDefault(),
                NumPosMsgRec = flight.NumPosMsgRec.GetValueOrDefault(),
            };
            flightList.Add(result);

            return result;
        }

        /// <summary>
        /// Creates the JSON representation of an aircraft.
        /// </summary>
        /// <param name="aircraft"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private ReportAircraftJson CreateReportAircraftJson(BaseStationAircraft aircraft, RequestReceivedEventArgs args)
        {
            var result = new ReportAircraftJson() {
                AircraftClass = aircraft.AircraftClass,
                AircraftId = aircraft.AircraftID,
                CofACategory = aircraft.CofACategory,
                CofAExpiry = aircraft.CofAExpiry,
                Country = aircraft.Country,
                CurrentRegDate = aircraft.CurrentRegDate,
                DeRegDate = aircraft.DeRegDate,
                FirstRegDate = aircraft.FirstRegDate,
                GenericName = aircraft.GenericName,
                IcaoTypeCode = aircraft.ICAOTypeCode,
                InfoUrl = aircraft.InfoUrl,
                Manufacturer = aircraft.Manufacturer,
                Icao = aircraft.ModeS,
                ModeSCountry = aircraft.ModeSCountry,
                MTOW = aircraft.MTOW,
                OperatorFlagCode = aircraft.OperatorFlagCode,
                OwnershipStatus = aircraft.OwnershipStatus,
                PictureUrl1 = aircraft.PictureUrl1,
                PictureUrl2 = aircraft.PictureUrl2,
                PictureUrl3 = aircraft.PictureUrl3,
                PopularName = aircraft.PopularName,
                PreviousId = aircraft.PreviousID,
                Registration = aircraft.Registration,
                RegisteredOwners = aircraft.RegisteredOwners,
                SerialNumber = aircraft.SerialNo,
                Status = aircraft.Status,
                TotalHours = aircraft.TotalHours,
                Type = aircraft.Type,
                Notes = aircraft.UserNotes,
                YearBuilt = aircraft.YearBuilt,
                HasPicture = args.IsInternetRequest && !_InternetClientCanSeePictures ? false : _PictureManager.FindPicture(_PictureFolderCache, aircraft.ModeS, aircraft.Registration) != null,
            };

            var aircraftType = String.IsNullOrEmpty(aircraft.ICAOTypeCode) ? null : StandingDataManager.FindAircraftType(aircraft.ICAOTypeCode);
            if(aircraftType != null) {
                result.WakeTurbulenceCategory = (int)aircraftType.WakeTurbulenceCategory;
                result.Engines = aircraftType.Engines;
                result.EngineType = (int)aircraftType.EngineType;
                result.Species = (int)aircraftType.Species;
            }

            var codeBlock = String.IsNullOrEmpty(aircraft.ModeS) ? null : StandingDataManager.FindCodeBlock(aircraft.ModeS);
            if(codeBlock != null) result.Military = codeBlock.IsMilitary;

            return result;
        }

        private void LimitDatesWhenNoStrongCriteriaPresent(SearchBaseStationCriteria criteria, bool isInternetRequest)
        {
            if(String.IsNullOrEmpty(criteria.Callsign) && String.IsNullOrEmpty(criteria.Registration) && String.IsNullOrEmpty(criteria.Icao)) {
                const int defaultDayCount = 7;

                DateTime now = Provider.UtcNow;

                bool fromIsMissing = criteria.FromDate.Year == DateTime.MinValue.Year;
                bool toIsMissing = criteria.ToDate.Year == DateTime.MaxValue.Year;

                if(fromIsMissing && toIsMissing) {
                    criteria.ToDate = now.Date;
                    toIsMissing = false;
                }

                if(fromIsMissing) criteria.FromDate = criteria.ToDate.AddDays(-defaultDayCount);
                else if(toIsMissing) criteria.ToDate = criteria.FromDate.AddDays(defaultDayCount);
                else if(isInternetRequest && (criteria.ToDate - criteria.FromDate).TotalDays > defaultDayCount) criteria.ToDate = criteria.FromDate.AddDays(defaultDayCount);
            }
        }

        private string FormatReportDate(DateTime date)
        {
            string result = null;
            if(date.Year != DateTime.MinValue.Year && date.Year != DateTime.MaxValue.Year) result = date.Date.ToShortDateString();

            return result;
        }

        private int BuildAirportJson(Airport sdmAirport, Dictionary<string, int> airportMap, List<ReportAirportJson> jsonList)
        {
            int result = -1;

            if(sdmAirport != null) {
                var code = String.IsNullOrEmpty(sdmAirport.IcaoCode) ? sdmAirport.IataCode : sdmAirport.IcaoCode;
                if(!String.IsNullOrEmpty(code)) {
                    if(!airportMap.TryGetValue(code, out result)) {
                        result = jsonList.Count;
                        jsonList.Add(new ReportAirportJson() { Code = code, Name = Describe.Airport(sdmAirport, false, false, true, true) });
                        airportMap.Add(code, result);
                    }
                }
            }

            return result;
        }
        #endregion
    }
}
