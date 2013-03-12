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
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VirtualRadar.Interface.Settings;
using System.Net;
using VirtualRadar.Localisation;

namespace VirtualRadar.WinForms.Controls
{
    /// <summary>
    /// A user control that can display information about connections to one or more rebroadcast servers.
    /// </summary>
    public partial class RebroadcastStatusControl : UserControl
    {
        /// <summary>
        /// A private class describing a connection to a rebroadcast server.
        /// </summary>
        class Connection
        {
            public string EndPointAddress;
            public int EndPointPort;
            public int ConnectedToPort;
            public RebroadcastFormat Format;
            public long BytesSent;
            public bool Changed;
        }

        /// <summary>
        /// A private class describing an entry in the display.
        /// </summary>
        class DisplayProperties
        {
            public ListViewItem Item;
            public Connection Connection;
            public string EndPointDescription;
            public string IncomingPortDescription;
            public string FormatDescription;
            public string BytesSentDescription;
        }

        /// <summary>
        /// A list of every connection that we're aware of.
        /// </summary>
        private List<Connection> _Connections = new List<Connection>();

        /// <summary>
        /// The object that is used to control access to the properties across multiple threads.
        /// </summary>
        private object _SyncLock = new object();

        private MonoAutoScaleMode _MonoAutoScaleMode;
        /// <summary>
        /// Gets or sets the AutoScaleMode.
        /// </summary>
        /// <remarks>Works around Mono's weirdness over AutoScaleMode and anchoring / docking - see the comments against MonoAutoScaleMode.</remarks>
        public new AutoScaleMode AutoScaleMode
        {
            get { return _MonoAutoScaleMode.AutoScaleMode; }
            set { _MonoAutoScaleMode.AutoScaleMode = value; }
        }

        /// <summary>
        /// Gets or sets a string describing the configuration of the rebroadcast servers.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Configuration
        {
            get { return labelDescribeConfiguration.Text; }
            set { labelDescribeConfiguration.Text = value; }
        }

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public RebroadcastStatusControl()
        {
            _MonoAutoScaleMode = new MonoAutoScaleMode(this);
            InitializeComponent();
        }

        /// <summary>
        /// Updates or adds a connection to the list.
        /// </summary>
        /// <param name="endPointAddress"></param>
        /// <param name="endPointPort"></param>
        /// <param name="connectedToPort"></param>
        /// <param name="portFormat"></param>
        /// <param name="bytesSent"></param>
        public void UpdateEntry(string endPointAddress, int endPointPort, int connectedToPort, RebroadcastFormat portFormat, int bytesSent)
        {
            lock(_SyncLock) {
                var connection = _Connections.Where(r => r.EndPointPort == endPointPort && r.EndPointAddress == endPointAddress).FirstOrDefault();
                if(connection == null) {
                    connection = new Connection() { EndPointAddress = endPointAddress, EndPointPort = endPointPort };
                    _Connections.Add(connection);
                }

                connection.BytesSent += bytesSent;
                connection.Format = portFormat;
                connection.ConnectedToPort = connectedToPort;
                connection.Changed = true;
            }
        }

        /// <summary>
        /// Removes a connection from the list.
        /// </summary>
        /// <param name="endPointAddress"></param>
        /// <param name="endPointPort"></param>
        public void RemoveEntry(string endPointAddress, int endPointPort)
        {
            lock(_SyncLock) {
                var connection = _Connections.Where(r => r.EndPointPort == endPointPort && r.EndPointAddress == endPointAddress).FirstOrDefault();
                if(connection != null) _Connections.Remove(connection);
            }
        }

        /// <summary>
        /// Called once a second on the GUI thread, updates the list view with the latest statistics.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            IEnumerable<DisplayProperties> displayProperties = null;
            lock(_SyncLock) {
                displayProperties = _Connections.Where(r => r.Changed).Select(r => new DisplayProperties() {
                    Item = listView.Items.OfType<ListViewItem>().Where(i => i.Tag == r).FirstOrDefault(),
                    Connection = r,
                    EndPointDescription = String.Format("{0}:{1}", r.EndPointAddress, r.EndPointPort),
                    IncomingPortDescription = r.ConnectedToPort.ToString(),
                    FormatDescription = ConvertFormatToString(r.Format),
                    BytesSentDescription = r.BytesSent.ToString("N0"),
                }).ToList();
                foreach(var displayProperty in displayProperties) {
                    displayProperty.Connection.Changed = false;
                }
            }

            foreach(var displayProperty in displayProperties) {
                if(displayProperty.Item == null) {
                    displayProperty.Item = new ListViewItem(new string[] { "", "", "", "" }) { Tag = displayProperty.Connection };
                    listView.Items.Add(displayProperty.Item);
                }
                displayProperty.Item.SubItems[0].Text = displayProperty.EndPointDescription;
                displayProperty.Item.SubItems[1].Text = displayProperty.IncomingPortDescription;
                displayProperty.Item.SubItems[2].Text = displayProperty.FormatDescription;
                displayProperty.Item.SubItems[3].Text = displayProperty.BytesSentDescription;
            }

            ListViewItem[] deletedItems = null;
            lock(_SyncLock) {
                // This could potentially miss a deleted connection if another connection is added after the list view has been updated for
                // changes above, but on the next timer tick it would eventually get deleted.
                if(_Connections.Count != listView.Items.Count) {
                    deletedItems = listView.Items.OfType<ListViewItem>().Where(r => !_Connections.Contains((Connection)r.Tag)).ToArray();
                }
            }
            if(deletedItems != null && deletedItems.Length > 0) {
                foreach(var deletedItem in deletedItems) {
                    listView.Items.Remove(deletedItem);
                }
            }
        }

        /// <summary>
        /// Returns the localised string for a <see cref="RebroadcastFormat"/>.
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        private string ConvertFormatToString(RebroadcastFormat format)
        {
            switch(format) {
                case RebroadcastFormat.Passthrough:     return Strings.RebroadcastFormatPassthrough;
                case RebroadcastFormat.Port30003:       return Strings.RebroadcastFormatPort30003;
                case RebroadcastFormat.Avr:             return Strings.RebroadcastFormatAvr;
                default:                                throw new NotImplementedException();
            }
        }
    }
}
