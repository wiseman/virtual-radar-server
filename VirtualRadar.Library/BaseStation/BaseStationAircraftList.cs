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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Database;
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.StandingData;

namespace VirtualRadar.Library.BaseStation
{
    /// <summary>
    /// The default implementation of <see cref="IBaseStationAircraftList"/>.
    /// </summary>
    sealed class BaseStationAircraftList : IBaseStationAircraftList
    {
        #region Private Class - DefaultProvider
        /// <summary>
        /// The default implementation of <see cref="IBaseStationAircraftListProvider"/>.
        /// </summary>
        class DefaultProvider : IBaseStationAircraftListProvider
        {
            public DateTime UtcNow { get { return DateTime.UtcNow; } }
        }
        #endregion

        #region Private Class - DatabaseLookup
        /// <summary>
        /// A private class that holds information about an aircraft that needs to be looked up.
        /// </summary>
        class DatabaseLookup
        {
            public IAircraft Aircraft;
            public bool QueueForRefresh;
            public bool AlwaysRefreshPicture;

            /// <summary>
            /// Creates a new object
            /// </summary>
            /// <param name="aircraft">The aircraft to look up in the database.</param>
            /// <param name="queueForRefresh">True if another lookup should be scheduled if the aircraft is either missing from the database
            /// or the record on the database is incomplete.</param>
            /// <param name="alwaysRefreshPicture">Usually the aircraft picture is only refreshed if the aircraft has a registration added by
            /// the database lookup - this parameter can force a lookup even if the registration is missing or not found.</param>
            public DatabaseLookup(IAircraft aircraft, bool queueForRefresh, bool alwaysRefreshPicture)
            {
                Aircraft = aircraft;
                QueueForRefresh = queueForRefresh;
                AlwaysRefreshPicture = alwaysRefreshPicture;
            }
        }
        #endregion

        #region Private Class - TrackCalculationParameters
        /// <summary>
        /// A private class that records parameters necessary for calculating tracks.
        /// </summary>
        class TrackCalculationParameters
        {
            /// <summary>
            /// Gets or sets the latitude used for the last track calculation.
            /// </summary>
            public double LastLatitude { get; set; }

            /// <summary>
            /// Gets or sets the longitude used for the last track calculation.
            /// </summary>
            public double LastLongitude { get; set; }

            /// <summary>
            /// Gets or sets the track last transmitted for the aircraft.
            /// </summary>
            public float? LastTransmittedTrack { get; set; }

            /// <summary>
            /// Gets or sets a value indicating that the transmitted track on ground appears to
            /// have locked to the track as it was when the aircraft was first started up.
            /// </summary>
            /// <remarks>
            /// This problem appears to affect 757-200s. When going from airborne to surface the
            /// SurfacePosition tracks are correct, but when the aircraft is started the tracks
            /// in SurfacePositions lock to the heading the aircraft was in on startup and never
            /// report the correct track until after the aircraft has taken off and landed.
            /// </remarks>
            public bool TrackFrozen { get; set; }

            /// <summary>
            /// Gets or sets the time at UTC when the track was considered to be frozen. Frozen
            /// tracks are expired - some operators continue to transmit messages for many hours
            /// while the aircraft is on the ground; because the track after landing was correct
            /// it will still be considered to be correct once the aircraft taxis to takeoff,
            /// this reset prevents that.
            /// </summary>
            public DateTime TrackFrozenAt { get; set; }
        }
        #endregion

        #region Fields
        /// <summary>
        /// Number of ticks in a second.
        /// </summary>
        private const long TicksPerSecond = 10000000L;

        /// <summary>
        /// True once <see cref="Start"/> has been called. This indicates that all properties are in a good
        /// state. Although properties such as the listener and database can be changed at any time the intention
        /// is that they are configured once and then remain constant over the lifetime of the aircraft list.
        /// </summary>
        private bool _Started;

        /// <summary>
        /// The last DataVersion applied to an aircraft.
        /// </summary>
        private long _DataVersion;

        /// <summary>
        /// A map of unique identifiers to aircraft objects.
        /// </summary>
        private Dictionary<int, IAircraft> _AircraftMap = new Dictionary<int, IAircraft>();

        /// <summary>
        /// The object that synchronises access to <see cref="_AircraftMap"/>.
        /// </summary>
        private object _AircraftMapLock = new object();

        /// <summary>
        /// The object that synchronises access to the fields that are copied from the current configuration.
        /// </summary>
        private object _ConfigurationLock = new object();

        /// <summary>
        /// The number of seconds of coordinates that are held in the ShortCoordinates list for aircraft.
        /// </summary>
        private int _ShortTrailLengthSeconds;

        /// <summary>
        /// The number of seconds that has to elapse since the last message for an aircraft before <see cref="TakeSnapshot"/>
        /// suppresses it from the returned list.
        /// </summary>
        private int _SnapshotTimeoutSeconds;

