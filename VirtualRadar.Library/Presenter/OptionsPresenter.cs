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
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.View;
using InterfaceFactory;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Localisation;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;
using System.Diagnostics;
using VirtualRadar.Interface;
using System.IO.Ports;

namespace VirtualRadar.Library.Presenter
{
    /// <summary>
    /// The default implementation of <see cref="IOptionsPresenter"/>.
    /// </summary>
    sealed class OptionsPresenter : IOptionsPresenter
    {
        #region Private class - DefaultProvider
        /// <summary>
        /// The default implementation of the provider.
        /// </summary>
        class DefaultProvider : IOptionsPresenterProvider
        {
            private ISpeechSynthesizerWrapper _SpeechSynthesizer;

            private string _DefaultVoiceName;

            public DefaultProvider()
            {
                _SpeechSynthesizer = Factory.Singleton.Resolve<ISpeechSynthesizerWrapper>();
            }

            public void Dispose()
            {
                if(_SpeechSynthesizer != null) {
                    _SpeechSynthesizer.Dispose();
                    _SpeechSynthesizer = null;
                }
            }

            public bool FileExists(string fileName)     { return File.Exists(fileName); }

            public bool FolderExists(string folder)     { return Directory.Exists(folder); }

            public Exception TestNetworkConnection(string address, int port)
            {
                Exception result = null;
                try {
                    UriBuilder builder = new UriBuilder("telnet", address, port);
                    Uri uri = builder.Uri;
                    using(TcpClient client = new TcpClient()) {
                        client.Connect(address, port);
                    }
                } catch(Exception ex) {
                    result = ex;
                }

                return result;
            }

            public Exception TestSerialConnection(string comPort, int baudRate, int dataBits, StopBits stopBits, Parity parity, Handshake handShake)
            {
                Exception result = null;
                try {
                    using(var serialPort = new SerialPort(comPort, baudRate, parity, dataBits, stopBits)) {
                        serialPort.Handshake = handShake;
                        serialPort.ReadTimeout = 250;
                        serialPort.WriteTimeout = 250;
                        serialPort.Open();
                        byte[] buffer = new byte[128];
                        serialPort.Read(buffer, 0, buffer.Length);
                        serialPort.Write(new byte[1] { 0x10 }, 0, 1);
                    }
                } catch(TimeoutException) {
                } catch(Exception ex) {
                    result = ex;
                }

                return result;
            }

            public IEnumerable<string> GetVoiceNames()
            {
                List<string> result = new List<string>();
                result.Add(null);

                _DefaultVoiceName = _SpeechSynthesizer.DefaultVoiceName;

                foreach(var installedVoiceName in _SpeechSynthesizer.GetInstalledVoiceNames()) {
                    result.Add(installedVoiceName);
                }

                return result;
            }

            public void TestTextToSpeech(string name, int rate)
            {
                _SpeechSynthesizer.Dispose();
                _SpeechSynthesizer = Factory.Singleton.Resolve<ISpeechSynthesizerWrapper>();

                _SpeechSynthesizer.SelectVoice(name ?? _DefaultVoiceName);
                _SpeechSynthesizer.Rate = rate;
                _SpeechSynthesizer.SetOutputToDefaultAudioDevice();
                _SpeechSynthesizer.SpeakAsync("From L. F. P. G., Paris to E. D. D. T., Berlin");
            }
        }
        #endregion

        #region Private class - CachedFileSystemResult
        /// <summary>
        /// The cached result of a check against a file system entity.
        /// </summary>
        class CachedFileSystemResult
        {
            public ValidationField ValidationField;
            public string FileSystemEntityName;
            public ValidationResult ValidationResult;
        }
        #endregion

        #region Fields
        /// <summary>
        /// The GUI object that we're controlling.
        /// </summary>
        private IOptionsView _View;

        /// <summary>
        /// The cache of file system results.
        /// </summary>
        private List<CachedFileSystemResult> _CachedFileSystemResults = new List<CachedFileSystemResult>();
        #endregion

        #region Properties
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IOptionsPresenterProvider Provider { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public OptionsPresenter()
        {
            Provider = new DefaultProvider();
        }

        /// <summary>
        /// Finalises the object.
        /// </summary>
        ~OptionsPresenter()
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
            if(disposing) Provider.Dispose();
        }
        #endregion

