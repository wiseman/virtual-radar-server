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
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface;
using InterfaceFactory;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.ModeS;
using VirtualRadar.Interface.Adsb;

namespace VirtualRadar.Library.Listener
{
    /// <summary>
    /// The default implementation of <see cref="IAutoConfigListener"/>.
    /// </summary>
    sealed class AutoConfigListener : IAutoConfigListener
    {
        #region Fields
        /// <summary>
        /// True once events have been hooked.
        /// </summary>
        private bool _EventsHooked;
        #endregion

        #region Properties
        private static readonly IAutoConfigListener _Singleton = new AutoConfigListener();
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IAutoConfigListener Singleton { get { return _Singleton; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IListener Listener { get; private set; }
        #endregion

        #region Events exposed
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler<EventArgs<Exception>> ExceptionCaught;

        /// <summary>
        /// Raises <see cref="ExceptionCaught"/>
        /// </summary>
        /// <param name="args"></param>
        private void OnExceptionCaught(EventArgs<Exception> args)
        {
            if(ExceptionCaught != null) ExceptionCaught(this, args);
        }
        #endregion

        #region Constructor and Finaliser
        /// <summary>
        /// Finalises the object.
        /// </summary>
        ~AutoConfigListener()
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
        /// Disposes of or finalises the object.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if(disposing) {
                if(_EventsHooked) {
                    Factory.Singleton.Resolve<IConfigurationStorage>().Singleton.ConfigurationChanged -= ConfigurationStorage_ConfigurationChanged;
                    _EventsHooked = false;
                }
                if(Listener != null) {
                    Listener.ExceptionCaught -= Listener_ExceptionCaught;
                    Listener.Dispose();
                }
            }
        }
        #endregion

        #region Initialise, ApplyConfiguration
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void Initialise()
        {
            if(Listener != null) throw new InvalidOperationException("Cannot call Initialise more than once");
            Listener = Factory.Singleton.Resolve<IListener>();
            Listener.ExceptionCaught += Listener_ExceptionCaught;

            var configurationStorage = ApplyConfiguration(false);
            configurationStorage.ConfigurationChanged += ConfigurationStorage_ConfigurationChanged;
            _EventsHooked = true;
        }

        /// <summary>
        /// Applies the current configuration to the listener, reconnecting if required.
        /// </summary>
        /// <param name="reconnect"></param>
        private IConfigurationStorage ApplyConfiguration(bool reconnect)
        {
            var configurationStorage = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton;
            var config = configurationStorage.Load();

            Listener.IgnoreBadMessages = config.BaseStationSettings.IgnoreBadMessages;
            var listenerProvider = DetermineListenerProvider(config);
            var bytesExtractor = DetermineBytesExtractor(config);
            bool feedSourceHasChanged = listenerProvider != Listener.Provider || bytesExtractor != Listener.BytesExtractor;
            var rawMessageTranslator = DetermineRawMessageTranslator(config, feedSourceHasChanged);

            if(feedSourceHasChanged) {
                Listener.ChangeSource(listenerProvider, bytesExtractor, rawMessageTranslator, reconnect);
                Factory.Singleton.Resolve<IStatistics>().Singleton.ResetMessageCounters();
            }

            return configurationStorage;
        }