        /// <summary>
        /// The number of seconds that has to elapse before old aircraft are removed from the list.
        /// </summary>
        private int _TrackingTimeoutSeconds;

        /// <summary>
        /// The object that looks up aircraft pictures for us. This is usually called on a background thread as some operations, e.g.
        /// searching for every aircraft's picture at once after a configuration change, does not block processing of messages.
        /// </summary>
        private IAircraftPictureManager _PictureManager;

        /// <summary>
        /// The object that keeps a cache of the files in the picture folder.
        /// </summary>
        private IDirectoryCache _PictureDirectoryCache;

        /// <summary>
        /// The queue of aircraft that need to have their picture looked up.
        /// </summary>
        /// <remarks>
        /// We look these up on a separate thread because it could be quite an expensive operation, particularly if the picture folder
        /// changes when there are hundreds of aircraft in the list. All of the aircraft need to have their pictures re-checked.
        /// </remarks>
        private BackgroundThreadQueue<IAircraft> _PictureLookupQueue = new BackgroundThreadQueue<IAircraft>("PictureLookupQueue");

        /// <summary>
        /// The queue of aircraft that need their details fetching from the database.
        /// </summary>
        /// <remarks>
        /// SQLite is kind-of multithreaded but only inasmuch as it blocks other threads while one thread is using the database. The
        /// <see cref="IBaseStationDatabase"/> implementation polices this by also blocking threads while one thread is running SQL
        /// statements. If we attempt to lookup database details during the processing of aircraft messages while a report is running
        /// or another thread is hammering the database then our message processing thread can block. We don't NEED to know database
        /// details straight away so instead we stick the aircraft into this queue and have a background thread read the details from
        /// the database for it.
        /// </remarks>
        private BackgroundThreadQueue<DatabaseLookup> _DatabaseLookupQueue = new BackgroundThreadQueue<DatabaseLookup>("DatabaseLookupQueue");

        /// <summary>
        /// A map of aircraft identifiers to the parameters used for calculating its track. This is a parallel list to
        /// <see cref="_AircraftMap"/> and is locked using <see cref="_AircraftMapLock"/>.
        /// </summary>
        private Dictionary<int, TrackCalculationParameters> _CalculatedTrackCoordinates = new Dictionary<int,TrackCalculationParameters>();

        /// <summary>
        /// A map of aircraft identifiers to the time that the details for the aircraft should be refreshed from the database.
        /// </summary>
        private Dictionary<int, DateTime> _RefreshDatabaseTimes = new Dictionary<int,DateTime>();

        /// <summary>
        /// The object that locks access to <see cref="_RefreshDatabaseTimes"/>.
        /// </summary>
        private object _RefreshDatabaseTimesLock = new Object();

        /// <summary>
        /// The time that the last removal of old aircraft from the list was performed.
        /// </summary>
        private DateTime _LastRemoveOldAircraftTime;

        /// <summary>
        /// A copy of the prefer IATA codes setting from the configuration.
        /// </summary>
        private bool _PreferIataAirportCodes;
        #endregion

