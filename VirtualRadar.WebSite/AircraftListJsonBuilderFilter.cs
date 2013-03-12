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
using VirtualRadar.Interface;
using VirtualRadar.Interface.StandingData;

namespace VirtualRadar.WebSite
{
    /// <summary>
    /// The arguments passed to <see cref="AircraftListJsonBuilder.Build"/> that can be used to suppress
    /// aircraft from the list returned to the browser.
    /// </summary>
    class AircraftListJsonBuilderFilter
    {
        /// <summary>
        /// Gets or sets the lowest altitude that an aircraft can be flying at in order to pass the filter.
        /// </summary>
        public int? AltitudeLower { get; set; }

        /// <summary>
        /// Gets or sets the highest altitude that an aircraft can be flying at in order to pass the filter.
        /// </summary>
        public int? AltitudeUpper { get; set; }

        /// <summary>
        /// Gets or sets the text that must be contained within an aircraft's callsign before it can pass the filter.
        /// </summary>
        public string CallsignContains { get; set; }

        /// <summary>
        /// Gets or sets the lowest distance in kilometres that the aircraft can be at before it can pass the filter.
        /// </summary>
        public double? DistanceLower { get; set; }

        /// <summary>
        /// Gets or sets the highest distance in kilometres that the aircraft can be at before it can pass the filter.
        /// </summary>
        public double? DistanceUpper { get; set; }

        /// <summary>
        /// Gets or sets the engine type that the aircraft must have before it can pass the filter.
        /// </summary>
        public EngineType? EngineTypeEquals { get; set; }

        /// <summary>
        /// Gets or sets the text that must be contained within an aircraft's ICAO24 country before it can pass the filter.
        /// </summary>
        public string Icao24CountryContains { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that the aircraft must be flagged as Interested in the BaseStation database before it can pass the filter.
        /// </summary>
        public bool? IsInterestingEquals { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that the aircraft must be operated by the military before it can pass the filter.
        /// </summary>
        public bool? IsMilitaryEquals { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that the aircraft must be transmitting a position before it can pass the filter.
        /// </summary>
        public bool MustTransmitPosition { get; set; }

        /// <summary>
        /// Gets or sets the text that an aircraft's operator must contain to pass the filter.
        /// </summary>
        public string OperatorContains { get; set; }

        /// <summary>
        /// Gets or sets the lines of latitude and longitude that the aircraft must be within before it can pass the filter.
        /// The first coordinate is Top-Left and the second is Bottom-Right.
        /// </summary>
        public Pair<Coordinate> PositionWithin { get; set; }

        /// <summary>
        /// Gets or sets the text that must be contained within an aircraft's registration before it can pass the filter.
        /// </summary>
        public string RegistrationContains { get; set; }

        /// <summary>
        /// Gets or sets the aircraft species that is allowed to pass the filter.
        /// </summary>
        public Species? SpeciesEquals { get; set; }

        /// <summary>
        /// Gets or sets the lowest squawk value that allows an aircraft to pass the filter.
        /// </summary>
        public int? SquawkLower { get; set; }

        /// <summary>
        /// Gets or sets the highest squawk value that allows an aircraft to pass the filter.
        /// </summary>
        public int? SquawkUpper { get; set; }

        /// <summary>
        /// Gets or sets the text that the aircraft type must start with before it can pass the filter.
        /// </summary>
        public string TypeStartsWith { get; set; }

        /// <summary>
        /// Gets or sets the wake turbulence category that the aircraft must have before it can pass the filter.
        /// </summary>
        public WakeTurbulenceCategory? WakeTurbulenceCategoryEquals { get; set; }
    }
}