        #region Initialise
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="view"></param>
        public void Initialise(IOptionsView view)
        {
            _View = view;
            _View.ResetToDefaultsClicked += View_ResetToDefaultsClicked;
            _View.SaveClicked += View_SaveClicked;
            _View.TestConnectionClicked += View_TestConnectionClicked;
            _View.TestTextToSpeechSettingsClicked += View_TestTextToSpeechSettingsClicked;
            _View.UseIcaoRawDecodingSettingsClicked += View_UseIcaoRawDecodingSettingsClicked;
            _View.UseRecommendedRawDecodingSettingsClicked += View_UseRecommendedRawDecodingSettingsClicked;
            _View.ValuesChanged += View_ValuesChanged;

            _View.PopulateTextToSpeechVoices(Provider.GetVoiceNames());

            var configuration = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton.Load();
            CopyConfigurationToUI(configuration);
        }
        #endregion

        #region CopyConfigurationToUI, CopyUIToConfiguration
        /// <summary>
        /// Loads the configuration and copies it to the user interface.
        /// </summary>
        /// <param name="configuration"></param>
        private void CopyConfigurationToUI(Configuration configuration)
        {
            _View.AudioEnabled = configuration.AudioSettings.Enabled;
            _View.TextToSpeechSpeed = configuration.AudioSettings.VoiceRate;
            _View.TextToSpeechVoice = String.IsNullOrEmpty(configuration.AudioSettings.VoiceName) ? null : configuration.AudioSettings.VoiceName;
            _View.RebroadcastSettings.AddRange(configuration.RebroadcastSettings);

            _View.BaseStationAddress = configuration.BaseStationSettings.Address;
            _View.BaseStationDatabaseFileName = configuration.BaseStationSettings.DatabaseFileName;
            _View.BaseStationDataSource = configuration.BaseStationSettings.DataSource;
            _View.DisplayTimeoutSeconds = configuration.BaseStationSettings.DisplayTimeoutSeconds;
            _View.TrackingTimeoutSeconds = configuration.BaseStationSettings.TrackingTimeoutSeconds;
            _View.OperatorFlagsFolder = configuration.BaseStationSettings.OperatorFlagsFolder;
            _View.PicturesFolder = configuration.BaseStationSettings.PicturesFolder;
            _View.BaseStationPort = configuration.BaseStationSettings.Port;
            _View.SilhouettesFolder = configuration.BaseStationSettings.SilhouettesFolder;
            _View.BaseStationConnectionType = configuration.BaseStationSettings.ConnectionType;
            _View.AutoReconnectAtStartup = configuration.BaseStationSettings.AutoReconnectAtStartup;

            _View.SerialComPort = configuration.BaseStationSettings.ComPort;
            _View.SerialBaudRate = configuration.BaseStationSettings.BaudRate;
            _View.SerialDataBits = configuration.BaseStationSettings.DataBits;
            _View.SerialStopBits = configuration.BaseStationSettings.StopBits;
            _View.SerialParity = configuration.BaseStationSettings.Parity;
            _View.SerialHandshake = configuration.BaseStationSettings.Handshake;
            _View.SerialStartupText = configuration.BaseStationSettings.StartupText;
            _View.SerialShutdownText = configuration.BaseStationSettings.ShutdownText;

            _View.RawDecodingReceiverLocations.AddRange(configuration.ReceiverLocations);

            _View.DownloadFlightRoutes = configuration.FlightRouteSettings.AutoUpdateEnabled;

            _View.InitialGoogleMapLatitude = configuration.GoogleMapSettings.InitialMapLatitude;
            _View.InitialGoogleMapLongitude = configuration.GoogleMapSettings.InitialMapLongitude;
            _View.InitialGoogleMapType = configuration.GoogleMapSettings.InitialMapType;
            _View.InitialGoogleMapZoom = configuration.GoogleMapSettings.InitialMapZoom;
            _View.InitialGoogleMapRefreshSeconds = configuration.GoogleMapSettings.InitialRefreshSeconds;
            _View.MinimumGoogleMapRefreshSeconds = configuration.GoogleMapSettings.MinimumRefreshSeconds;
            _View.ShortTrailLengthSeconds = configuration.GoogleMapSettings.ShortTrailLengthSeconds;
            _View.InitialDistanceUnit = configuration.GoogleMapSettings.InitialDistanceUnit;
            _View.InitialHeightUnit = configuration.GoogleMapSettings.InitialHeightUnit;
            _View.InitialSpeedUnit = configuration.GoogleMapSettings.InitialSpeedUnit;
            _View.PreferIataAirportCodes = configuration.GoogleMapSettings.PreferIataAirportCodes;

            _View.InternetClientCanPlayAudio = configuration.InternetClientSettings.CanPlayAudio;
            _View.InternetClientCanRunReports = configuration.InternetClientSettings.CanRunReports;
            _View.InternetClientCanSeeLabels = configuration.InternetClientSettings.CanShowPinText;
            _View.InternetClientCanSeePictures = configuration.InternetClientSettings.CanShowPictures;
            _View.InternetClientTimeoutMinutes = configuration.InternetClientSettings.TimeoutMinutes;
            _View.AllowInternetProximityGadgets = configuration.InternetClientSettings.AllowInternetProximityGadgets;
            _View.InternetClientCanSubmitRoutes = configuration.InternetClientSettings.CanSubmitRoutes;

            _View.CheckForNewVersions = configuration.VersionCheckSettings.CheckAutomatically;
            _View.CheckForNewVersionsPeriodDays = configuration.VersionCheckSettings.CheckPeriodDays;

            _View.WebServerUserMustAuthenticate = configuration.WebServerSettings.AuthenticationScheme == AuthenticationSchemes.Basic;
            _View.WebServerUserName = configuration.WebServerSettings.BasicAuthenticationUser;
            _View.WebServerPasswordHasChanged = configuration.WebServerSettings.BasicAuthenticationPasswordHash == null;
            _View.EnableUPnpFeatures = configuration.WebServerSettings.EnableUPnp;
            _View.IsOnlyVirtualRadarServerOnLan = configuration.WebServerSettings.IsOnlyInternetServerOnLan;
            _View.AutoStartUPnp = configuration.WebServerSettings.AutoStartUPnP;
            _View.UPnpPort = configuration.WebServerSettings.UPnpPort;

            _View.RawDecodingAcceptableAirborneSpeed = configuration.RawDecodingSettings.AcceptableAirborneSpeed;
            _View.RawDecodingAcceptableAirSurfaceTransitionSpeed = configuration.RawDecodingSettings.AcceptableAirSurfaceTransitionSpeed;
            _View.RawDecodingAcceptableSurfaceSpeed = configuration.RawDecodingSettings.AcceptableSurfaceSpeed;
            _View.RawDecodingAirborneGlobalPositionLimit = configuration.RawDecodingSettings.AirborneGlobalPositionLimit;
            _View.RawDecodingFastSurfaceGlobalPositionLimit = configuration.RawDecodingSettings.FastSurfaceGlobalPositionLimit;
            _View.RawDecodingIgnoreCallsignsInBds20 = configuration.RawDecodingSettings.IgnoreCallsignsInBds20;
            _View.RawDecodingIgnoreMilitaryExtendedSquitter = configuration.RawDecodingSettings.IgnoreMilitaryExtendedSquitter;
            _View.RawDecodingReceiverLocationId = configuration.RawDecodingSettings.ReceiverLocationId;
            _View.RawDecodingReceiverRange = configuration.RawDecodingSettings.ReceiverRange;
            _View.RawDecodingSlowSurfaceGlobalPositionLimit = configuration.RawDecodingSettings.SlowSurfaceGlobalPositionLimit;
            _View.RawDecodingSuppressReceiverRangeCheck = configuration.RawDecodingSettings.SuppressReceiverRangeCheck;
            _View.RawDecodingUseLocalDecodeForInitialPosition = configuration.RawDecodingSettings.UseLocalDecodeForInitialPosition;
            _View.AcceptIcaoInNonPICount = configuration.RawDecodingSettings.AcceptIcaoInNonPICount;
            _View.AcceptIcaoInNonPISeconds = configuration.RawDecodingSettings.AcceptIcaoInNonPISeconds;
            _View.AcceptIcaoInPI0Count = configuration.RawDecodingSettings.AcceptIcaoInPI0Count;
            _View.AcceptIcaoInPI0Seconds = configuration.RawDecodingSettings.AcceptIcaoInPI0Seconds;
        }

