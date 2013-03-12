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
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Web;
using VirtualRadar.Interface;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.StandingData;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Interface.WebSite;

namespace VirtualRadar.WebSite
{
    /// <summary>
    /// Responds to requests for aircraft lists in JSON file result.
    /// </summary>
    class AircraftListJsonPage : Page
    {
        /// <summary>
        /// The object that will do the work of producing JSON files from aircraft lists.
        /// </summary>
        private AircraftListJsonBuilder _Builder;

        /// <summary>
        /// Gets or sets the aircraft list that is keeping track of aircraft that an instance of BaseStation is receiving messages from.
        /// </summary>
        public IBaseStationAircraftList BaseStationAircraftList { get; set; }

        /// <summary>
        /// Gets or sets the aircraft list that is keeping track of aircraft in a flight simulator.
        /// </summary>
        public ISimpleAircraftList FlightSimulatorAircraftList { get; set; }

        /// <summary>
        /// See base class.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected override bool DoHandleRequest(IWebServer server, RequestReceivedEventArgs args)
        {
            bool result = false;

            if(args.PathAndFile.Equals("/AircraftList.json", StringComparison.OrdinalIgnoreCase)) result = HandleAircraftListJson(args, BaseStationAircraftList, false);
            else if(args.PathAndFile.Equals("/FlightSimList.json", StringComparison.OrdinalIgnoreCase)) result = HandleAircraftListJson(args, FlightSimulatorAircraftList, true);

            return result;
        }

        /// <summary>
        /// Sends the appropriate AircraftList.json content in response to the request passed across.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="aircraftList"></param>
        /// <param name="isFlightSimulator"></param>
        /// <returns>Always returns true - this just helps to make the caller's code a little more compact.</returns>
        private bool HandleAircraftListJson(RequestReceivedEventArgs args, IAircraftList aircraftList, bool isFlightSimulator)
        {
            if(_Builder == null) _Builder = new AircraftListJsonBuilder(Provider);

            if(aircraftList == null) args.Response.StatusCode = HttpStatusCode.InternalServerError;
            else {
                var buildArgs = ConstructBuildArgs(args, aircraftList, isFlightSimulator);
                var json = _Builder.Build(buildArgs);

                Responder.SendJson(args.Response, json, args.QueryString["callback"]);
                args.Classification = ContentClassification.Json;
            }

            return true;
        }

        /// <summary>
        /// Creates an object that holds all of the aircraft list arguments that were extracted from the request.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="aircraftList"></param>
        /// <param name="isFlightSimulator"></param>
        /// <returns></returns>
        private AircraftListJsonBuilderArgs ConstructBuildArgs(RequestReceivedEventArgs args, IAircraftList aircraftList, bool isFlightSimulator)
        {
            var result = new AircraftListJsonBuilderArgs() {
                AircraftList =          aircraftList,
                BrowserLatitude =       QueryNDouble(args, "lat"),
                BrowserLongitude =      QueryNDouble(args, "lng"),
                Filter =                isFlightSimulator ? null : ConstructFilter(args),
                IsFlightSimulatorList = isFlightSimulator,
                IsInternetClient =      args.IsInternetRequest,
                PreviousDataVersion =   QueryLong(args, "ldv", -1),
                ResendTrails =          QueryString(args, "refreshTrails", false) == "1",
                ShowShortTrail =        QueryString(args, "trFmt", true) == "S",
            };

            for(int sortColumnCount = 0;sortColumnCount < 2;++sortColumnCount) {
                var sortColumn = QueryString(args, String.Format("sortBy{0}", sortColumnCount + 1), true);
                var sortOrder = QueryString(args, String.Format("sortOrder{0}", sortColumnCount + 1), true);
                if(String.IsNullOrEmpty(sortColumn) || String.IsNullOrEmpty(sortOrder)) break;
                result.SortBy.Add(new KeyValuePair<string,bool>(sortColumn, sortOrder == "ASC"));
            }
            if(result.SortBy.Count == 0) result.SortBy.Add(new KeyValuePair<string,bool>(AircraftComparerColumn.FirstSeen, false));

            var previousAircraftIds = args.Request.Headers["X-VirtualRadarServer-AircraftIds"];
            if(!String.IsNullOrEmpty(previousAircraftIds)) {
                var decodedPreviousAircraftIds = HttpUtility.UrlDecode(previousAircraftIds);
                foreach(var chunk in decodedPreviousAircraftIds.Split(',')) {
                    int id;
                    if(int.TryParse(chunk, out id)) result.PreviousAircraft.Add(id);
                }
            }

            return result;
        }

        /// <summary>
        /// Extract the filter arguments from the request and returns them collected into an object.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private AircraftListJsonBuilderFilter ConstructFilter(RequestReceivedEventArgs args)
        {
            var result = new AircraftListJsonBuilderFilter() {
                AltitudeLower =                 QueryNInt(args, "fAltL"),
                AltitudeUpper =                 QueryNInt(args, "fAltU"),
                CallsignContains =              QueryString(args, "fCall", true),
                DistanceLower =                 QueryNDouble(args, "fDstL"),
                DistanceUpper =                 QueryNDouble(args, "fDstU"),
                EngineTypeEquals =              QueryNEnum<EngineType>(args, "fEgt"),
                Icao24CountryContains =         QueryString(args, "fCou", true),
                IsInterestingEquals =           QueryNBool(args, "fInt"),
                IsMilitaryEquals =              QueryNBool(args, "fMil"),
                MustTransmitPosition =          QueryNBool(args, "fNoPos") ?? false,
                OperatorContains =              QueryString(args, "fOp", true),
                RegistrationContains =          QueryString(args, "fReg", true),
                SpeciesEquals =                 QueryNEnum<Species>(args, "fSpc"),
                SquawkLower =                   QueryNInt(args, "fSqkL"),
                SquawkUpper =                   QueryNInt(args, "fSqkU"),
                TypeStartsWith =                QueryString(args, "fTyp", true),
                WakeTurbulenceCategoryEquals =  QueryNEnum<WakeTurbulenceCategory>(args, "fWtc"),
            };

            double? northBounds = QueryNDouble(args, "fNBnd");
            double? eastBounds = QueryNDouble(args, "fEBnd");
            double? southBounds = QueryNDouble(args, "fSBnd");
            double? westBounds = QueryNDouble(args, "fWBnd");

            if(northBounds != null && southBounds != null && westBounds != null && eastBounds != null) {
                result.PositionWithin = new Pair<Coordinate>(
                    new Coordinate((float)northBounds, (float)westBounds),
                    new Coordinate((float)southBounds, (float)eastBounds)
                );
            }

            return result;
        }
    }
}
