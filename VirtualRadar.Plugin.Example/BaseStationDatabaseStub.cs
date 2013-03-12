// Copyright © 2012 onwards, Andrew Whewell
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

namespace VirtualRadar.Plugin.Example
{
    /// <summary>
    /// An implementation of <see cref="IBaseStationDatabase"/> that doesn't do anything.
    /// </summary>
    class BaseStationDatabaseStub : IBaseStationDatabase
    {
        class DefaultProvider : IBaseStationDatabaseProvider
        {
            public DateTime  UtcNow { get { return DateTime.UtcNow; } }
        }

        public IBaseStationDatabaseProvider Provider { get; set; }

        public string FileName { get; set; }

        public string LogFileName { get; set; }

        public bool IsConnected { get; private set; }

        public BaseStationDatabaseStub()
        {
            Provider = new DefaultProvider();
        }

        public bool TestConnection()
        {
            return true;
        }

        public bool WriteSupportEnabled { get; set; }

        #pragma warning disable 0067
        public event EventHandler  FileNameChanging;

        public event EventHandler  FileNameChanged;
        #pragma warning restore 0067

        public void CreateDatabaseIfMissing(string fileName)
        {
            ;
        }

        public BaseStationAircraft GetAircraftByRegistration(string registration)
        {
            return null;
        }

        public BaseStationAircraft GetAircraftByCode(string icao24)
        {
            return null;
        }

        public List<BaseStationFlight> GetFlightsForAircraft(BaseStationAircraft aircraft, SearchBaseStationCriteria criteria, int fromRow, int toRow, string sort1, bool sort1Ascending, string sort2, bool sort2Ascending)
        {
            return new List<BaseStationFlight>();
        }

        public int GetCountOfFlightsForAircraft(BaseStationAircraft aircraft, SearchBaseStationCriteria criteria)
        {
            return 0;
        }

        public List<BaseStationFlight> GetFlights(SearchBaseStationCriteria criteria, int fromRow, int toRow, string sortField1, bool sortField1Ascending, string sortField2, bool sortField2Ascending)
        {
            return new List<BaseStationFlight>();
        }

        public int GetCountOfFlights(SearchBaseStationCriteria criteria)
        {
            return 0;
        }

        public IList<BaseStationDBHistory> GetDatabaseHistory()
        {
            return new List<BaseStationDBHistory>();
        }

        public BaseStationDBInfo GetDatabaseVersion()
        {
            return new BaseStationDBInfo();
        }

        public IList<BaseStationSystemEvents> GetSystemEvents()
        {
            return new List<BaseStationSystemEvents>();
        }

        public void InsertSystemEvent(BaseStationSystemEvents systemEvent)
        {
            ;
        }

        public void UpdateSystemEvent(BaseStationSystemEvents systemEvent)
        {
            ;
        }

        public void DeleteSystemEvent(BaseStationSystemEvents systemEvent)
        {
            ;
        }

        public IList<BaseStationLocation> GetLocations()
        {
            return new List<BaseStationLocation>();
        }

        public void InsertLocation(BaseStationLocation location)
        {
            ;
        }

        public void UpdateLocation(BaseStationLocation location)
        {
            ;
        }

        public void DeleteLocation(BaseStationLocation location)
        {
            ;
        }

        public IList<BaseStationSession> GetSessions()
        {
            return new List<BaseStationSession>();
        }

        public void InsertSession(BaseStationSession session)
        {
            ;
        }

        public void UpdateSession(BaseStationSession session)
        {
            ;
        }

        public void DeleteSession(BaseStationSession session)
        {
            ;
        }

        public BaseStationAircraft GetAircraftById(int id)
        {
            return null;
        }

        public void InsertAircraft(BaseStationAircraft aircraft)
        {
            ;
        }

        public void UpdateAircraft(BaseStationAircraft aircraft)
        {
            ;
        }

        public void UpdateAircraftModeSCountry(int aircraftId, string modeSCountry)
        {
            ;
        }

        public void DeleteAircraft(BaseStationAircraft aircraft)
        {
            ;
        }

        public BaseStationFlight GetFlightById(int id)
        {
            return null;
        }

        public void InsertFlight(BaseStationFlight flight)
        {
            ;
        }

        public void UpdateFlight(BaseStationFlight flight)
        {
            ;
        }

        public void DeleteFlight(BaseStationFlight flight)
        {
            ;
        }

        public void StartTransaction()
        {
            ;
        }

        public void EndTransaction()
        {
            ;
        }

        public void RollbackTransaction()
        {
            ;
        }

        public void Dispose()
        {
            ;
        }
    }
}