        /// <summary>
        /// Copies the configuration settings from the UI to the user interface.
        /// </summary>
        /// <param name="configuration"></param>
        private void CopyUIToConfiguration(Configuration configuration)
        {
            configuration.AudioSettings.Enabled = _View.AudioEnabled;
            configuration.AudioSettings.VoiceName = _View.TextToSpeechVoice;
            configuration.AudioSettings.VoiceRate = _View.TextToSpeechSpeed;
            configuration.RebroadcastSettings.Clear();
            configuration.RebroadcastSettings.AddRange(_View.RebroadcastSettings);

            configuration.BaseStationSettings.Address = _View.BaseStationAddress;
            configuration.BaseStationSettings.DatabaseFileName = _View.BaseStationDatabaseFileName;
            configuration.BaseStationSettings.DisplayTimeoutSeconds = _View.DisplayTimeoutSeconds;
            configuration.BaseStationSettings.TrackingTimeoutSeconds = _View.TrackingTimeoutSeconds;
            configuration.BaseStationSettings.PicturesFolder = _View.PicturesFolder;
            configuration.BaseStationSettings.Port = _View.BaseStationPort;
            configuration.BaseStationSettings.OperatorFlagsFolder = _View.OperatorFlagsFolder;
            configuration.BaseStationSettings.SilhouettesFolder = _View.SilhouettesFolder;
            configuration.BaseStationSettings.DataSource = _View.BaseStationDataSource;
            configuration.BaseStationSettings.ConnectionType = _View.BaseStationConnectionType;
            configuration.BaseStationSettings.AutoReconnectAtStartup = _View.AutoReconnectAtStartup;

            configuration.BaseStationSettings.ComPort = _View.SerialComPort;
            configuration.BaseStationSettings.BaudRate = _View.SerialBaudRate;
            configuration.BaseStationSettings.DataBits = _View.SerialDataBits;
            configuration.BaseStationSettings.StopBits = _View.SerialStopBits;
            configuration.BaseStationSettings.Parity = _View.SerialParity;
            configuration.BaseStationSettings.Handshake = _View.SerialHandshake;
            configuration.BaseStationSettings.StartupText = _View.SerialStartupText;
            configuration.BaseStationSettings.ShutdownText = _View.SerialShutdownText;

            configuration.ReceiverLocations.Clear();
            configuration.ReceiverLocations.AddRange(_View.RawDecodingReceiverLocations);

            configuration.FlightRouteSettings.AutoUpdateEnabled = _View.DownloadFlightRoutes;

            configuration.GoogleMapSettings.InitialMapLatitude = _View.InitialGoogleMapLatitude;
            configuration.GoogleMapSettings.InitialMapLongitude = _View.InitialGoogleMapLongitude;
            configuration.GoogleMapSettings.InitialMapType = _View.InitialGoogleMapType;
            configuration.GoogleMapSettings.InitialMapZoom = _View.InitialGoogleMapZoom;
            configuration.GoogleMapSettings.InitialRefreshSeconds = _View.InitialGoogleMapRefreshSeconds;
            configuration.GoogleMapSettings.MinimumRefreshSeconds = _View.MinimumGoogleMapRefreshSeconds;
            configuration.GoogleMapSettings.ShortTrailLengthSeconds = _View.ShortTrailLengthSeconds;
            configuration.GoogleMapSettings.InitialDistanceUnit = _View.InitialDistanceUnit;
            configuration.GoogleMapSettings.InitialHeightUnit = _View.InitialHeightUnit;
            configuration.GoogleMapSettings.InitialSpeedUnit = _View.InitialSpeedUnit;
            configuration.GoogleMapSettings.PreferIataAirportCodes = _View.PreferIataAirportCodes;

            configuration.InternetClientSettings.AllowInternetProximityGadgets = _View.AllowInternetProximityGadgets;
            configuration.InternetClientSettings.CanPlayAudio = _View.InternetClientCanPlayAudio;
            configuration.InternetClientSettings.CanRunReports = _View.InternetClientCanRunReports;
            configuration.InternetClientSettings.CanShowPictures = _View.InternetClientCanSeePictures;
            configuration.InternetClientSettings.CanShowPinText = _View.InternetClientCanSeeLabels;
            configuration.InternetClientSettings.TimeoutMinutes = _View.InternetClientTimeoutMinutes;
            configuration.InternetClientSettings.CanSubmitRoutes = _View.InternetClientCanSubmitRoutes;

            configuration.VersionCheckSettings.CheckAutomatically = _View.CheckForNewVersions;
            configuration.VersionCheckSettings.CheckPeriodDays = _View.CheckForNewVersionsPeriodDays;

            configuration.WebServerSettings.AuthenticationScheme = _View.WebServerUserMustAuthenticate ? AuthenticationSchemes.Basic : AuthenticationSchemes.Anonymous;
            configuration.WebServerSettings.BasicAuthenticationUser = _View.WebServerUserName;
            configuration.WebServerSettings.EnableUPnp = _View.EnableUPnpFeatures;
            configuration.WebServerSettings.IsOnlyInternetServerOnLan = _View.IsOnlyVirtualRadarServerOnLan;
            configuration.WebServerSettings.AutoStartUPnP = _View.AutoStartUPnp;
            configuration.WebServerSettings.UPnpPort = _View.UPnpPort;

            configuration.RawDecodingSettings.AcceptableAirborneSpeed = _View.RawDecodingAcceptableAirborneSpeed;
            configuration.RawDecodingSettings.AcceptableAirSurfaceTransitionSpeed = _View.RawDecodingAcceptableAirSurfaceTransitionSpeed;
            configuration.RawDecodingSettings.AcceptableSurfaceSpeed = _View.RawDecodingAcceptableSurfaceSpeed;
            configuration.RawDecodingSettings.AirborneGlobalPositionLimit = _View.RawDecodingAirborneGlobalPositionLimit;
            configuration.RawDecodingSettings.FastSurfaceGlobalPositionLimit = _View.RawDecodingFastSurfaceGlobalPositionLimit;
            configuration.RawDecodingSettings.IgnoreCallsignsInBds20 = _View.RawDecodingIgnoreCallsignsInBds20;
            configuration.RawDecodingSettings.IgnoreMilitaryExtendedSquitter = _View.RawDecodingIgnoreMilitaryExtendedSquitter;
            configuration.RawDecodingSettings.ReceiverLocationId = _View.RawDecodingReceiverLocationId;
            configuration.RawDecodingSettings.ReceiverRange = _View.RawDecodingReceiverRange;
            configuration.RawDecodingSettings.SlowSurfaceGlobalPositionLimit = _View.RawDecodingSlowSurfaceGlobalPositionLimit;
            configuration.RawDecodingSettings.SuppressReceiverRangeCheck = _View.RawDecodingSuppressReceiverRangeCheck;
            configuration.RawDecodingSettings.UseLocalDecodeForInitialPosition = _View.RawDecodingUseLocalDecodeForInitialPosition;
            configuration.RawDecodingSettings.AcceptIcaoInNonPICount = _View.AcceptIcaoInNonPICount;
            configuration.RawDecodingSettings.AcceptIcaoInNonPISeconds = _View.AcceptIcaoInNonPISeconds;
            configuration.RawDecodingSettings.AcceptIcaoInPI0Count = _View.AcceptIcaoInPI0Count;
            configuration.RawDecodingSettings.AcceptIcaoInPI0Seconds = _View.AcceptIcaoInPI0Seconds;

            if(_View.WebServerPasswordHasChanged) configuration.WebServerSettings.BasicAuthenticationPasswordHash = _View.WebServerPassword == null ? null : new Hash(_View.WebServerPassword);
        }
        #endregion