        /// <summary>
        /// Creates and configures the provider to use to connect to the data feed.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        private IListenerProvider DetermineListenerProvider(Configuration config)
        {
            IListenerProvider result = Listener.Provider;

            var settings = config.BaseStationSettings;
            switch(config.BaseStationSettings.ConnectionType) {
                case ConnectionType.COM:
                    var existingSerialProvider = result as ISerialListenerProvider;
                    if(existingSerialProvider == null || existingSerialProvider.BaudRate != settings.BaudRate || existingSerialProvider.ComPort != settings.ComPort ||
                       existingSerialProvider.DataBits != settings.DataBits || existingSerialProvider.Handshake != settings.Handshake || 
                       existingSerialProvider.Parity != settings.Parity || existingSerialProvider.ShutdownText != settings.ShutdownText ||
                       existingSerialProvider.StartupText != settings.StartupText || existingSerialProvider.StopBits != settings.StopBits) {
                        var serialProvider = Factory.Singleton.Resolve<ISerialListenerProvider>();
                        serialProvider.BaudRate = settings.BaudRate;
                        serialProvider.ComPort = settings.ComPort;
                        serialProvider.DataBits = settings.DataBits;
                        serialProvider.Handshake = settings.Handshake;
                        serialProvider.Parity = settings.Parity;
                        serialProvider.ShutdownText = settings.ShutdownText;
                        serialProvider.StartupText = settings.StartupText;
                        serialProvider.StopBits = settings.StopBits;
                        result = serialProvider;
                    }
                    break;
                case ConnectionType.TCP:
                    var existingTcpProvider = result as ITcpListenerProvider;
                    if(existingTcpProvider == null || existingTcpProvider.Address != settings.Address || existingTcpProvider.Port != settings.Port) {
                        var tcpProvider = Factory.Singleton.Resolve<ITcpListenerProvider>();
                        tcpProvider.Address = settings.Address;
                        tcpProvider.Port = settings.Port;
                        result = tcpProvider;
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }

            return result;
        }

        /// <summary>
        /// Creates and configures the message bytes extractor to use to get the message bytes out of the feed.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        private IMessageBytesExtractor DetermineBytesExtractor(Configuration config)
        {
            IMessageBytesExtractor result = Listener.BytesExtractor;

            switch(config.BaseStationSettings.DataSource) {
                case DataSource.Beast:      if(result == null || !(result is IBeastMessageBytesExtractor)) result = Factory.Singleton.Resolve<IBeastMessageBytesExtractor>(); break;
                case DataSource.Port30003:  if(result == null || !(result is IPort30003MessageBytesExtractor)) result = Factory.Singleton.Resolve<IPort30003MessageBytesExtractor>(); break;
                case DataSource.Sbs3:       if(result == null || !(result is ISbs3MessageBytesExtractor)) result = Factory.Singleton.Resolve<ISbs3MessageBytesExtractor>(); break;
                default:                    throw new NotImplementedException();
            }

            return result;
        }

        /// <summary>
        /// Creates and configures the object to translate Mode-S messages into BaseStation messages.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="isNewSource"></param>
        /// <returns></returns>
        private IRawMessageTranslator DetermineRawMessageTranslator(Configuration config, bool isNewSource)
        {
            var result = isNewSource || Listener.RawMessageTranslator == null ? Factory.Singleton.Resolve<IRawMessageTranslator>() : Listener.RawMessageTranslator;

            // There's every chance that the translator is in use while we're changing the properties here. In practise I
            // don't think it's going to make a huge difference, and people won't be changing this stuff very often anyway,
            // but I might have to add some shared locking code here. I'd like to avoid it if I can though.

            var receiverLocation = config.ReceiverLocation(config.RawDecodingSettings.ReceiverLocationId);
            result.ReceiverLocation = receiverLocation == null ? null : new GlobalCoordinate(receiverLocation.Latitude, receiverLocation.Longitude);

            result.AcceptIcaoInNonPICount                       = config.RawDecodingSettings.AcceptIcaoInNonPICount;
            result.AcceptIcaoInNonPIMilliseconds                = config.RawDecodingSettings.AcceptIcaoInNonPISeconds * 1000;
            result.AcceptIcaoInPI0Count                         = config.RawDecodingSettings.AcceptIcaoInPI0Count;
            result.AcceptIcaoInPI0Milliseconds                  = config.RawDecodingSettings.AcceptIcaoInPI0Seconds * 1000;
            result.GlobalDecodeAirborneThresholdMilliseconds    = config.RawDecodingSettings.AirborneGlobalPositionLimit * 1000;
            result.GlobalDecodeFastSurfaceThresholdMilliseconds = config.RawDecodingSettings.FastSurfaceGlobalPositionLimit * 1000;
            result.GlobalDecodeSlowSurfaceThresholdMilliseconds = config.RawDecodingSettings.SlowSurfaceGlobalPositionLimit * 1000;
            result.IgnoreMilitaryExtendedSquitter               = config.RawDecodingSettings.IgnoreMilitaryExtendedSquitter;
            result.LocalDecodeMaxSpeedAirborne                  = config.RawDecodingSettings.AcceptableAirborneSpeed;
            result.LocalDecodeMaxSpeedSurface                   = config.RawDecodingSettings.AcceptableSurfaceSpeed;
            result.LocalDecodeMaxSpeedTransition                = config.RawDecodingSettings.AcceptableAirSurfaceTransitionSpeed;
            result.ReceiverRangeKilometres                      = config.RawDecodingSettings.ReceiverRange;
            result.SuppressCallsignsFromBds20                   = config.RawDecodingSettings.IgnoreCallsignsInBds20;
            result.SuppressReceiverRangeCheck                   = config.RawDecodingSettings.SuppressReceiverRangeCheck;
            result.TrackingTimeoutSeconds                       = config.BaseStationSettings.TrackingTimeoutSeconds;
            result.UseLocalDecodeForInitialPosition             = config.RawDecodingSettings.UseLocalDecodeForInitialPosition;

            return result;
        }
        #endregion

        #region Events subscribed
        /// <summary>
        /// Called when the configuration has been changed by the user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ConfigurationStorage_ConfigurationChanged(object sender, EventArgs args)
        {
            ApplyConfiguration(true);
        }

        /// <summary>
        /// Called when the listener picks up an exception on a background thread and wants it reported on the GUI thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Listener_ExceptionCaught(object sender, EventArgs<Exception> args)
        {
            OnExceptionCaught(args);
        }
        #endregion
    }
}
