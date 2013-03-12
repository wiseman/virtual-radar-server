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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.View;
using VirtualRadar.Localisation;
using VirtualRadar.WinForms.Options;
using System.IO.Ports;

namespace VirtualRadar.WinForms
{
    /// <summary>
    /// The default implementation of <see cref="IOptionsView"/>.
    /// </summary>
    public partial class OptionsPropertySheetView : Form, IOptionsView
    {
        #region Fields
        /// <summary>
        /// The object that's controlling this view.
        /// </summary>
        private IOptionsPresenter _Presenter;

        /// <summary>
        /// The object that's handling online help for us.
        /// </summary>
        private OnlineHelpHelper _OnlineHelp;

        // Each of the sheets of configuration options.
        private SheetDataSourceOptions  _DataSourcesSheet =  new SheetDataSourceOptions();
        private SheetRawFeedOptions     _RawFeedSheet = new SheetRawFeedOptions();
        private SheetWebServerOptions   _WebServerSheet = new SheetWebServerOptions();
        private SheetWebSiteOptions     _WebSiteSheet = new SheetWebSiteOptions();
        private SheetGeneralOptions     _GeneralOptions = new SheetGeneralOptions();

        /// <summary>
        /// The list of sheets that will be displayed in the screen, one at a time.
        /// </summary>
        private List<ISheet> _Sheets = new List<ISheet>();

        /// <summary>
        /// The fake password used to determine whether the user has entered a password or not.
        /// </summary>
        /// <remarks>
        /// The original password entered by the user is not known, we only store a hash. This gets written into
        /// the password field and then if the password isn't this value then we know they've entered something.
        /// </remarks>
        private readonly string _SurrogatePassword = new String((char)1, 10);
        #endregion

        #region Properties
        #region AutoScaleMode
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
        #endregion

        #region Internal properties
        /// <summary>
        /// Gets or sets the selected sheet.
        /// </summary>
        private ISheet SelectedSheet
        {
            get { return listBox.SelectedItem as ISheet; }
            set { listBox.SelectedItem = value; }
        }
        #endregion