        #region UseIcaoRawDecodingSettings, UseRecommendedRawDecodingSettings
        /// <summary>
        /// Configures the view with the ICAO raw decoding settings.
        /// </summary>
        private void UseIcaoRawDecodingSettings()
        {
            _View.RawDecodingAcceptableAirborneSpeed = 11.112;
            _View.RawDecodingAcceptableAirSurfaceTransitionSpeed = 4.63;
            _View.RawDecodingAcceptableSurfaceSpeed = 1.389;
            _View.RawDecodingAirborneGlobalPositionLimit = 10;
            _View.RawDecodingFastSurfaceGlobalPositionLimit = 25;
            _View.RawDecodingSlowSurfaceGlobalPositionLimit = 50;
            _View.RawDecodingSuppressReceiverRangeCheck = false;
            _View.RawDecodingUseLocalDecodeForInitialPosition = false;
        }

        /// <summary>
        /// Configures the view with the default raw decoding settings.
        /// </summary>
        private void UseRecommendedRawDecodingSettings()
        {
            var defaults = new RawDecodingSettings();

            _View.RawDecodingAcceptableAirborneSpeed = defaults.AcceptableAirborneSpeed;
            _View.RawDecodingAcceptableAirSurfaceTransitionSpeed = defaults.AcceptableAirSurfaceTransitionSpeed;
            _View.RawDecodingAcceptableSurfaceSpeed = defaults.AcceptableSurfaceSpeed;
            _View.RawDecodingAirborneGlobalPositionLimit = defaults.AirborneGlobalPositionLimit;
            _View.RawDecodingFastSurfaceGlobalPositionLimit = defaults.FastSurfaceGlobalPositionLimit;
            _View.RawDecodingSlowSurfaceGlobalPositionLimit = defaults.SlowSurfaceGlobalPositionLimit;
            _View.RawDecodingSuppressReceiverRangeCheck = true;
            _View.RawDecodingUseLocalDecodeForInitialPosition = false;
        }
        #endregion

