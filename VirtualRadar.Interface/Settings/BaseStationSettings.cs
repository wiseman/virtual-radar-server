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
using System.Text;
using System.Runtime.Serialization;
using System.IO;
using InterfaceFactory;
using System.IO.Ports;

namespace VirtualRadar.Interface.Settings
{
    /// <summary>
    /// An object that describes the source of data we should connect to.
    /// </summary>
    /// <remarks>
    /// Originally the application was only able to connect to instances of BaseStation. Later versions
    /// were able to deal with a wider variety of sources. Ideally the name of this class would be changed
    /// to something that doesn't imply that it's only talking about BaseStation connections but the name
    /// has been serialised into the configuration files and it's not worth the pain of changing it.
    /// </remarks>
    public class BaseStationSettings
    {
        /// <summary>
        /// Gets or sets the data source to connect to.
        /// </summary>
        public DataSource DataSource { get; set; }

        /// <summary>
        /// Gets or sets the mechanism to use to connect to the data source.
        /// </summary>
        public ConnectionType ConnectionType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that the program should keep attempting to connect to the data source
        /// if it cannot connect when the program first starts.
        /// </summary>
        public bool AutoReconnectAtStartup { get; set; }

        /// <summary>
        /// Gets or sets the address of the source of data to listen to.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the port of the source of data to listen to.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the COM port to listen to.
        /// </summary>
        public string ComPort { get; set; }

        /// <summary>
        /// Gets or sets the baud rate to use.
        /// </summary>
        public int BaudRate { get; set; }

        /// <summary>
        /// Gets or sets the data bits to use.
        /// </summary>
        public int DataBits { get; set; }

        /// <summary>
        /// Gets or sets the stop bits to use.
        /// </summary>
        public StopBits StopBits { get; set; }

        /// <summary>
        /// Gets or sets the parity to use.
        /// </summary>
        public Parity Parity { get; set; }

        /// <summary>
        /// Gets or sets the handshake protocol to use.
        /// </summary>
        public Handshake Handshake { get; set; }

        /// <summary>
        /// Gets or sets the text to send across the COM port on startup - a null or empty string will disable the
        /// feature. Can contain \r and \n.
        /// </summary>
        public string StartupText { get; set; }

        /// <summary>
        /// Gets or sets the text to send across the COM port on shutdown - a null or empty string will disable the
        /// feature. Can contain \r and \n.
        /// </summary>
        public string ShutdownText { get; set; }

        /// <summary>
        /// Gets or sets the full path to the BaseStation database file to use.
        /// </summary>
        public string DatabaseFileName { get; set; }

        /// <summary>
        /// Gets or sets the folder that holds operator logo images to display to the user.
        /// </summary>
        public string OperatorFlagsFolder { get; set; }

        /// <summary>
        /// Gets or sets the folder that holds aircraft silhouette images to display to the user.
        /// </summary>
        public string SilhouettesFolder { get; set; }

        /// <summary>
        /// No longer used.
        /// </summary>
        public string OutlinesFolder { get; set; }

        /// <summary>
        /// Gets or sets the folder that holds pictures of aircraft to display to the user.
        /// </summary>
        public string PicturesFolder { get; set; }

        /// <summary>
        /// Gets or sets the number of seconds that aircraft should remain on display in the browser after their last transmission.
        /// </summary>
        public int DisplayTimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets the number of seconds that aircraft should remain in the aircraft list after their last transmission.
        /// </summary>
        public int TrackingTimeoutSeconds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that badly formatted messages on the feed should be ignored rather than triggering a disconnection
        /// from the feed.
        /// </summary>
        /// <remarks>
        /// This has been retired - the program sets this to true and the configuration loader is expected to force it to true on load. Bad
        /// messages will no longer disconnect the listener.
        /// </remarks>
        public bool IgnoreBadMessages { get; set; }

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public BaseStationSettings()
        {
            var isMono = Factory.Singleton.Resolve<IRuntimeEnvironment>().Singleton.IsMono;

            Address = "127.0.0.1";
            Port = 30003;
            IgnoreBadMessages = true;

            BaudRate = 115200;
            DataBits = 8;
            StopBits = StopBits.One;
            Parity = Parity.None;
            Handshake = Handshake.None;
            StartupText = "#43-02\\r";
            ShutdownText = "#43-00\\r";

            DatabaseFileName = isMono ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Kinetic\BaseStation\BaseStation.sqb");
            OperatorFlagsFolder = isMono ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Kinetic\BaseStation\OperatorFlags");
            OutlinesFolder = isMono ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Kinetic\BaseStation\Outlines");

            DisplayTimeoutSeconds = 30;
            TrackingTimeoutSeconds = 600;
        }
    }
}
