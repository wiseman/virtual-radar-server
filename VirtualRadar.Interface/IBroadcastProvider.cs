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
using System.Net;
using VirtualRadar.Interface.Settings;

namespace VirtualRadar.Interface
{
    /// <summary>
    /// The interface for objects that can broadcast streams of bytes to remote clients
    /// listening on a TCP connection.
    /// </summary>
    public interface IBroadcastProvider : IBackgroundThreadExceptionCatcher, IDisposable
    {
        /// <summary>
        /// Gets or sets the port to listen on.
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// Gets or sets the format to brand the events with.
        /// </summary>
        /// <remarks>
        /// This is ONLY used on the <see cref="BroadcastEventArgs"/> raised by the implementation.
        /// Under no circumstances will the implementation ever change the format of the bytes being
        /// sent via <see cref="Send"/>, they are always transmitted without modification.
        /// </remarks>
        RebroadcastFormat EventFormat { get; set; }

        /// <summary>
        /// Raised when a client connects to the provider.
        /// </summary>
        event EventHandler<BroadcastEventArgs> ClientConnected;

        /// <summary>
        /// Raised when a client disconnects from the provider.
        /// </summary>
        event EventHandler<BroadcastEventArgs> ClientDisconnected;

        /// <summary>
        /// Raised after some bytes have been sent to a client.
        /// </summary>
        event EventHandler<BroadcastEventArgs> BroadcastSent;

        /// <summary>
        /// Begins listening to the port specified.
        /// </summary>
        void BeginListening();

        /// <summary>
        /// Sends the bytes to any remote clients connected to the server.
        /// </summary>
        /// <param name="bytes"></param>
        /// <remarks>
        /// The bytes may be sent on a background thread - no copy is taken of the bytes
        /// passed across so be careful not to reuse the bytes array after this has
        /// been called.
        /// </remarks>
        void Send(byte[] bytes);
    }
}