        #region ValidateForm
        /// <summary>
        /// Validates the content of the form, returning a list of errors and warnings.
        /// </summary>
        /// <returns></returns>
        private List<ValidationResult> ValidateForm()
        {
            List<ValidationResult> result = new List<ValidationResult>();

            switch(_View.BaseStationConnectionType) {
                case ConnectionType.TCP:
                    if(String.IsNullOrEmpty(_View.BaseStationAddress)) result.Add(new ValidationResult(ValidationField.BaseStationAddress, Strings.DataSourceNetworkAddressMissing));
                    break;
                case ConnectionType.COM:
                    if(String.IsNullOrEmpty(_View.SerialComPort)) result.Add(new ValidationResult(ValidationField.ComPort, Strings.SerialComPortMissing));
                    break;
                default:
                    throw new NotImplementedException();
            }
            ValidateWithinBounds(ValidationField.BaseStationPort, _View.BaseStationPort, 1, 65535, Strings.PortOutOfBounds, result);
            switch(_View.SerialBaudRate) {
                case 110:
                case 300:
                case 1200:
                case 2400:
                case 4800:
                case 9600:
                case 19200:
                case 38400:
                case 57600:
                case 115200:
                case 230400:
                case 460800:
                case 921600:
                case 3000000:
                    break;
                default:
                    result.Add(new ValidationResult(ValidationField.BaudRate, Strings.SerialBaudRateInvalidValue));
                    break;
            }
            ValidateWithinBounds(ValidationField.DataBits, _View.SerialDataBits, 5, 8, Strings.SerialDataBitsOutOfBounds, result);
            ValidateFileExists(ValidationField.BaseStationDatabase, _View.BaseStationDatabaseFileName, result);
            ValidateFolderExists(ValidationField.FlagsFolder, _View.OperatorFlagsFolder, result);
            ValidateFolderExists(ValidationField.PicturesFolder, _View.PicturesFolder, result);
            ValidateFolderExists(ValidationField.SilhouettesFolder, _View.SilhouettesFolder, result);

            ValidateWithinBounds(ValidationField.ReceiverRange, _View.RawDecodingReceiverRange, 0, 99999, Strings.ReceiverRangeOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AirborneGlobalPositionLimit, _View.RawDecodingAirborneGlobalPositionLimit, 1, 30, Strings.AirborneGlobalPositionLimitOutOfBounds, result);
            ValidateWithinBounds(ValidationField.FastSurfaceGlobalPositionLimit, _View.RawDecodingFastSurfaceGlobalPositionLimit, 1, 75, Strings.FastSurfaceGlobalPositionLimitOutOfBounds, result);
            ValidateWithinBounds(ValidationField.SlowSurfaceGlobalPositionLimit, _View.RawDecodingSlowSurfaceGlobalPositionLimit, 1, 150, Strings.SlowSurfaceGlobalPositionLimitOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AcceptableAirborneLocalPositionSpeed, _View.RawDecodingAcceptableAirborneSpeed, 0.005, 45.0, Strings.AcceptableAirborneSpeedOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AcceptableTransitionLocalPositionSpeed, _View.RawDecodingAcceptableAirSurfaceTransitionSpeed, 0.003, 20.0, Strings.AcceptableAirSurfaceTransitionSpeedOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AcceptableSurfaceLocalPositionSpeed, _View.RawDecodingAcceptableSurfaceSpeed, 0.001, 10.0, Strings.AcceptableSurfaceSpeedOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AcceptIcaoInNonPICount, _View.AcceptIcaoInNonPICount, 0, 100, Strings.AcceptIcaoInNonPICountOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AcceptIcaoInNonPISeconds, _View.AcceptIcaoInNonPISeconds, 1, 30, Strings.AcceptIcaoInNonPISecondsOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AcceptIcaoInPI0Count, _View.AcceptIcaoInPI0Count, 1, 10, Strings.AcceptIcaoInPI0CountOutOfBounds, result);
            ValidateWithinBounds(ValidationField.AcceptIcaoInPI0Seconds, _View.AcceptIcaoInPI0Seconds, 1, 60, Strings.AcceptIcaoInPI0SecondsOutOfBounds, result);

            ValidateWithinBounds(ValidationField.UPnpPortNumber, _View.UPnpPort, 1, 65535, Strings.UPnpPortOutOfBounds, result);
            ValidateWithinBounds(ValidationField.InternetUserIdleTimeout, _View.InternetClientTimeoutMinutes, 0, 1440, Strings.InternetUserIdleTimeoutOutOfBounds, result);

            ValidateWithinBounds(ValidationField.Latitude, _View.InitialGoogleMapLatitude, -90.0, 90.0, Strings.LatitudeOutOfBounds, result);
            ValidateWithinBounds(ValidationField.Longitude, _View.InitialGoogleMapLongitude, -180.0, 180.0, Strings.LongitudeOutOfBounds, result);
            ValidateWithinBounds(ValidationField.GoogleMapZoomLevel, _View.InitialGoogleMapZoom, 0, 19, Strings.GoogleMapZoomOutOfBounds, result);
            ValidateWithinBounds(ValidationField.MinimumGoogleMapRefreshSeconds, _View.MinimumGoogleMapRefreshSeconds, 0, 3600, Strings.MinimumRefreshOutOfBounds, result);
            if(_View.InitialGoogleMapRefreshSeconds > 3600) result.Add(new ValidationResult(ValidationField.InitialGoogleMapRefreshSeconds, Strings.InitialRefreshOutOfBounds));
            if(_View.InitialGoogleMapRefreshSeconds < _View.MinimumGoogleMapRefreshSeconds) result.Add(new ValidationResult(ValidationField.InitialGoogleMapRefreshSeconds, Strings.InitialRefreshLessThanMinimumRefresh));
            if(String.IsNullOrEmpty(_View.WebServerUserName) && _View.WebServerUserMustAuthenticate) result.Add(new ValidationResult(ValidationField.WebUserName, Strings.UserNameMissing));

            ValidateWithinBounds(ValidationField.CheckForNewVersions, _View.CheckForNewVersionsPeriodDays, 1, 365, Strings.DaysBetweenChecksOutOfBounds, result);
            ValidateWithinBounds(ValidationField.DisplayTimeout, _View.DisplayTimeoutSeconds, 5, 540, Strings.DurationBeforeAircraftRemovedFromMapOutOfBounds, result);
            ValidateWithinBounds(ValidationField.ShortTrailLength, _View.ShortTrailLengthSeconds, 1, 1800, Strings.DurationOfShortTrailsOutOfBounds, result);
            ValidateWithinBounds(ValidationField.TextToSpeechSpeed, _View.TextToSpeechSpeed, -10, 10, Strings.ReadingSpeedOutOfBounds, result);
            if(_View.TrackingTimeoutSeconds < _View.DisplayTimeoutSeconds) result.Add(new ValidationResult(ValidationField.TrackingTimeout, Strings.TrackingTimeoutLessThanDisplayTimeout));
            else if(_View.TrackingTimeoutSeconds > 3600) result.Add(new ValidationResult(ValidationField.TrackingTimeout, Strings.TrackingTimeoutOutOfBounds));

            return result;
        }

