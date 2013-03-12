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
using System.Text;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.StandingData;
using VirtualRadar.Interface.WebSite;

namespace VirtualRadar.WebSite
{
    /// <summary>
    /// An object that can translate a list of <see cref="IAircraft"/> into an <see cref="AircraftListJson"/>.
    /// </summary>
    class AircraftListJsonBuilder
    {
        #region Fields
        /// <summary>
        /// True if the server allows aircraft pictures to be sent to Internet clients.
        /// </summary>
        private bool _ShowPicturesToInternetClients;

        /// <summary>
        /// True if the operator flags folder has been correctly configured.
        /// </summary>
        private bool _ShowFlags;

        /// <summary>
        /// True if the pictures folder has been correctly configured.
        /// </summary>
        private bool _ShowPictures;

        /// <summary>
        /// True if silhouettes have been correctly configured.
        /// </summary>
        private bool _ShowSilhouettes;

        /// <summary>
        /// The number of seconds of positions to show on short trails.
        /// </summary>
        private int _ShortTrailLength;

        /// <summary>
        /// The provider that will abstract away parts of the environment for us.
        /// </summary>
        private IWebSiteProvider _Provider;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        /// <param name="provider"></param>
        public AircraftListJsonBuilder(IWebSiteProvider provider)
        {
            _Provider = provider;

            LoadConfiguration();

            var configurationStorage = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton;
            configurationStorage.ConfigurationChanged += ConfigurationStorage_ConfigurationChanged;
        }

        /// <summary>
        /// Loads the bits of configuration that the object is interested in.
        /// </summary>
        private void LoadConfiguration()
        {
            var config = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton.Load();

            _ShowPicturesToInternetClients = config.InternetClientSettings.CanShowPictures;

            _ShowFlags = !String.IsNullOrEmpty(config.BaseStationSettings.OperatorFlagsFolder) && _Provider.DirectoryExists(config.BaseStationSettings.OperatorFlagsFolder);
            _ShowPictures = !String.IsNullOrEmpty(config.BaseStationSettings.PicturesFolder) && _Provider.DirectoryExists(config.BaseStationSettings.PicturesFolder);
            _ShowSilhouettes = !String.IsNullOrEmpty(config.BaseStationSettings.SilhouettesFolder) && _Provider.DirectoryExists(config.BaseStationSettings.SilhouettesFolder);

            _ShortTrailLength = config.GoogleMapSettings.ShortTrailLengthSeconds;
        }
        #endregion

        #region Build
        /// <summary>
        /// Returns a fully-formed <see cref="AircraftListJson"/> from the aircraft list passed across.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public AircraftListJson Build(AircraftListJsonBuilderArgs args)
        {
            if(args == null) throw new ArgumentNullException("aircraftList");
            if(args.AircraftList == null) throw new InvalidOperationException("The AircraftList must be supplied");

            var result = new AircraftListJson() {
                FlagHeight = 20,
                FlagWidth = 85,
                ShowFlags = _ShowFlags && !args.IsFlightSimulatorList,
                ShowPictures = _ShowPictures && (!args.IsInternetClient || _ShowPicturesToInternetClients) && !args.IsFlightSimulatorList,
                ShowSilhouettes = _ShowSilhouettes && !args.IsFlightSimulatorList,
                ShortTrailLengthSeconds = _ShortTrailLength,
                Source = (int)args.AircraftList.Source,
            };

            long timestamp, dataVersion;
            var aircraft = args.AircraftList.TakeSnapshot(out timestamp, out dataVersion);
            result.AvailableAircraft = aircraft.Count;
            result.LastDataVersion = dataVersion.ToString();
            result.ServerTime = JavascriptHelper.ToJavascriptTicks(timestamp);

            Dictionary<int, double?> distances = new Dictionary<int,double?>();
            aircraft = FilterAircraft(aircraft, args, ref distances);
            SortAircraft(aircraft, args, distances);
            CopyAircraft(result, aircraft, args, distances);

            return result;
        }

