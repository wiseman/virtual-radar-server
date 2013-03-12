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
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Database;
using VirtualRadar.Interface.StandingData;
using VirtualRadar.Interface.WebServer;

namespace VirtualRadar.Interface.WebSite
{
    /// <summary>
    /// The interface for objects that bring together a collection of pages into the website that is
    /// presented to the browser.
    /// </summary>
    public interface IWebSite
    {
        /// <summary>
        /// Gets or sets the testing provider.
        /// </summary>
        IWebSiteProvider Provider { get; set; }

        /// <summary>
        /// Gets or sets the BaseStation aircraft list that the site will use when creating JSON files with aircraft details.
        /// </summary>
        IBaseStationAircraftList BaseStationAircraftList { get; set; }

        /// <summary>
        /// Gets or sets the aircraft list to use when browsers ask for the FSX aircraft list.
        /// </summary>
        ISimpleAircraftList FlightSimulatorAircraftList { get; set; }

        /// <summary>
        /// Gets or sets the BaseStation database that the site will use when generating reports.
        /// </summary>
        IBaseStationDatabase BaseStationDatabase { get; set; }

        /// <summary>
        /// Gets or sets the object that can lookup entries in the standing data on our behalf.
        /// </summary>
        IStandingDataManager StandingDataManager { get; set; }

        /// <summary>
        /// Gets the web server that the site is attached to.
        /// </summary>
        IWebServer WebServer { get; }

        /// <summary>
        /// Attaches the website to a server.
        /// </summary>
        /// <param name="server"></param>
        void AttachSiteToServer(IWebServer server);
    }
}