        private void ValidateWithinBounds(ValidationField field, int value, int lowerInclusive, int upperInclusive, string message, List<ValidationResult> results)
        {
            if(value < lowerInclusive || value > upperInclusive) results.Add(new ValidationResult(field, message));
        }

        private void ValidateWithinBounds(ValidationField field, double value, double lowerInclusive, double upperInclusive, string message, List<ValidationResult> results)
        {
            if(value < lowerInclusive || value > upperInclusive) results.Add(new ValidationResult(field, message));
        }

        private void ValidateFileExists(ValidationField field, string fileName, List<ValidationResult> results)
        {
            ValidateFileSystemEntityExists(field, fileName, results, true);
        }

        private void ValidateFolderExists(ValidationField field, string folder, List<ValidationResult> results)
        {
            ValidateFileSystemEntityExists(field, folder, results, false);
        }

        private void ValidateFileSystemEntityExists(ValidationField field, string entityName, List<ValidationResult> results, bool isFile)
        {
            ValidationResult result = null;

            if(!String.IsNullOrEmpty(entityName)) {
                var cachedResult = _CachedFileSystemResults.Where(r => r.ValidationField == field).FirstOrDefault();
                if(cachedResult != null && entityName == cachedResult.FileSystemEntityName) result = cachedResult.ValidationResult;
                else {
                    if(cachedResult != null) _CachedFileSystemResults.Remove(cachedResult);
                    cachedResult = new CachedFileSystemResult() { ValidationField = field, FileSystemEntityName = entityName };

                    bool entityExists = isFile ? Provider.FileExists(entityName) : Provider.FolderExists(entityName);
                    if(!entityExists) cachedResult.ValidationResult = result = new ValidationResult(field, String.Format(Strings.SomethingDoesNotExist, entityName), true);

                    _CachedFileSystemResults.Add(cachedResult);
                }

                if(result != null) results.Add(result);
            }
        }
        #endregion