        /// <summary>
        /// Returns a filtered list of aircraft and at the same time calculates the distances from the browser location to each aircraft.
        /// </summary>
        /// <param name="aircraftListSnapshot"></param>
        /// <param name="args"></param>
        /// <param name="distances"></param>
        /// <returns></returns>
        /// <remarks>Distance calculations can be expensive, hence the reason why we try to minimise the number of times that they are performed.</remarks>
        private List<IAircraft> FilterAircraft(List<IAircraft> aircraftListSnapshot, AircraftListJsonBuilderArgs args, ref Dictionary<int, double?> distances)
        {
            List<IAircraft> result = new List<IAircraft>();

            foreach(var aircraft in aircraftListSnapshot) {
                if(!PassesFilter(aircraft, args.Filter)) continue;

                var distance = args.IsFlightSimulatorList ? null : GreatCircleMaths.Distance(args.BrowserLatitude, args.BrowserLongitude, aircraft.Latitude, aircraft.Longitude);
                if(args.Filter != null) {
                    if(args.Filter.DistanceLower != null && (distance == null || distance < args.Filter.DistanceLower)) continue;
                    if(args.Filter.DistanceUpper != null && (distance == null || distance > args.Filter.DistanceUpper)) continue;
                }

                result.Add(aircraft);
                distances.Add(aircraft.UniqueId, distance);
            }

            return result;
        }

        /// <summary>
        /// Sorts the aircraft list using the parameters in the args object.
        /// </summary>
        /// <param name="aircraftListSnapshot"></param>
        /// <param name="args"></param>]
        /// <param name="distances"></param>
        private void SortAircraft(List<IAircraft> aircraftListSnapshot, AircraftListJsonBuilderArgs args, Dictionary<int, double?> distances)
        {
            IAircraftComparer comparer = Factory.Singleton.Resolve<IAircraftComparer>();
            comparer.BrowserLocation = args.BrowserLatitude == null || args.BrowserLongitude == null ? null : new Coordinate((float)args.BrowserLatitude, (float)args.BrowserLongitude);
            foreach(var sortBy in args.SortBy) {
                comparer.SortBy.Add(sortBy);
            }
            foreach(var distance in distances) {
                comparer.PrecalculatedDistances.Add(distance.Key, distance.Value);
            }

            aircraftListSnapshot.Sort(comparer);
        }

