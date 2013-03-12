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
using VirtualRadar.Interface.Settings;

namespace VirtualRadar.WinForms.Options
{
    #pragma warning disable 0659 // We only override Equals, we have no intention of using GetHashCode.
    /// <summary>
    /// A class that brings together all of the options required to determine the current receiver location.
    /// </summary>
    class ReceiverLocationOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the ID of the current location in <see cref="ReceiverLocations"/>.
        /// </summary>
        public int CurrentReceiverId { get; set; }

        /// <summary>
        /// Gets a list of all known locations.
        /// </summary>
        public List<ReceiverLocation> ReceiverLocations { get; private set; }

        /// <summary>
        /// Gets or sets the current location in <see cref="ReceiverLocations"/> as specified by <see cref="CurrentReceiverId"/>.
        /// </summary>
        public ReceiverLocation CurrentReceiverLocation
        {
            get { return ReceiverLocations.Where(r => r.UniqueId == CurrentReceiverId).FirstOrDefault(); }
            set { CurrentReceiverId = value == null || !ReceiverLocations.Any(r => r.UniqueId == value.UniqueId) ? -1 : value.UniqueId; }
        }

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public ReceiverLocationOptions()
        {
            ReceiverLocations = new List<ReceiverLocation>();
        }

        /// <summary>
        /// Returns an English description of the location.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return CurrentReceiverLocation == null ? "" : CurrentReceiverLocation.Name;
        }

        /// <summary>
        /// Returns true if the object passed in is equal to this one.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            bool result = Object.ReferenceEquals(this, obj);
            if(!result) {
                ReceiverLocationOptions other = obj as ReceiverLocationOptions;
                if(other != null) result = other.ReceiverLocations.SequenceEqual(ReceiverLocations) && other.CurrentReceiverId == CurrentReceiverId;
            }

            return result;
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            var result = new ReceiverLocationOptions();
            foreach(var receiverLocation in ReceiverLocations) {
                result.ReceiverLocations.Add((ReceiverLocation)receiverLocation.Clone());
            }
            result.CurrentReceiverId = CurrentReceiverId;

            return result;
        }
    }
    #pragma warning restore 0659
}