        #region Events subscribed
        /// <summary>
        /// Raised when the user wants to reset the view to default values.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_ResetToDefaultsClicked(object sender, EventArgs args)
        {
            CopyConfigurationToUI(new Configuration());
        }

        /// <summary>
        /// Raised when the user elects to save their changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_SaveClicked(object sender, EventArgs args)
        {
            var validationResults = ValidateForm();
            _View.ShowValidationResults(validationResults);

            if(validationResults.Where(r => r.IsWarning == false).Count() == 0) {
                var configurationStorage = Factory.Singleton.Resolve<IConfigurationStorage>().Singleton;
                var configuration = configurationStorage.Load();

                CopyUIToConfiguration(configuration);
                configurationStorage.Save(configuration);
            }
        }

        /// <summary>
        /// Raised when the user indicates that they want to test the connection settings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_TestConnectionClicked(object sender, EventArgs args)
        {
            var previousBusyState = _View.ShowBusy(true, null);
            Exception exception = null;
            try {
                switch(_View.BaseStationConnectionType) {
                    case ConnectionType.TCP:
                        exception = Provider.TestNetworkConnection(_View.BaseStationAddress, _View.BaseStationPort);
                        break;
                    case ConnectionType.COM:
                        exception = Provider.TestSerialConnection(_View.SerialComPort, _View.SerialBaudRate, _View.SerialDataBits, _View.SerialStopBits, _View.SerialParity, _View.SerialHandshake);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            } finally {
                _View.ShowBusy(false, previousBusyState);
            }

            if(exception == null)   _View.ShowTestConnectionResults(Strings.CanConnectWithSettings, Strings.ConnectedSuccessfully);
            else                    _View.ShowTestConnectionResults(String.Format("{0} {1}", Strings.CannotConnectWithSettings, exception.Message), Strings.CannotConnect);
        }

        /// <summary>
        /// Raised when the user requests a test of the text-to-speech settings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_TestTextToSpeechSettingsClicked(object sender, EventArgs args)
        {
            Provider.TestTextToSpeech(_View.TextToSpeechVoice, _View.TextToSpeechSpeed);
        }

        /// <summary>
        /// Raised when the user wants to use the ICAO recommended raw decoder settings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_UseIcaoRawDecodingSettingsClicked(object sender, EventArgs args)
        {
            UseIcaoRawDecodingSettings();
        }

        /// <summary>
        /// Raised when the user wants to use the default raw decoder settings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_UseRecommendedRawDecodingSettingsClicked(object sender, EventArgs args)
        {
            UseRecommendedRawDecodingSettings();
        }

        /// <summary>
        /// Raised after control values may have been changed by the user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_ValuesChanged(object sender, EventArgs args)
        {
            _View.ShowValidationResults(ValidateForm());
        }
        #endregion
    }
}