        /// <summary>
        /// Copies the aircraft from the snapshot to the JSON object.
        /// </summary>
        /// <param name="aircraftListJson"></param>
        /// <param name="aircraftListSnapshot"></param>
        /// <param name="args"></param>
        /// <param name="distances"></param>
        private void CopyAircraft(AircraftListJson aircraftListJson, List<IAircraft> aircraftListSnapshot, AircraftListJsonBuilderArgs args, Dictionary<int, double?> distances)
        {
            var now = _Provider.UtcNow;

            foreach(var aircraftSnapshot in aircraftListSnapshot) {
                double? distance;
                if(!distances.TryGetValue(aircraftSnapshot.UniqueId, out distance)) distance = null;

                var aircraftJson = new AircraftJson() {
                    BearingFromHere = GreatCircleMaths.Bearing(args.BrowserLatitude, args.BrowserLongitude, aircraftSnapshot.Latitude, aircraftSnapshot.Longitude, null, false, true),
                    DistanceFromHere = distance == null ? (double?)null : Math.Round(distance.Value, 2),
                    UniqueId = aircraftSnapshot.UniqueId,
                };
                if(aircraftJson.BearingFromHere != null) aircraftJson.BearingFromHere = Math.Round(aircraftJson.BearingFromHere.Value, 1);

                bool firstTimeSeen = !args.PreviousAircraft.Contains(aircraftSnapshot.UniqueId);

                if(firstTimeSeen || aircraftSnapshot.AltitudeChanged > args.PreviousDataVersion)                aircraftJson.Altitude = aircraftSnapshot.Altitude;
                if(firstTimeSeen || aircraftSnapshot.CallsignChanged > args.PreviousDataVersion)                aircraftJson.Callsign = aircraftSnapshot.Callsign;
                if(firstTimeSeen || aircraftSnapshot.CallsignIsSuspectChanged > args.PreviousDataVersion)       aircraftJson.CallsignIsSuspect = aircraftSnapshot.CallsignIsSuspect;
                if(firstTimeSeen || aircraftSnapshot.ConstructionNumberChanged > args.PreviousDataVersion)      aircraftJson.ConstructionNumber = aircraftSnapshot.ConstructionNumber;
                if(firstTimeSeen || aircraftSnapshot.CountMessagesReceivedChanged > args.PreviousDataVersion)   aircraftJson.CountMessagesReceived = aircraftSnapshot.CountMessagesReceived;
                if(firstTimeSeen || aircraftSnapshot.DestinationChanged > args.PreviousDataVersion)             aircraftJson.Destination = aircraftSnapshot.Destination;
                if(firstTimeSeen || aircraftSnapshot.GroundSpeedChanged > args.PreviousDataVersion)             aircraftJson.GroundSpeed = Round.GroundSpeed(aircraftSnapshot.GroundSpeed);
                if(firstTimeSeen || aircraftSnapshot.EmergencyChanged > args.PreviousDataVersion)               aircraftJson.Emergency = aircraftSnapshot.Emergency;
                if(firstTimeSeen || aircraftSnapshot.EngineTypeChanged > args.PreviousDataVersion)              aircraftJson.EngineType = (int)aircraftSnapshot.EngineType;
                if(firstTimeSeen || aircraftSnapshot.FirstSeenChanged > args.PreviousDataVersion)               aircraftJson.FirstSeen = aircraftSnapshot.FirstSeen;
                if(firstTimeSeen || aircraftSnapshot.FlightsCountChanged > args.PreviousDataVersion)            aircraftJson.FlightsCount = aircraftSnapshot.FlightsCount;
                if(firstTimeSeen || aircraftSnapshot.PictureFileNameChanged > args.PreviousDataVersion)         aircraftJson.HasPicture = !String.IsNullOrEmpty(aircraftSnapshot.PictureFileName);
                if(firstTimeSeen || aircraftSnapshot.Icao24Changed > args.PreviousDataVersion)                  aircraftJson.Icao24 = aircraftSnapshot.Icao24;
                if(firstTimeSeen || aircraftSnapshot.Icao24CountryChanged > args.PreviousDataVersion)           aircraftJson.Icao24Country = aircraftSnapshot.Icao24Country;
                if(firstTimeSeen || aircraftSnapshot.Icao24InvalidChanged > args.PreviousDataVersion)           aircraftJson.Icao24Invalid = aircraftSnapshot.Icao24Invalid;
                if(firstTimeSeen || aircraftSnapshot.IsMilitaryChanged > args.PreviousDataVersion)              aircraftJson.IsMilitary = aircraftSnapshot.IsMilitary;
                if(firstTimeSeen || aircraftSnapshot.IsInterestingChanged > args.PreviousDataVersion)           aircraftJson.IsInteresting = aircraftSnapshot.IsInteresting;
                if(firstTimeSeen || aircraftSnapshot.LatitudeChanged > args.PreviousDataVersion)                aircraftJson.Latitude = Round.Coordinate(aircraftSnapshot.Latitude);
                if(firstTimeSeen || aircraftSnapshot.LongitudeChanged > args.PreviousDataVersion)               aircraftJson.Longitude = Round.Coordinate(aircraftSnapshot.Longitude);
                if(firstTimeSeen || aircraftSnapshot.ModelChanged > args.PreviousDataVersion)                   aircraftJson.Model = aircraftSnapshot.Model;
                if(firstTimeSeen || aircraftSnapshot.NumberOfEnginesChanged > args.PreviousDataVersion)         aircraftJson.NumberOfEngines = aircraftSnapshot.NumberOfEngines;
                if(firstTimeSeen || aircraftSnapshot.OnGroundChanged > args.PreviousDataVersion)                aircraftJson.OnGround = aircraftSnapshot.OnGround;
                if(firstTimeSeen || aircraftSnapshot.OperatorChanged > args.PreviousDataVersion)                aircraftJson.Operator = aircraftSnapshot.Operator;
                if(firstTimeSeen || aircraftSnapshot.OperatorIcaoChanged > args.PreviousDataVersion)            aircraftJson.OperatorIcao = aircraftSnapshot.OperatorIcao;
                if(firstTimeSeen || aircraftSnapshot.OriginChanged > args.PreviousDataVersion)                  aircraftJson.Origin = aircraftSnapshot.Origin;
                if(firstTimeSeen || aircraftSnapshot.PositionTimeChanged > args.PreviousDataVersion)            aircraftJson.PositionTime = aircraftSnapshot.PositionTime == null ? (long?)null : JavascriptHelper.ToJavascriptTicks(aircraftSnapshot.PositionTime.Value);
                if(firstTimeSeen || aircraftSnapshot.RegistrationChanged > args.PreviousDataVersion)            aircraftJson.Registration = aircraftSnapshot.Registration;
                if(firstTimeSeen || aircraftSnapshot.SpeciesChanged > args.PreviousDataVersion)                 aircraftJson.Species = (int)aircraftSnapshot.Species;
                if(firstTimeSeen || aircraftSnapshot.SpeedTypeChanged > args.PreviousDataVersion)               aircraftJson.SpeedType = (int)aircraftSnapshot.SpeedType;
                if(firstTimeSeen || aircraftSnapshot.SquawkChanged > args.PreviousDataVersion)                  aircraftJson.Squawk = String.Format("{0:0000}", aircraftSnapshot.Squawk);
                if(firstTimeSeen || aircraftSnapshot.TrackChanged > args.PreviousDataVersion)                   aircraftJson.Track = Round.Track(aircraftSnapshot.Track);
                if(firstTimeSeen || aircraftSnapshot.TypeChanged > args.PreviousDataVersion)                    aircraftJson.Type = aircraftSnapshot.Type;
                if(firstTimeSeen || aircraftSnapshot.UserTagChanged > args.PreviousDataVersion)                 aircraftJson.UserTag = aircraftSnapshot.UserTag;
                if(firstTimeSeen || aircraftSnapshot.VerticalRateChanged > args.PreviousDataVersion)            aircraftJson.VerticalRate = aircraftSnapshot.VerticalRate;
                if(firstTimeSeen || aircraftSnapshot.WakeTurbulenceCategoryChanged > args.PreviousDataVersion)  aircraftJson.WakeTurbulenceCategory = (int)aircraftSnapshot.WakeTurbulenceCategory;

                if(aircraftSnapshot.Stopovers.Count > 0 && (firstTimeSeen || aircraftSnapshot.StopoversChanged > args.PreviousDataVersion)) {
                    aircraftJson.Stopovers = new List<string>();
                    aircraftJson.Stopovers.AddRange(aircraftSnapshot.Stopovers);
                }

                aircraftJson.SecondsTracked = (long)((now - aircraftSnapshot.FirstSeen).TotalSeconds);

                if(args.ShowShortTrail) {
                    if(aircraftSnapshot.ShortCoordinates.Count > 0) {
                        BuildCoordinatesList(true, firstTimeSeen, aircraftJson, aircraftSnapshot, args);
                    }
                } else {
                    if(aircraftSnapshot.FullCoordinates.Count > 0) {
                        BuildCoordinatesList(false, firstTimeSeen, aircraftJson, aircraftSnapshot, args);
                    }
                }

                aircraftListJson.Aircraft.Add(aircraftJson);
            }
        }

