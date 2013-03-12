﻿// Copyright © 2010 onwards, Andrew Whewell
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
using System.IO;
using VirtualRadar.Interface.Settings;
using InterfaceFactory;

namespace VirtualRadar.Library
{
    /// <summary>
    /// The default implementation of <see cref="IAircraftPictureManager"/>.
    /// </summary>
    sealed class AircraftPictureManager : IAircraftPictureManager
    {
        private static readonly IAircraftPictureManager _Singleton = new AircraftPictureManager();
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IAircraftPictureManager Singleton { get { return _Singleton; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="directoryCache"></param>
        /// <param name="icao24"></param>
        /// <param name="registration"></param>
        /// <returns></returns>
        public string FindPicture(IDirectoryCache directoryCache, string icao24, string registration)
        {
            string result = null;

            if(!String.IsNullOrEmpty(icao24)) {
                result = SearchForPicture(directoryCache, icao24, "jpg") ??
                         SearchForPicture(directoryCache, icao24, "jpeg") ??
                         SearchForPicture(directoryCache, icao24, "png") ??
                         SearchForPicture(directoryCache, icao24, "bmp");
            }

            if(result == null && !String.IsNullOrEmpty(registration)) {
                var icaoCompliantRegistration = Describe.IcaoCompliantRegistration(registration);
                result = SearchForPicture(directoryCache, icaoCompliantRegistration, "jpg") ??
                         SearchForPicture(directoryCache, icaoCompliantRegistration, "jpeg") ??
                         SearchForPicture(directoryCache, icaoCompliantRegistration, "png") ??
                         SearchForPicture(directoryCache, icaoCompliantRegistration, "bmp");
            }

            return result;
        }

        /// <summary>
        /// Returns the full path to the file if the file exists or null if it does not.
        /// </summary>
        /// <param name="directoryCache"></param>
        /// <param name="fileName"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        private string SearchForPicture(IDirectoryCache directoryCache, string fileName, string extension)
        {
            var fullPath = Path.Combine(directoryCache.Folder ?? "", String.Format("{0}.{1}", fileName, extension));

            return directoryCache.FileExists(fullPath) ? fullPath : null;
        }
    }
}