        #region General options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool CheckForNewVersions
        {
            get { return _GeneralOptions.CheckForNewVersions; }
            set { _GeneralOptions.CheckForNewVersions = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int CheckForNewVersionsPeriodDays
        {
            get { return _GeneralOptions.CheckForNewVersionsPeriodDays; }
            set { _GeneralOptions.CheckForNewVersionsPeriodDays = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool DownloadFlightRoutes
        {
            get { return _GeneralOptions.DownloadFlightRoutes; }
            set { _GeneralOptions.DownloadFlightRoutes = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int DisplayTimeoutSeconds
        {
            get { return _GeneralOptions.DisplayTimeoutSeconds; }
            set { _GeneralOptions.DisplayTimeoutSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int TrackingTimeoutSeconds
        {
            get { return _GeneralOptions.TrackingTimeoutSeconds; }
            set { _GeneralOptions.TrackingTimeoutSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int ShortTrailLengthSeconds
        {
            get { return _GeneralOptions.ShortTrailLengthSeconds; }
            set { _GeneralOptions.ShortTrailLengthSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public List<RebroadcastSettings> RebroadcastSettings
        {
            get { return _GeneralOptions.RebroadcastSettings; }
        }
        #endregion

        #region Audio settings
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool AudioEnabled
        {
            get { return _GeneralOptions.AudioEnabled; }
            set { _GeneralOptions.AudioEnabled = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string TextToSpeechVoice
        {
            get { return _GeneralOptions.TextToSpeechVoice == TextToSpeechVoiceTypeConverter.DefaultVoiceName() ? null : _GeneralOptions.TextToSpeechVoice; }
            set { _GeneralOptions.TextToSpeechVoice = value ?? TextToSpeechVoiceTypeConverter.DefaultVoiceName(); }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int TextToSpeechSpeed
        {
            get { return _GeneralOptions.TextToSpeechSpeed; }
            set { _GeneralOptions.TextToSpeechSpeed = value; }
        }
        #endregion

        #region Data feed options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public DataSource BaseStationDataSource
        {
            get { return _DataSourcesSheet.DataSource; }
            set { _DataSourcesSheet.DataSource = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public ConnectionType BaseStationConnectionType
        {
            get { return _DataSourcesSheet.ConnectionType; }
            set { _DataSourcesSheet.ConnectionType = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool AutoReconnectAtStartup
        {
            get { return _DataSourcesSheet.AutoReconnectAtStartup; }
            set { _DataSourcesSheet.AutoReconnectAtStartup = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string BaseStationAddress
        {
            get { return _DataSourcesSheet.Address; }
            set { _DataSourcesSheet.Address = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int BaseStationPort
        {
            get { return _DataSourcesSheet.Port; }
            set { _DataSourcesSheet.Port = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string SerialComPort
        {
            get { return _DataSourcesSheet.ComPort; }
            set { _DataSourcesSheet.ComPort = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int SerialBaudRate
        {
            get { return _DataSourcesSheet.BaudRate; }
            set { _DataSourcesSheet.BaudRate = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int SerialDataBits
        {
            get { return _DataSourcesSheet.DataBits; }
            set { _DataSourcesSheet.DataBits = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public StopBits SerialStopBits
        {
            get { return _DataSourcesSheet.StopBits; }
            set { _DataSourcesSheet.StopBits = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public Parity SerialParity
        {
            get { return _DataSourcesSheet.Parity; }
            set { _DataSourcesSheet.Parity = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public Handshake SerialHandshake
        {
            get { return _DataSourcesSheet.Handshake; }
            set { _DataSourcesSheet.Handshake = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string SerialStartupText
        {
            get { return _DataSourcesSheet.StartupText; }
            set { _DataSourcesSheet.StartupText = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string SerialShutdownText
        {
            get { return _DataSourcesSheet.ShutdownText; }
            set { _DataSourcesSheet.ShutdownText = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string BaseStationDatabaseFileName
        {
            get { return _DataSourcesSheet.DatabaseFileName; }
            set { _DataSourcesSheet.DatabaseFileName = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string OperatorFlagsFolder
        {
            get { return _DataSourcesSheet.FlagsFolder; }
            set { _DataSourcesSheet.FlagsFolder = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string SilhouettesFolder
        {
            get { return _DataSourcesSheet.SilhouettesFolder; }
            set { _DataSourcesSheet.SilhouettesFolder = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string PicturesFolder
        {
            get { return _DataSourcesSheet.PicturesFolder; }
            set { _DataSourcesSheet.PicturesFolder = value; }
        }
        #endregion

        #region Raw decoding options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingReceiverLocationId
        {
            get { return _RawFeedSheet.ReceiverLocationOptions.CurrentReceiverId; }
            set { _RawFeedSheet.ReceiverLocationOptions.CurrentReceiverId = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public List<ReceiverLocation> RawDecodingReceiverLocations
        {
            get { return _RawFeedSheet.ReceiverLocationOptions.ReceiverLocations; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingReceiverRange
        {
            get { return _RawFeedSheet.ReceiverRange; }
            set { _RawFeedSheet.ReceiverRange = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingSuppressReceiverRangeCheck
        {
            get { return _RawFeedSheet.SuppressReceiverRangeCheck; }
            set { _RawFeedSheet.SuppressReceiverRangeCheck = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingIgnoreMilitaryExtendedSquitter
        {
            get { return _RawFeedSheet.IgnoreMilitaryExtendedSquitter; }
            set { _RawFeedSheet.IgnoreMilitaryExtendedSquitter = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingUseLocalDecodeForInitialPosition
        {
            get { return _RawFeedSheet.UseLocalDecodeForInitialPosition; }
            set { _RawFeedSheet.UseLocalDecodeForInitialPosition = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingAirborneGlobalPositionLimit
        {
            get { return _RawFeedSheet.AirborneGlobalPositionLimit; }
            set { _RawFeedSheet.AirborneGlobalPositionLimit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingFastSurfaceGlobalPositionLimit
        {
            get { return _RawFeedSheet.FastSurfaceGlobalPositionLimit; }
            set { _RawFeedSheet.FastSurfaceGlobalPositionLimit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int RawDecodingSlowSurfaceGlobalPositionLimit
        {
            get { return _RawFeedSheet.SlowSurfaceGlobalPositionLimit; }
            set { _RawFeedSheet.SlowSurfaceGlobalPositionLimit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double RawDecodingAcceptableAirborneSpeed
        {
            get { return _RawFeedSheet.AcceptableAirborneSpeed; }
            set { _RawFeedSheet.AcceptableAirborneSpeed = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double RawDecodingAcceptableAirSurfaceTransitionSpeed
        {
            get { return _RawFeedSheet.AcceptableAirSurfaceTransitionSpeed; }
            set { _RawFeedSheet.AcceptableAirSurfaceTransitionSpeed = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double RawDecodingAcceptableSurfaceSpeed
        {
            get { return _RawFeedSheet.AcceptableSurfaceSpeed; }
            set { _RawFeedSheet.AcceptableSurfaceSpeed = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool RawDecodingIgnoreCallsignsInBds20
        {
            get { return _RawFeedSheet.IgnoreCallsignsInBds20; }
            set { _RawFeedSheet.IgnoreCallsignsInBds20 = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInPI0Count
        {
            get { return _RawFeedSheet.AcceptIcaoInPI0Count; }
            set { _RawFeedSheet.AcceptIcaoInPI0Count = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInPI0Seconds
        {
            get { return _RawFeedSheet.AcceptIcaoInPI0Seconds; }
            set { _RawFeedSheet.AcceptIcaoInPI0Seconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInNonPICount
        {
            get { return _RawFeedSheet.AcceptIcaoInNonPICount; }
            set { _RawFeedSheet.AcceptIcaoInNonPICount = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int AcceptIcaoInNonPISeconds
        {
            get { return _RawFeedSheet.AcceptIcaoInNonPISeconds; }
            set { _RawFeedSheet.AcceptIcaoInNonPISeconds = value; }
        }
        #endregion

        #region Web server options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool WebServerUserMustAuthenticate
        {
            get { return _WebServerSheet.UserMustAuthenticate; }
            set { _WebServerSheet.UserMustAuthenticate = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string WebServerUserName
        {
            get { return _WebServerSheet.UserName; }
            set { _WebServerSheet.UserName = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool WebServerPasswordHasChanged
        {
            get { return WebServerPassword != _SurrogatePassword; }
            set { if(!value) WebServerPassword = _SurrogatePassword; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string WebServerPassword
        {
            get { return _WebServerSheet.Password; }
            set { _WebServerSheet.Password = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool EnableUPnpFeatures
        {
            get { return _WebServerSheet.EnableUPnpFeatures; }
            set { _WebServerSheet.EnableUPnpFeatures = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool IsOnlyVirtualRadarServerOnLan
        {
            get { return _WebServerSheet.IsOnlyVirtualRadarServerOnLan; }
            set { _WebServerSheet.IsOnlyVirtualRadarServerOnLan = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool AutoStartUPnp
        {
            get { return _WebServerSheet.AutoStartUPnp; }
            set { _WebServerSheet.AutoStartUPnp = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int UPnpPort
        {
            get { return _WebServerSheet.UPnpPort; }
            set { _WebServerSheet.UPnpPort = value; }
        }
        #endregion

        #region Internet client restrictions
        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanRunReports
        {
            get { return _WebServerSheet.InternetClientCanRunReports; }
            set { _WebServerSheet.InternetClientCanRunReports = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanPlayAudio
        {
            get { return _WebServerSheet.InternetClientCanPlayAudio; }
            set { _WebServerSheet.InternetClientCanPlayAudio = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanSeePictures
        {
            get { return _WebServerSheet.InternetClientCanSeePictures; }
            set { _WebServerSheet.InternetClientCanSeePictures = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int InternetClientTimeoutMinutes
        {
            get { return _WebServerSheet.InternetClientTimeoutMinutes; }
            set { _WebServerSheet.InternetClientTimeoutMinutes = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanSeeLabels
        {
            get { return _WebServerSheet.InternetClientCanSeeLabels; }
            set { _WebServerSheet.InternetClientCanSeeLabels = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool AllowInternetProximityGadgets
        {
            get { return _WebServerSheet.AllowInternetProximityGadgets; }
            set { _WebServerSheet.AllowInternetProximityGadgets = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool InternetClientCanSubmitRoutes
        {
            get { return _WebServerSheet.InternetClientCanSubmitRoutes; }
            set { _WebServerSheet.InternetClientCanSubmitRoutes = value; }
        }
        #endregion

        #region Web site options
        /// <summary>
        /// See interface docs.
        /// </summary>
        public double InitialGoogleMapLatitude
        {
            get { return _WebSiteSheet.InitialGoogleMapLatitude; }
            set { _WebSiteSheet.InitialGoogleMapLatitude = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public double InitialGoogleMapLongitude
        {
            get { return _WebSiteSheet.InitialGoogleMapLongitude; }
            set { _WebSiteSheet.InitialGoogleMapLongitude = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string InitialGoogleMapType
        {
            get { return _WebSiteSheet.InitialGoogleMapType; }
            set { _WebSiteSheet.InitialGoogleMapType = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int InitialGoogleMapZoom
        {
            get { return _WebSiteSheet.InitialGoogleMapZoom; }
            set { _WebSiteSheet.InitialGoogleMapZoom = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int InitialGoogleMapRefreshSeconds
        {
            get { return _WebSiteSheet.InitialGoogleMapRefreshSeconds; }
            set { _WebSiteSheet.InitialGoogleMapRefreshSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int MinimumGoogleMapRefreshSeconds
        {
            get { return _WebSiteSheet.MinimumGoogleMapRefreshSeconds; }
            set { _WebSiteSheet.MinimumGoogleMapRefreshSeconds = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public DistanceUnit InitialDistanceUnit
        {
            get { return _WebSiteSheet.InitialDistanceUnit; }
            set { _WebSiteSheet.InitialDistanceUnit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public HeightUnit InitialHeightUnit
        {
            get { return _WebSiteSheet.InitialHeightUnit; }
            set { _WebSiteSheet.InitialHeightUnit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public SpeedUnit InitialSpeedUnit
        {
            get { return _WebSiteSheet.InitialSpeedUnit; }
            set { _WebSiteSheet.InitialSpeedUnit = value; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool PreferIataAirportCodes
        {
            get { return _WebSiteSheet.PreferIataAirportCodes; }
            set { _WebSiteSheet.PreferIataAirportCodes = value; }
        }
        #endregion
        #endregion

        #region Events exposed
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ResetToDefaultsClicked;

        /// <summary>
        /// Raises <see cref="ResetToDefaultsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnResetToDefaultsClicked(EventArgs args)
        {
            if(ResetToDefaultsClicked != null) ResetToDefaultsClicked(this, args);
            propertyGrid.Refresh();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler SaveClicked;

        /// <summary>
        /// Raises <see cref="SaveClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnSaveClicked(EventArgs args)
        {
            if(SaveClicked != null) SaveClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler TestConnectionClicked;

        /// <summary>
        /// Raises <see cref="TestConnectionClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnTestBaseStationConnectionSettingsClicked(EventArgs args)
        {
            if(TestConnectionClicked != null) TestConnectionClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler TestTextToSpeechSettingsClicked;

        /// <summary>
        /// Raises <see cref="TestTextToSpeechSettingsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnTestTextToSpeechSettingsClicked(EventArgs args)
        {
            if(TestTextToSpeechSettingsClicked != null) TestTextToSpeechSettingsClicked(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler UseIcaoRawDecodingSettingsClicked;

        /// <summary>
        /// Raises <see cref="UseIcaoRawDecodingSettingsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnUseIcaoRawDecodingSettingsClicked(EventArgs args)
        {
            if(UseIcaoRawDecodingSettingsClicked != null) UseIcaoRawDecodingSettingsClicked(this, args);
            propertyGrid.Refresh();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler UseRecommendedRawDecodingSettingsClicked;

        /// <summary>
        /// Raises <see cref="UseRecommendedRawDecodingSettingsClicked"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnUseRecommendedRawDecodingSettingsClicked(EventArgs args)
        {
            if(UseRecommendedRawDecodingSettingsClicked != null) UseRecommendedRawDecodingSettingsClicked(this, args);
            propertyGrid.Refresh();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ValuesChanged;

        /// <summary>
        /// Raises <see cref="ValuesChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnValuesChanged(EventArgs args)
        {
            if(ValuesChanged != null) ValuesChanged(this, args);
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public OptionsPropertySheetView()
        {
            _MonoAutoScaleMode = new MonoAutoScaleMode(this);
            InitializeComponent();

            labelValidationMessages.Text = "";
            buttonSheetButton.Visible = false;

            _Sheets.AddRange(new ISheet[] {
                _DataSourcesSheet,
                _RawFeedSheet,
                _WebServerSheet,
                _WebSiteSheet,
                _GeneralOptions,
            });
        }
        #endregion

        #region ArrangeControls, Populate, RecordInitialValues
        /// <summary>
        /// Moves the controls into their final position.
        /// </summary>
        /// <remarks>
        /// Some of the controls sit on top of each other at run-time, which is messy to do in the designer.
        /// This just moves controls from their designer positions to their finished locations.
        /// </remarks>
        private void ArrangeControls()
        {
            var rawDecodingLinkLabelWidth = Math.Max(linkLabelUseRecommendedSettings.Width, linkLabelUseIcaoSettings.Width);
            linkLabelUseIcaoSettings.Left = buttonSheetButton.Right - rawDecodingLinkLabelWidth;
            linkLabelUseRecommendedSettings.Left = buttonSheetButton.Right - rawDecodingLinkLabelWidth;
            labelValidationMessages.Width = (buttonSheetButton.Left - 6) - labelValidationMessages.Left;

            buttonSheetButton.Visible = linkLabelUseIcaoSettings.Visible = linkLabelUseRecommendedSettings.Visible = false;
        }

        /// <summary>
        /// Populates the controls.
        /// </summary>
        private void Populate()
        {
            listBox.Items.Clear();
            foreach(var sheet in _Sheets) {
                listBox.Items.Add(sheet);
            }
        }

        /// <summary>
        /// Records the current values in every property in every sheet as the default value.
        /// </summary>
        private void RecordInitialValues()
        {
            foreach(var sheet in _Sheets) {
                sheet.SetInitialValues();
            }
        }
        #endregion

        #region Presenter helpers
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="voiceNames"></param>
        public void PopulateTextToSpeechVoices(IEnumerable<string> voiceNames)
        {
            TextToSpeechVoiceTypeConverter.PopulateWithVoiceNames(voiceNames);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        public void ShowTestConnectionResults(string message, string title)
        {
            MessageBox.Show(message, title);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="isBusy"></param>
        /// <param name="previousState"></param>
        /// <returns></returns>
        public object ShowBusy(bool isBusy, object previousState)
        {
            return BusyViewHelper.ShowBusy(isBusy, previousState);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="results"></param>
        public void ShowValidationResults(IEnumerable<ValidationResult> results)
        {
            ISheet selectSheet = null;
            var messages = new StringBuilder();
            foreach(var result in results.Where(r => !r.IsWarning)) {
                selectSheet = AddValidationMessage(selectSheet, messages, result);
            }
            foreach(var result in results.Where(r => r.IsWarning)) {
                AddValidationMessage(selectSheet, messages, result);
            }

            labelValidationMessages.Text = messages.ToString();
            if(selectSheet != null) SelectedSheet = selectSheet;

            if(results.Where(r => !r.IsWarning).Count() > 0) DialogResult = DialogResult.None;
        }

        private ISheet AddValidationMessage(ISheet selectSheet, StringBuilder messages, ValidationResult result)
        {
            if(messages.Length != 0) messages.Append("\r\n");
            messages.AppendFormat("{0}{1}", result.IsWarning ? Localise.GetLocalisedText("::Warning:::") : "", result.Message);

            if(selectSheet == null) {
                switch(result.Field) {
                    case ValidationField.BaseStationAddress:
                    case ValidationField.BaseStationPort:
                    case ValidationField.ComPort:
                    case ValidationField.BaudRate:
                    case ValidationField.DataBits:
                    case ValidationField.BaseStationDatabase:
                    case ValidationField.FlagsFolder:
                    case ValidationField.PicturesFolder:
                    case ValidationField.SilhouettesFolder:
                        selectSheet = _DataSourcesSheet;
                        break;
                    case ValidationField.Location:
                    case ValidationField.ReceiverRange:
                    case ValidationField.AcceptableAirborneLocalPositionSpeed:
                    case ValidationField.AcceptableSurfaceLocalPositionSpeed:
                    case ValidationField.AcceptableTransitionLocalPositionSpeed:
                    case ValidationField.AirborneGlobalPositionLimit:
                    case ValidationField.FastSurfaceGlobalPositionLimit:
                    case ValidationField.SlowSurfaceGlobalPositionLimit:
                    case ValidationField.AcceptIcaoInNonPICount:
                    case ValidationField.AcceptIcaoInNonPISeconds:
                    case ValidationField.AcceptIcaoInPI0Count:
                    case ValidationField.AcceptIcaoInPI0Seconds:
                        selectSheet = _RawFeedSheet;
                        break;
                    case ValidationField.WebUserName:
                    case ValidationField.UPnpPortNumber:
                    case ValidationField.InternetUserIdleTimeout:
                        selectSheet = _WebServerSheet;
                        break;
                    case ValidationField.InitialGoogleMapRefreshSeconds:
                    case ValidationField.MinimumGoogleMapRefreshSeconds:
                    case ValidationField.Latitude:
                    case ValidationField.Longitude:
                    case ValidationField.GoogleMapZoomLevel:
                        selectSheet = _WebSiteSheet;
                        break;
                    case ValidationField.CheckForNewVersions:
                    case ValidationField.DisplayTimeout:
                    case ValidationField.ShortTrailLength:
                    case ValidationField.TextToSpeechSpeed:
                    case ValidationField.TrackingTimeout:
                        selectSheet = _GeneralOptions;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            return selectSheet;
        }
        #endregion

        #region Events consumed
        /// <summary>
        /// Called after the form has finished initialising but before it has been shown to the user.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if(!DesignMode) {
                Localise.Form(this);

                ArrangeControls();

                Populate();

                _OnlineHelp = new OnlineHelpHelper(this, OnlineHelpAddress.WinFormsOptionsDialog);

                _Presenter = Factory.Singleton.Resolve<IOptionsPresenter>();
                _Presenter.Initialise(this);

                RecordInitialValues();
                listBox.SelectedIndex = 0;

                OnValuesChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Called when an item is selected in the list of sheets.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedSheet = SelectedSheet;
            if(selectedSheet != null) {
                propertyGrid.SelectedObject = selectedSheet;

                var sheetButtonText = "";
                var showResetToRecommended = false;
                var showResetToIcaoSettings = false;

                if(selectedSheet == _DataSourcesSheet)      sheetButtonText = Strings.TestConnection;
                else if(selectedSheet == _RawFeedSheet)     showResetToIcaoSettings = showResetToRecommended = true;
                else if(selectedSheet == _GeneralOptions)   sheetButtonText = Strings.TestAudioSettings;

                buttonSheetButton.Text = sheetButtonText;
                buttonSheetButton.Visible = sheetButtonText != "";
                linkLabelUseIcaoSettings.Visible = showResetToIcaoSettings;
                linkLabelUseRecommendedSettings.Visible = showResetToRecommended;
            }
        }

        /// <summary>
        /// Called when the OK button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            OnSaveClicked(e);
        }

        /// <summary>
        /// Called when a property is changed.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if(e.ChangedItem.PropertyDescriptor.Attributes.OfType<RaisesValuesChangedAttribute>().Any()) {
                OnValuesChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Called when the sheet-specific action button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSheetButton_Click(object sender, EventArgs e)
        {
            var selectedSheet = SelectedSheet;
            if(selectedSheet == _DataSourcesSheet)      OnTestBaseStationConnectionSettingsClicked(EventArgs.Empty);
            else if(selectedSheet == _GeneralOptions)   OnTestTextToSpeechSettingsClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Called when the user clicks the 'use ICAO settings' button on the raw decoding property sheet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabelUseIcaoSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnUseIcaoRawDecodingSettingsClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Called when the user clicks the 'use recommended settings' button on the raw decoding sheet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabelUseRecommendedSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnUseRecommendedRawDecodingSettingsClicked(EventArgs.Empty);
        }

        /// <summary>
        /// Called when the user clicks the reset-to-defaults button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabelResetToDefaults_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnResetToDefaultsClicked(EventArgs.Empty);
        }
        #endregion
    }
}
