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
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Localisation;
using VirtualRadar.Interface;

namespace VirtualRadar.WinForms.Controls
{
    /// <summary>
    /// A user control that displays the state of our BaseStation connection.
    /// </summary>
    public partial class DataFeedStatusControl : UserControl
    {
        #region Fields
        /// <summary>
        /// The total messages last displayed to the user.
        /// </summary>
        private long _LastDisplayedTotalMessages = -1;

        /// <summary>
        /// The total bad messages last displayed to the user.
        /// </summary>
        private long _LastDisplayedTotalBadMessages = -1;

        /// <summary>
        /// The total number of aircraft last shown to the user.
        /// </summary>
        private int _LastDisplayedTotalAircraft = -1;
        #endregion

        #region Properties
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

        private ConnectionStatus _ConnectionStatus;
        /// <summary>
        /// Gets or sets the connection status to show to the user.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConnectionStatus ConnectionStatus
        {
            get { return _ConnectionStatus; }
            set
            {
                if(InvokeRequired) BeginInvoke(new MethodInvoker(() => ConnectionStatus = value));
                else if(_ConnectionStatus != value) {
                    _ConnectionStatus = value;
                    switch(_ConnectionStatus) {
                        case ConnectionStatus.CannotConnect:    labelConnectionStatus.Text = Strings.CannotConnect; break;
                        case ConnectionStatus.Connecting:       labelConnectionStatus.Text = Strings.Connecting; break;
                        case ConnectionStatus.Connected:        labelConnectionStatus.Text = Strings.Connected; break;
                        case ConnectionStatus.Disconnected:     labelConnectionStatus.Text = Strings.Disconnected; break;
                        case ConnectionStatus.Reconnecting:     labelConnectionStatus.Text = Strings.Reconnecting; break;
                        default:                                throw new NotImplementedException();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the total number of messages received from BaseStation. There is a delay before the display is updated after this
        /// has been changed.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long TotalMessages { get; set; }

        /// <summary>
        /// Gets or sets the total number of bad messages received from BaseStation. There is a delay before the display is updated after this
        /// has been changed.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long TotalBadMessages { get; set; }

        /// <summary>
        /// Gets or sets the total number of aircraft being tracked from BaseStation messages. There is a delay before the display is updated
        /// after this has been changed.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TotalAircraft { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public DataFeedStatusControl()
        {
            _MonoAutoScaleMode = new MonoAutoScaleMode(this);
            InitializeComponent();
        }
        #endregion

        #region Refresh Display
        /// <summary>
        /// Updates the timed elements of the control.
        /// </summary>
        private void RefreshDisplay()
        {
            if(InvokeRequired) BeginInvoke(new MethodInvoker(() => RefreshDisplay()));
            else {
                if(_LastDisplayedTotalMessages != TotalMessages) {
                    _LastDisplayedTotalMessages = TotalMessages;
                    labelTotalMessages.Text = String.Format("{0:N0}", TotalMessages);
                }

                if(_LastDisplayedTotalBadMessages != TotalBadMessages) {
                    _LastDisplayedTotalBadMessages = TotalBadMessages;
                    labelTotalBadMessages.Text = String.Format("{0:N0}", TotalBadMessages);
                }

                if(_LastDisplayedTotalAircraft != TotalAircraft) {
                    _LastDisplayedTotalAircraft = TotalAircraft;
                    labelCountAircraft.Text = String.Format("{0:N0}", TotalAircraft);
                }
            }
        }
        #endregion

        #region Events consumed
        /// <summary>
        /// Raised when the control has been created but is not yet visible.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if(!DesignMode) {
                Localise.Control(this);

                ConnectionStatus = ConnectionStatus.Disconnected;
                labelCountAircraft.Text = "0";
                labelTotalMessages.Text = "0";
            }
        }

        /// <summary>
        /// Raised every time the timer ticks.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer_Tick(object sender, EventArgs e)
        {
            RefreshDisplay();
        }
        #endregion
    }
}