        #region Properties
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IBaseStationAircraftListProvider Provider { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public AircraftListSource Source { get { return AircraftListSource.BaseStation; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int Count
        {
            get
            {
                int result;
                lock(_AircraftMap) result = _AircraftMap.Count;
                return result;
            }
        }

        IListener _Port30003Listener;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IListener Listener
        {
            get { return _Port30003Listener; }
            set
            {
                if(_Port30003Listener != value) {
                    if(_Port30003Listener != null) {
                        _Port30003Listener.Port30003MessageReceived -= BaseStationListener_MessageReceived;
                        _Port30003Listener.SourceChanged -= BaseStationListener_SourceChanged;
                        _Port30003Listener.PositionReset -= BaseStationListener_PositionReset;
                    }
                    _Port30003Listener = value;
                    if(_Port30003Listener != null) {
                        _Port30003Listener.Port30003MessageReceived += BaseStationListener_MessageReceived;
                        _Port30003Listener.SourceChanged += BaseStationListener_SourceChanged;
                        _Port30003Listener.PositionReset += BaseStationListener_PositionReset;
                    }
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IBaseStationDatabase BaseStationDatabase { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IStandingDataManager StandingDataManager { get; set; }
        #endregion

        #region Events
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler<EventArgs<Exception>> ExceptionCaught;

        /// <summary>
        /// Raises <see cref="ExceptionCaught"/>. Note that the class is sealed, hence this is private instead of protected virtual.
        /// </summary>
        /// <param name="args"></param>
        private void OnExceptionCaught(EventArgs<Exception> args)
        {
            if(ExceptionCaught != null) ExceptionCaught(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler CountChanged;

        /// <summary>
        /// Raises <see cref="CountChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        private void OnCountChanged(EventArgs args)
        {
            if(CountChanged != null) CountChanged(this, args);
        }
        #endregion

        #region Constructor and Finaliser
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public BaseStationAircraftList()
        {
            Provider = new DefaultProvider();

            _PictureManager = Factory.Singleton.Resolve<IAircraftPictureManager>().Singleton;
            _PictureDirectoryCache = Factory.Singleton.Resolve<IAutoConfigPictureFolderCache>().Singleton.DirectoryCache;
            _PictureDirectoryCache.CacheChanged += PictureDirectoryCache_CacheChanged;

            _PictureLookupQueue.StartBackgroundThread(SearchForPicture, (ex) => { OnExceptionCaught(new EventArgs<Exception>(ex)); });
            _DatabaseLookupQueue.StartBackgroundThread(LoadAircraftDetails, (ex) => { OnExceptionCaught(new EventArgs<Exception>(ex)); });
        }

        /// <summary>
        /// Finalises the object.
        /// </summary>
        ~BaseStationAircraftList()
        {
            Dispose(false);
        }
        #endregion

        #region Dispose
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalises or disposes of the object. Note that this class is sealed.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if(disposing) {
                if(_Port30003Listener != null) _Port30003Listener.Port30003MessageReceived -= BaseStationListener_MessageReceived;
                if(_PictureLookupQueue != null) _PictureLookupQueue.Dispose();
                if(_DatabaseLookupQueue != null) _DatabaseLookupQueue.Dispose();

                if(_PictureDirectoryCache != null) {
                    _PictureDirectoryCache.CacheChanged -= PictureDirectoryCache_CacheChanged;
                    _PictureDirectoryCache = null;
                }
            }
        }
        #endregion

        #region Start, LoadConfiguration
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void Start()
        {
            if(!_Started) {
                if(Listener == null) throw new InvalidOperationException("You must supply a Port30003 listener before the aircraft list can be started");
                if(BaseStationDatabase == null) throw new InvalidOperationException("You must supply a database before the aircraft list can be started");
                if(StandingDataManager == null) throw new InvalidOperationException("You must supply a standing data manager before the aircraft list can be started");

                LoadConfiguration();

                var configurationStorage = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton;
                configurationStorage.ConfigurationChanged += ConfigurationStorage_ConfigurationChanged;

                Factory.Singleton.Resolve<IHeartbeatService>().Singleton.SlowTick += Heartbeat_SlowTick;
                Factory.Singleton.Resolve<IHeartbeatService>().Singleton.FastTick += Heartbeat_FastTick;
                Factory.Singleton.Resolve<IStandingDataManager>().Singleton.LoadCompleted += StandingDataManager_LoadCompleted;

                _Started = true;
            }
        }

        /// <summary>
        /// Reads all of the important values out of the configuration file.
        /// </summary>
        private void LoadConfiguration()
        {
            var configuration = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton.Load();

            lock(_ConfigurationLock) {
                _ShortTrailLengthSeconds = configuration.GoogleMapSettings.ShortTrailLengthSeconds;
                _SnapshotTimeoutSeconds = configuration.BaseStationSettings.DisplayTimeoutSeconds;
                _TrackingTimeoutSeconds = configuration.BaseStationSettings.TrackingTimeoutSeconds;
                _PreferIataAirportCodes = configuration.GoogleMapSettings.PreferIataAirportCodes;
            }
        }
        #endregion

        #region ProcessMessage, ApplyMessageToAircraft, CalculateTrack
        /// <summary>
        /// Adds information contained within the message to the object held for the aircraft (creating a new object if one
        /// does not already exist).
        /// </summary>
        /// <param name="message"></param>
        private void ProcessMessage(BaseStationMessage message)
        {
            try {
                if(message.MessageType == BaseStationMessageType.Transmission) {
                    var uniqueId = ConvertIcaoToUniqueId(message.Icao24);
                    if(uniqueId != -1) {
                        bool isNewAircraft = false;

                        // This must keep the aircraft map locked until the aircraft is fully formed. This means that we have a lock within a lock,
                        // so care must be taken to avoid deadlocks.
                        lock(_AircraftMapLock) {
                            IAircraft aircraft;
                            isNewAircraft = !_AircraftMap.TryGetValue(uniqueId, out aircraft);
                            if(isNewAircraft) {
                                aircraft = Factory.Singleton.Resolve<IAircraft>();
                                aircraft.UniqueId = uniqueId;
                                _AircraftMap.Add(uniqueId, aircraft);
                            }

                            ApplyMessageToAircraft(message, aircraft, isNewAircraft);
                        }

                        if(isNewAircraft) OnCountChanged(EventArgs.Empty);
                    }
                }
            } catch(Exception ex) {
                Debug.WriteLine(String.Format("BaseStationAircraftList.ProcessMessage caught exception: {0}", ex.ToString()));
                OnExceptionCaught(new EventArgs<Exception>(ex));
            }
        }

        private void ApplyMessageToAircraft(BaseStationMessage message, IAircraft aircraft, bool isNewAircraft)
        {
            // We want to retrieve all of the lookups without writing anything to the aircraft. Then all of the values
            // that need changing on the aircraft will be set in one lock with one DataVersion so they're all consistent.

            CodeBlock codeBlock = null;
            Route route = null;

            if(isNewAircraft) codeBlock = StandingDataManager.FindCodeBlock(message.Icao24);

            bool callsignChanged;
            string operatorIcao;
            lock(aircraft) {  // <-- nothing should be changing Callsign, we're the only thread that writes it, but just in case...
                callsignChanged = !String.IsNullOrEmpty(message.Callsign) && message.Callsign != aircraft.Callsign;
                operatorIcao = aircraft.OperatorIcao;
            }
            if(callsignChanged) route = LookupRoute(message.Callsign, operatorIcao);

            var track = CalculateTrack(message, aircraft);

            lock(aircraft) {
                var now = Provider.UtcNow;
                GenerateDataVersion(aircraft);

                aircraft.LastUpdate = now;
                ++aircraft.CountMessagesReceived;
                if(isNewAircraft) aircraft.FirstSeen = now;
                if(aircraft.Icao24 == null) aircraft.Icao24 = message.Icao24;

                if(!String.IsNullOrEmpty(message.Callsign)) aircraft.Callsign = message.Callsign;
                if(message.Altitude != null) aircraft.Altitude = message.Altitude;
                if(message.GroundSpeed != null) aircraft.GroundSpeed = message.GroundSpeed;
                if(track != null) aircraft.Track = track;
                if(message.Track != null && message.Track != 0.0) aircraft.IsTransmittingTrack = true;
                if(message.Latitude != null) aircraft.Latitude = message.Latitude;
                if(message.Longitude != null) aircraft.Longitude = message.Longitude;
                if(message.VerticalRate != null) aircraft.VerticalRate = message.VerticalRate;
                if(message.OnGround != null) aircraft.OnGround = message.OnGround;
                if(message.Squawk != null) {
                    aircraft.Squawk = message.Squawk;
                    aircraft.Emergency = message.Squawk == 7500 || message.Squawk == 7600 || message.Squawk == 7700;
                }

                var supplementaryMessage = message != null && message.Supplementary != null ? message.Supplementary : null;
                if(supplementaryMessage != null) {
                    if(supplementaryMessage.SpeedType != null) aircraft.SpeedType = supplementaryMessage.SpeedType.Value;
                    if(supplementaryMessage.CallsignIsSuspect != null) aircraft.CallsignIsSuspect = supplementaryMessage.CallsignIsSuspect.Value;
                }

                ApplyCodeBlock(aircraft, codeBlock);
                ApplyRoute(aircraft, route);

                if(message.Latitude != null && message.Longitude != null) aircraft.UpdateCoordinates(now, _ShortTrailLengthSeconds);
            }

            if(isNewAircraft) _DatabaseLookupQueue.Enqueue(new DatabaseLookup(aircraft, true, true));
        }

        /// <summary>
        /// Looks up a route using the callsign and optionally the operator ICAO code.
        /// </summary>
        /// <param name="callsign"></param>
        /// <param name="operatorIcao"></param>
        /// <returns></returns>
        private Route LookupRoute(string callsign, string operatorIcao)
        {
            var result = StandingDataManager.FindRoute(callsign);

            if(result == null && !String.IsNullOrEmpty(callsign) && Char.IsDigit(callsign[0]) && !String.IsNullOrEmpty(operatorIcao)) {
                callsign = String.Format("{0}{1}", operatorIcao, callsign);
                result = StandingDataManager.FindRoute(callsign);
            }

            return result;
        }


        /// <summary>
        /// Applies the code block to the aircraft.
        /// </summary>
        /// <param name="aircraft"></param>
        /// <param name="codeBlock"></param>
        private static void ApplyCodeBlock(IAircraft aircraft, CodeBlock codeBlock)
        {
            if(codeBlock != null) {
                aircraft.Icao24Country = codeBlock.Country;
                aircraft.IsMilitary = codeBlock.IsMilitary;
            }
        }

        /// <summary>
        /// Applies route data to the aircraft.
        /// </summary>
        /// <param name="aircraft"></param>
        /// <param name="route"></param>
        private void ApplyRoute(IAircraft aircraft, Route route)
        {
            if(route != null) {
                aircraft.Origin = Describe.Airport(route.From, _PreferIataAirportCodes);
                aircraft.Destination = Describe.Airport(route.To, _PreferIataAirportCodes);
                foreach(var stopover in route.Stopovers) {
                    aircraft.Stopovers.Add(Describe.Airport(stopover, _PreferIataAirportCodes));
                }
            }
        }

        /// <summary>
        /// If the message contains a track then it is simply returned, otherwise if it's possible to calculate the track then
        /// it is calculated and returned.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="aircraft"></param>
        /// <returns></returns>
        private float? CalculateTrack(BaseStationMessage message, IAircraft aircraft)
        {
            var result = message.Track;

            var onGround = message.OnGround.GetValueOrDefault();
            var positionPresent = message.Latitude != null && message.Longitude != null;
            var trackNeverTransmitted = !aircraft.IsTransmittingTrack;

            if(result == 0.0 && trackNeverTransmitted) result = null;

            if(positionPresent && (onGround || trackNeverTransmitted)) {
                TrackCalculationParameters calcParameters;
                lock(_AircraftMapLock) {
                    _CalculatedTrackCoordinates.TryGetValue(aircraft.UniqueId, out calcParameters);
                    if(calcParameters != null && onGround && calcParameters.TrackFrozenAt.AddMinutes(30) <= Provider.UtcNow) {
                        _CalculatedTrackCoordinates.Remove(aircraft.UniqueId);
                        calcParameters = null;
                    }
                }

                var trackSuspect = message.Track == null || message.Track == 0.0;
                var trackFrozen = onGround && (calcParameters == null || calcParameters.TrackFrozen);
                if(trackSuspect || trackFrozen) {
                    var trackCalculated = false;
                    if(calcParameters == null) {
                        calcParameters = new TrackCalculationParameters() { LastLatitude = message.Latitude.Value, LastLongitude = message.Longitude.Value, LastTransmittedTrack = message.Track, TrackFrozen = true, TrackFrozenAt = Provider.UtcNow };
                        lock(_AircraftMapLock) _CalculatedTrackCoordinates.Add(aircraft.UniqueId, calcParameters);
                        trackCalculated = true;
                    } else if(message.Latitude != calcParameters.LastLatitude || message.Longitude != calcParameters.LastLongitude) {
                        if(trackFrozen && onGround && calcParameters.LastTransmittedTrack != message.Track) {
                            trackFrozen = calcParameters.TrackFrozen = false;
                        }
                        if(trackSuspect || trackFrozen) {
                            var minimumDistanceKm = message.OnGround.GetValueOrDefault() ? 0.010 : 0.25;
                            if(GreatCircleMaths.Distance(message.Latitude, message.Longitude, calcParameters.LastLatitude, calcParameters.LastLongitude).GetValueOrDefault() >= minimumDistanceKm) {
                                result = (float?)GreatCircleMaths.Bearing(calcParameters.LastLatitude, calcParameters.LastLongitude, message.Latitude, message.Longitude, null, false, false);
                                result = Round.Track(result);
                                calcParameters.LastLatitude = message.Latitude.Value;
                                calcParameters.LastLongitude = message.Longitude.Value;
                                trackCalculated = true;
                            }
                            calcParameters.LastTransmittedTrack = message.Track;
                        }
                    }
                    if(!trackCalculated && (trackSuspect || trackFrozen)) result = aircraft.Track;
                }
            }

            return result;
        }
        #endregion

        #region SearchForPicture, RefreshPicture, RefreshAllPictures
        /// <summary>
        /// Looks for the picture filename for the aircraft.
        /// </summary>
        /// <param name="aircraft"></param>
        private void SearchForPicture(IAircraft aircraft)
        {
            string icao24, registration, oldFileName;
            lock(aircraft) {
                icao24 = aircraft.Icao24;
                registration = aircraft.Registration;
                oldFileName = aircraft.PictureFileName;
            }

            var fileName = _PictureManager.FindPicture(_PictureDirectoryCache, icao24, registration);

            if(oldFileName != fileName) {
                // We need to make sure that any locks are applied in the same order as any other thread that is modifying DataVersion.
                lock(_AircraftMapLock) {
                    lock(aircraft) {
                        GenerateDataVersion(aircraft);
                        aircraft.PictureFileName = fileName;
                    }
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="aircraft"></param>
        public void RefreshPicture(IAircraft aircraft)
        {
            if(aircraft == null) throw new ArgumentNullException("aircraft");

            IAircraft actualAircraft;
            lock(_AircraftMapLock) _AircraftMap.TryGetValue(aircraft.UniqueId, out actualAircraft);
            if(actualAircraft != null) _PictureLookupQueue.Enqueue(actualAircraft);
        }

        /// <summary>
        /// Puts every aircraft onto the picture lookup queue.
        /// </summary>
        private void RefreshAllPictures()
        {
            _PictureLookupQueue.Clear();
            lock(_AircraftMapLock) _PictureLookupQueue.EnqueueRange(_AircraftMap.Values);
        }
        #endregion

        #region ConvertIcaoToUniqueId, GenerateDataVersion
        /// <summary>
        /// Derives the unique identifier for the aircraft from an ICAO24 code.
        /// </summary>
        /// <param name="icao"></param>
        /// <returns></returns>
        private static int ConvertIcaoToUniqueId(string icao)
        {
            int uniqueId = -1;
            if(!String.IsNullOrEmpty(icao)) {
                try {
                    uniqueId = Convert.ToInt32(icao, 16);
                } catch(Exception ex) {
                    Debug.WriteLine(String.Format("BaseStationAircraftList.ConvertIcaoToUniqueId caught exception {0}", ex.ToString()));
                }
            }

            return uniqueId;
        }

        /// <summary>
        /// Sets a valid DataVersion. Always lock the aircraft map before calling this.
        /// </summary>
        /// <param name="aircraft"></param>
        /// <remarks>
        /// The tests call for this to be based on UtcNow, the idea being that if the webServer is quit and restarted while
        /// a browser is connected the chances are good that the browser data version will correspond with the data versions
        /// held by the new instance of the webServer. Not sure how useful that will be but it's not hard to do. However there
        /// are times when we cannot use UTC - if the clock gets reset or if two messages come in on the same tick. When that
        /// happens we just fallback to incrementing the dataversion.
        /// </remarks>
        private void GenerateDataVersion(IAircraft aircraft)
        {
            // This can be called on any of the threads that update the list. The _DataVersionLock value is really a property of
            // the aircraft map - it represents the highest possible DataVersion of any aircraft within the map. The caller should
            // have established a lock on the aircraft map before calling us to ensure that snapshots of the map are not taken until
            // all changes that involve a new DataVersion have been applied, but just in case they don't we get it again here. If
            // we don't, and if they forgot to acquire the lock, then we could get inconsistencies.
            lock(_AircraftMapLock) {
                lock(aircraft) {
                    var dataVersion = Provider.UtcNow.Ticks;
                    if(dataVersion <= _DataVersion) dataVersion = _DataVersion + 1;
                    aircraft.DataVersion = _DataVersion = dataVersion;
                }
            }
        }
        #endregion

        #region FindAircraft, TakeSnapshot
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <returns></returns>
        public IAircraft FindAircraft(int uniqueId)
        {
            IAircraft result = null;

            lock(_AircraftMapLock) {
                IAircraft aircraft;
                if(_AircraftMap.TryGetValue(uniqueId, out aircraft)) {
                    lock(aircraft) {
                        result = (IAircraft)aircraft.Clone();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="snapshotTimeStamp"></param>
        /// <param name="snapshotDataVersion"></param>
        /// <returns></returns>
        public List<IAircraft> TakeSnapshot(out long snapshotTimeStamp, out long snapshotDataVersion)
        {
            snapshotTimeStamp = Provider.UtcNow.Ticks;
            snapshotDataVersion = -1L;

            long hideThreshold;
            lock(_ConfigurationLock) {
                hideThreshold = snapshotTimeStamp - (_SnapshotTimeoutSeconds * TicksPerSecond);
            }

            List<IAircraft> result = new List<IAircraft>();
            lock(_AircraftMapLock) {
                foreach(var aircraft in _AircraftMap.Values) {
                    if(aircraft.LastUpdate.Ticks < hideThreshold) continue;
                    if(aircraft.DataVersion > snapshotDataVersion) snapshotDataVersion = aircraft.DataVersion;
                    result.Add((IAircraft)aircraft.Clone());
                }
            }

            return result;
        }
        #endregion

        #region LoadAircraftDetails, RefreshAircraftDetails, RefreshDatabaseDetails, RemoveOldAircraft, ResetAircraftList
        /// <summary>
        /// Loads details about the aircraft from the database.
        /// </summary>
        /// <param name="databaseLookup"></param>
        private void LoadAircraftDetails(DatabaseLookup databaseLookup)
        {
            var aircraft = databaseLookup.Aircraft;
            bool lookupPicture = databaseLookup.AlwaysRefreshPicture;

            string icao24;
            int uniqueId;
            lock(aircraft) {
                icao24 = aircraft.Icao24;
                uniqueId = aircraft.UniqueId;
            }

            var baseStationAircraft = BaseStationDatabase.GetAircraftByCode(icao24);
            if(baseStationAircraft != null) {
                var flightsCount = BaseStationDatabase.GetCountOfFlightsForAircraft(baseStationAircraft, new SearchBaseStationCriteria() { ToDate = DateTime.MaxValue });
                var typeDetails = String.IsNullOrEmpty(baseStationAircraft.ICAOTypeCode) ? null : StandingDataManager.FindAircraftType(baseStationAircraft.ICAOTypeCode);

                Route route = null;
                if(!String.IsNullOrEmpty(aircraft.Callsign) && !String.IsNullOrEmpty(baseStationAircraft.OperatorFlagCode) && Char.IsDigit(aircraft.Callsign[0])) {
                    route = StandingDataManager.FindRoute(String.Format("{0}{1}", baseStationAircraft.OperatorFlagCode, aircraft.Callsign));
                }

                lock(_AircraftMapLock) {
                    lock(aircraft) {
                        GenerateDataVersion(aircraft);

                        if(String.IsNullOrEmpty(aircraft.Registration) && !String.IsNullOrEmpty(baseStationAircraft.Registration)) lookupPicture = true;

                        aircraft.Registration = baseStationAircraft.Registration;
                        aircraft.Type = baseStationAircraft.ICAOTypeCode;
                        aircraft.Manufacturer = baseStationAircraft.Manufacturer;
                        aircraft.Model = baseStationAircraft.Type;
                        aircraft.ConstructionNumber = baseStationAircraft.SerialNo;
                        aircraft.Operator = baseStationAircraft.RegisteredOwners;
                        aircraft.OperatorIcao = baseStationAircraft.OperatorFlagCode;
                        aircraft.IsInteresting = baseStationAircraft.Interested;
                        aircraft.UserTag = baseStationAircraft.UserTag;
                        aircraft.FlightsCount = flightsCount;

                        ApplyRoute(aircraft, route);
                        ApplyAircraftType(aircraft, typeDetails);
                    }
                }
            }

            if(lookupPicture) _PictureLookupQueue.Enqueue(aircraft);

            if(baseStationAircraft == null || String.IsNullOrEmpty(baseStationAircraft.Registration)) {
                if(databaseLookup.QueueForRefresh) {
                    lock(_RefreshDatabaseTimesLock) {
                        var scheduledRefreshTime = Provider.UtcNow.AddMinutes(1);
                        if(_RefreshDatabaseTimes.ContainsKey(uniqueId)) _RefreshDatabaseTimes[uniqueId] = scheduledRefreshTime;
                        else                                            _RefreshDatabaseTimes.Add(uniqueId, scheduledRefreshTime);
                    }
                }
            }
        }

        private static void ApplyAircraftType(IAircraft aircraft, AircraftType typeDetails)
        {
            if(typeDetails != null) {
                aircraft.NumberOfEngines = typeDetails.Engines;
                aircraft.EngineType = typeDetails.EngineType;
                aircraft.Species = typeDetails.Species;
                aircraft.WakeTurbulenceCategory = typeDetails.WakeTurbulenceCategory;
            }
        }

        /// <summary>
        /// Finds every aircraft that is due to have its database details refreshed and adds
        /// them to the refresh queue. 
        /// </summary>
        private void RefreshAircraftDetails()
        {
            var now = Provider.UtcNow;

            List<int> refreshAircraftIds;
            lock(_RefreshDatabaseTimesLock) {
                refreshAircraftIds = _RefreshDatabaseTimes.Where(kvp => kvp.Value <= now).Select(kvp => kvp.Key).ToList();
                foreach(var key in refreshAircraftIds) {
                    _RefreshDatabaseTimes.Remove(key);
                }
            }

            lock(_AircraftMapLock) {
                foreach(var aircraftId in refreshAircraftIds) {
                    IAircraft aircraft;
                    if(_AircraftMap.TryGetValue(aircraftId, out aircraft)) _DatabaseLookupQueue.Enqueue(new DatabaseLookup(aircraft, false, false));
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="aircraft"></param>
        public void RefreshDatabaseDetails(IAircraft aircraft)
        {
            if(aircraft == null) throw new ArgumentNullException("aircraft");
            IAircraft actualAircraft;
            lock(_AircraftMapLock) _AircraftMap.TryGetValue(aircraft.UniqueId, out actualAircraft);

            if(actualAircraft != null) _DatabaseLookupQueue.Enqueue(new DatabaseLookup(actualAircraft, false, false));
        }

        /// <summary>
        /// Removes aircraft that have not been seen for a while.
        /// </summary>
        private void RemoveOldAircraft()
        {
            var removeList = new List<int>();

            lock(_AircraftMapLock) {
                var threshold = Provider.UtcNow.Ticks - (_TrackingTimeoutSeconds * TicksPerSecond);

                foreach(var aircraft in _AircraftMap.Values) {
                    if(aircraft.LastUpdate.Ticks < threshold) removeList.Add(aircraft.UniqueId);
                }

                foreach(var uniqueId in removeList) {
                    _AircraftMap.Remove(uniqueId);
                    if(_CalculatedTrackCoordinates.ContainsKey(uniqueId)) _CalculatedTrackCoordinates.Remove(uniqueId);
                }
            }

            lock(_RefreshDatabaseTimesLock) {
                foreach(var uniqueId in removeList) {
                    if(_RefreshDatabaseTimes.ContainsKey(uniqueId)) _RefreshDatabaseTimes.Remove(uniqueId);
                }
            }

            if(removeList.Count > 0) OnCountChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Removes all of the aircraft in the aircraft list.
        /// </summary>
        private void ResetAircraftList()
        {
            _PictureLookupQueue.Clear();
            _DatabaseLookupQueue.Clear();
            lock(_AircraftMapLock) {
                _AircraftMap.Clear();
                _CalculatedTrackCoordinates.Clear();
            }

            lock(_RefreshDatabaseTimesLock) _RefreshDatabaseTimes.Clear();

            OnCountChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Refreshes the routes for aircraft that don't have them loaded.
        /// </summary>
        private void RefreshMissingRoutes()
        {
            var standingDataManager = Factory.Singleton.Resolve<IStandingDataManager>().Singleton;

            lock(_AircraftMapLock) {
                foreach(var aircraft in _AircraftMap.Values.Where(r => String.IsNullOrEmpty(r.Origin) && !String.IsNullOrEmpty(r.Callsign))) {
                    var route = standingDataManager.FindRoute(aircraft.Callsign);
                    if(route != null) {
                        lock(aircraft) {
                            GenerateDataVersion(aircraft);
                            ApplyRoute(aircraft, route);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes code block information.
        /// </summary>
        private void RefreshCodeBlocks()
        {
            var standingDataManager = Factory.Singleton.Resolve<IStandingDataManager>().Singleton;

            lock(_AircraftMapLock) {
                foreach(var aircraft in _AircraftMap.Values) {
                    var codeBlock = standingDataManager.FindCodeBlock(aircraft.Icao24);
                    if(codeBlock != null && (aircraft.Icao24Country != codeBlock.Country || aircraft.IsMilitary != codeBlock.IsMilitary)) {
                        lock(aircraft) {
                            GenerateDataVersion(aircraft);
                            ApplyCodeBlock(aircraft, codeBlock);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes aircraft type information for aircraft that don't have type information loaded.
        /// </summary>
        private void RefreshAircraftTypes()
        {
            var standingDataManager = Factory.Singleton.Resolve<IStandingDataManager>().Singleton;

            lock(_AircraftMapLock) {
                foreach(var aircraft in _AircraftMap.Values.Where(r => !String.IsNullOrEmpty(r.Type) && String.IsNullOrEmpty(r.NumberOfEngines))) {
                    var aircraftType = standingDataManager.FindAircraftType(aircraft.Type);
                    if(aircraftType != null) {
                        lock(aircraft) {
                            GenerateDataVersion(aircraft);
                            ApplyAircraftType(aircraft, aircraftType);
                        }
                    }
                }
            }
        }
        #endregion

        #region Events consumed
        /// <summary>
        /// Raised by <see cref="IListener"/> when a message is received from BaseStation.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void BaseStationListener_MessageReceived(object sender, BaseStationMessageEventArgs args)
        {
            if(_Started) ProcessMessage(args.Message);
        }

        /// <summary>
        /// Raised by <see cref="IListener"/> when the listener's source of feed data is changing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void BaseStationListener_SourceChanged(object sender, EventArgs args)
        {
            ResetAircraftList();
        }

        /// <summary>
        /// Raised by the <see cref="IListener"/> when the listener detects that the positions up to
        /// now are not correct and need to be thrown away.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void BaseStationListener_PositionReset(object sender, EventArgs<string> args)
        {
            var key = ConvertIcaoToUniqueId(args.Value);
            lock(_AircraftMapLock) {
                IAircraft aircraft;
                if(_AircraftMap.TryGetValue(key, out aircraft)) {
                    aircraft.ResetCoordinates();
                }
            }
        }

        /// <summary>
        /// Raised when the user changes the configuration.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ConfigurationStorage_ConfigurationChanged(object sender, EventArgs args)
        {
            LoadConfiguration();
        }

        /// <summary>
        /// Periodically raised on a background thread by the heartbeat service.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Heartbeat_SlowTick(object sender, EventArgs args)
        {
            var now = Provider.UtcNow;

            // Remove old aircraft once every ten minutes. We don't have test coverage for this because we cannot
            // observe the effect - taking a snapshot of the aircraft list also removes old aircraft. This is just
            // a failsafe to prevent a buildup of objects when no-one is using the website.
            if(_LastRemoveOldAircraftTime.AddSeconds(_TrackingTimeoutSeconds) <= now) {
                _LastRemoveOldAircraftTime = now;
                RemoveOldAircraft();
            }
        }

        /// <summary>
        /// Periodically raised on a background thread by the heartbeat service.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Heartbeat_FastTick(object sender, EventArgs args)
        {
            RefreshAircraftDetails();
        }

        /// <summary>
        /// Raised when the directory cache changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void PictureDirectoryCache_CacheChanged(object sender, EventArgs args)
        {
            RefreshAllPictures();
        }

        /// <summary>
        /// Raised after the standing data has been loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void StandingDataManager_LoadCompleted(object sender, EventArgs args)
        {
            RefreshMissingRoutes();
            RefreshCodeBlocks();
            RefreshAircraftTypes();
        }
        #endregion
    }
}