        /// <summary>
        /// Builds the full or short coordinates list and attaches it to the aircraft JSON object.
        /// </summary>
        /// <param name="shortCoordinates"></param>
        /// <param name="firstTimeSeen"></param>
        /// <param name="aircraftJson"></param>
        /// <param name="aircraftSnapshot"></param>
        /// <param name="args"></param>
        private void BuildCoordinatesList(bool shortCoordinates, bool firstTimeSeen, AircraftJson aircraftJson, IAircraft aircraftSnapshot, AircraftListJsonBuilderArgs args)
        {
            aircraftJson.ResetTrail = firstTimeSeen || args.ResendTrails || aircraftSnapshot.FirstCoordinateChanged > args.PreviousDataVersion;
            List<double?> list = new List<double?>();

            Coordinate lastCoordinate = null;
            foreach(var coordindate in shortCoordinates ? aircraftSnapshot.ShortCoordinates : aircraftSnapshot.FullCoordinates) {
                if(aircraftJson.ResetTrail || coordindate.DataVersion > args.PreviousDataVersion) {
                    list.Add(Round.Coordinate(coordindate.Latitude));
                    list.Add(Round.Coordinate(coordindate.Longitude));
                    if(shortCoordinates) list.Add(JavascriptHelper.ToJavascriptTicks(coordindate.Tick));
                    else                 list.Add(Round.Track(coordindate.Heading));
                }

                lastCoordinate = coordindate;
            }

            if(aircraftSnapshot.Latitude != null && aircraftSnapshot.Longitude != null &&
               (lastCoordinate.Latitude != aircraftSnapshot.Latitude || lastCoordinate.Longitude != aircraftSnapshot.Longitude) &&
               aircraftSnapshot.PositionTimeChanged > args.PreviousDataVersion) {
                list.Add(Round.Coordinate(aircraftSnapshot.Latitude));
                list.Add(Round.Coordinate(aircraftSnapshot.Longitude));
                if(shortCoordinates) list.Add(JavascriptHelper.ToJavascriptTicks(aircraftSnapshot.PositionTime.Value));
                else                 list.Add(Round.Track(aircraftSnapshot.Track));
            }

            if(list.Count != 0) {
                if(shortCoordinates) aircraftJson.ShortCoordinates = list;
                else                 aircraftJson.FullCoordinates = list;
            }
        }

        /// <summary>
        /// Returns true if the aircraft passes the filter criteria passed across.
        /// </summary>
        /// <param name="aircraft"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        private bool PassesFilter(IAircraft aircraft, AircraftListJsonBuilderFilter filter)
        {
            bool result = filter == null;

            if(!result) {
                result = true;
                if(filter.AltitudeLower != null)                                  result = aircraft.Altitude != null && aircraft.Altitude >= filter.AltitudeLower;
                if(result && filter.AltitudeUpper != null)                        result = aircraft.Altitude != null && aircraft.Altitude <= filter.AltitudeUpper;
                if(result && !String.IsNullOrEmpty(filter.CallsignContains))      result = aircraft.Callsign != null && aircraft.Callsign.Contains(filter.CallsignContains);
                if(result && filter.EngineTypeEquals != null)                     result = aircraft.EngineType == filter.EngineTypeEquals;
                if(result && !String.IsNullOrEmpty(filter.Icao24CountryContains)) result = aircraft.Icao24Country != null && aircraft.Icao24Country.ToUpperInvariant().Contains(filter.Icao24CountryContains);
                if(result && filter.IsInterestingEquals != null)                  result = aircraft.IsInteresting == filter.IsInterestingEquals;
                if(result && filter.IsMilitaryEquals != null)                     result = aircraft.IsMilitary == filter.IsMilitaryEquals;
                if(result && filter.MustTransmitPosition)                         result = aircraft.Latitude != null && aircraft.Longitude != null;
                if(result && !String.IsNullOrEmpty(filter.OperatorContains))      result = aircraft.Operator != null && aircraft.Operator.ToUpperInvariant().Contains(filter.OperatorContains);
                if(result && filter.PositionWithin != null)                       result = IsWithinBounds(aircraft.Latitude, aircraft.Longitude, filter.PositionWithin);
                if(result && !String.IsNullOrEmpty(filter.RegistrationContains))  result = aircraft.Registration != null && aircraft.Registration.Contains(filter.RegistrationContains);
                if(result && filter.SpeciesEquals != null)                        result = aircraft.Species == filter.SpeciesEquals;
                if(result && filter.SquawkLower != null)                          result = aircraft.Squawk != null && aircraft.Squawk >= filter.SquawkLower;
                if(result && filter.SquawkUpper != null)                          result = aircraft.Squawk != null && aircraft.Squawk <= filter.SquawkUpper;
                if(result && !String.IsNullOrEmpty(filter.TypeStartsWith))        result = aircraft.Type != null && aircraft.Type.StartsWith(filter.TypeStartsWith);
                if(result && filter.WakeTurbulenceCategoryEquals != null)         result = aircraft.WakeTurbulenceCategory == filter.WakeTurbulenceCategoryEquals;
            }

            return result;
        }

        /// <summary>
        /// Returns true if an aircraft at the latitude and longitude passed across is within the rectangle on the surface
        /// of the earth described by the pair of coordinates passed across, where the first coordinate is top-left and
        /// the second is bottom-right.
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        private bool IsWithinBounds(double? latitude, double? longitude, Pair<Coordinate> bounds)
        {
            bool result = latitude != null && longitude != null;
            if(result) {
                result = false;

                // Latitude is simple because we assume there is nothing past the poles... from north to south the earth is flat :)
                result = bounds.First.Latitude >= latitude && bounds.Second.Latitude <= latitude;

                if(result) {
                    // Longitude is harder because if the bounding box straddles the anti-meridian then the normal comparison of coordinates
                    // fails. When it straddles the anti-meridian the left edge is a +ve value < 180 and the right edge is a -ve value > -180.
                    // When this happens we can just look to see if the longitude is +ve or -ve. If it's +ve it has to be between the left
                    // edge and 180, if it's -ve is has to be between -180 and the right edge. We won't bother enforcing the 180 thing, if they
                    // enter invalid coordinates then the result is undefined.
                    if(bounds.First.Longitude >= 0 && bounds.Second.Longitude < 0) {
                        result = longitude >= 0 ? bounds.First.Longitude <= longitude : bounds.Second.Longitude >= longitude;
                    } else {
                        result = bounds.First.Longitude <= longitude && bounds.Second.Longitude >= longitude;
                    }
                }
            }

            return result;
        }
        #endregion

        #region Events subscribed
        /// <summary>
        /// Called when the configuration changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ConfigurationStorage_ConfigurationChanged(object sender, EventArgs args)
        {
            LoadConfiguration();
        }
        #endregion
    }
}
