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
using InterfaceFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Test.Framework;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.ModeS;
using VirtualRadar.Interface.Adsb;
using System.IO.Ports;

namespace Test.VirtualRadar.Library.Listener
{
    [TestClass]
    public class AutoConfigListenerTests
    {
        #region Private class
        /// <summary>
        /// A class that describes a configuration setting that has some significane to the auto-config listener.
        /// </summary>
        class SettingsProperty
        {
            public string Name;                             // The name of the property
            public Action<Configuration> ChangeProperty;    // A delegate that changes the property to a non-default value

            public SettingsProperty()
            {
            }

            public SettingsProperty(string name, Action<Configuration> changeProperty) : this()
            {
                Name = name;
                ChangeProperty = changeProperty;
            }
        }

        /// <summary>
        /// A class that describes a configuration setting carrying a connection property.
        /// </summary>
        class ConnectionProperty : SettingsProperty
        {
            public List<ConnectionType> ConnectionTypes;    // The connection types that depend upon the property

            public ConnectionProperty() : this(default(ConnectionType), null, null)
            {
            }

            public ConnectionProperty(ConnectionType connectionType, string name, Action<Configuration> changeProperty) : base(name, changeProperty)
            {
                ConnectionTypes = new List<ConnectionType>();
                ConnectionTypes.Add(connectionType);
            }

            public bool MatchesConnectionType(ConnectionType connectionType)
            {
                return ConnectionTypes.Contains(connectionType);
            }
        }
        #endregion

        #region Fields, TestInitialise, TestCleanup
        public TestContext TestContext { get; set; }

        private IClassFactory _OriginalClassFactory;
        private IAutoConfigListener _AutoConfigListener;
        private Mock<IConfigurationStorage> _ConfigurationStorage;
        private Configuration _Configuration;
        private Mock<IListener> _Listener;
        private Mock<ITcpListenerProvider> _TcpProvider;
        private Mock<ISerialListenerProvider> _SerialProvider;
        private Mock<IPort30003MessageBytesExtractor> _Port30003Extractor;
        private Mock<ISbs3MessageBytesExtractor> _Sbs3MessageBytesExtractor;
        private Mock<IBeastMessageBytesExtractor> _BeastMessageBytesExtractor;
        private Mock<IRawMessageTranslator> _RawMessageTranslator;
        private Mock<IStatistics> _Statistics;

        private readonly List<ConnectionProperty> _ConnectionProperties = new List<ConnectionProperty>() {
            new ConnectionProperty(ConnectionType.TCP, "Address",       c => c.BaseStationSettings.Address = "9.8.7.6"),
            new ConnectionProperty(ConnectionType.TCP, "Port",          c => c.BaseStationSettings.Port = 77),

            new ConnectionProperty(ConnectionType.COM, "ComPort",       c => c.BaseStationSettings.ComPort = "COM99"),
            new ConnectionProperty(ConnectionType.COM, "BaudRate",      c => c.BaseStationSettings.BaudRate = 9600),
            new ConnectionProperty(ConnectionType.COM, "DataBits",      c => c.BaseStationSettings.DataBits = 7),
            new ConnectionProperty(ConnectionType.COM, "StopBits",      c => c.BaseStationSettings.StopBits = StopBits.OnePointFive),
            new ConnectionProperty(ConnectionType.COM, "Parity",        c => c.BaseStationSettings.Parity = Parity.Odd),
            new ConnectionProperty(ConnectionType.COM, "Handshake",     c => c.BaseStationSettings.Handshake = Handshake.XOnXOff),
            new ConnectionProperty(ConnectionType.COM, "StartupText",   c => c.BaseStationSettings.StartupText = "UP"),
            new ConnectionProperty(ConnectionType.COM, "ShutdownText",  c => c.BaseStationSettings.ShutdownText = "DOWN"),
        };

        private readonly List<SettingsProperty> _RawMessageTranslatorProperties = new List<SettingsProperty>() {
            new SettingsProperty("AirborneGlobalPositionLimit",         s => s.RawDecodingSettings.AirborneGlobalPositionLimit = 999),
            new SettingsProperty("FastSurfaceGlobalPositionLimit",      s => s.RawDecodingSettings.FastSurfaceGlobalPositionLimit = 998),
            new SettingsProperty("SlowSurfaceGlobalPositionLimit",      s => s.RawDecodingSettings.SlowSurfaceGlobalPositionLimit = 997),
            new SettingsProperty("AcceptableAirborneSpeed",             s => s.RawDecodingSettings.AcceptableAirborneSpeed = 996),
            new SettingsProperty("AcceptableSurfaceSpeed",              s => s.RawDecodingSettings.AcceptableSurfaceSpeed = 995),
            new SettingsProperty("AcceptableAirSurfaceTransitionSpeed", s => s.RawDecodingSettings.AcceptableAirSurfaceTransitionSpeed = 994),
            new SettingsProperty("ReceiverRange",                       s => s.RawDecodingSettings.ReceiverRange = 993),
            new SettingsProperty("IgnoreMilitaryExtendedSquitter",      s => s.RawDecodingSettings.IgnoreMilitaryExtendedSquitter = true),
            new SettingsProperty("ReceiverLocation",                    s => s.RawDecodingSettings.ReceiverLocationId = 2),
            new SettingsProperty("TrackingTimeoutSeconds",              s => s.BaseStationSettings.TrackingTimeoutSeconds = 100),
        };

        private readonly List<ReceiverLocation> _DefaultReceiverLocations = new List<ReceiverLocation>() {
            new ReceiverLocation() { UniqueId = 1, Name = "First", Latitude = 1.1, Longitude = 2.2 },
            new ReceiverLocation() { UniqueId = 2, Name = "Second", Latitude = 3.3, Longitude = 4.4 },
        };

        [TestInitialize]
        public void TestInitialise()
        {
            _OriginalClassFactory = Factory.TakeSnapshot();

            _ConfigurationStorage = TestUtilities.CreateMockSingleton<IConfigurationStorage>();
            _Configuration = new Configuration();
            _Configuration.ReceiverLocations.AddRange(_DefaultReceiverLocations);
            _ConfigurationStorage.Setup(r => r.Load()).Returns(_Configuration);

            _Statistics = TestUtilities.CreateMockSingleton<IStatistics>();

            _Listener = TestUtilities.CreateMockImplementation<IListener>();
            _Listener.Setup(r => r.Provider).Returns((IListenerProvider)null);
            _Listener.Setup(r => r.BytesExtractor).Returns((IMessageBytesExtractor)null);
            _Listener.Setup(r => r.RawMessageTranslator).Returns((IRawMessageTranslator)null);
            _Listener.Setup(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), It.IsAny<bool>()))
                     .Callback((IListenerProvider provider, IMessageBytesExtractor extractor, IRawMessageTranslator translator, bool reconnect) => {
                _Listener.Setup(r => r.Provider).Returns(provider);
                _Listener.Setup(r => r.BytesExtractor).Returns(extractor);
                _Listener.Setup(r => r.RawMessageTranslator).Returns(translator);
            });

            CreateNewListenerChildObjectInstances();

            _AutoConfigListener = Factory.Singleton.Resolve<IAutoConfigListener>();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Factory.RestoreSnapshot(_OriginalClassFactory);

            if(_AutoConfigListener != null) _AutoConfigListener.Dispose();
        }
        #endregion

        #region Utility methods
        private void DoForAllSourcesAndConnectionTypes(Action<DataSource, ConnectionType, string> action)
        {
            foreach(DataSource dataSource in Enum.GetValues(typeof(DataSource))) {
                foreach(ConnectionType connectionType in Enum.GetValues(typeof(ConnectionType))) {
                    TestCleanup();
                    TestInitialise();

                    _Configuration.BaseStationSettings.DataSource = dataSource;
                    _Configuration.BaseStationSettings.ConnectionType = connectionType;

                    action(dataSource, connectionType, String.Format("DataSource {0} ConnectionType {1}", dataSource, connectionType));
                }
            }
        }

        private void CreateNewListenerChildObjectInstances()
        {
            // The TestInitialise method sets up the different listener providers, byte extractors and raw message translators
            // so that every time the code asks for one they'll get the same instance back. This is fine as far as it goes but
            // it makes it hard to test that new instances are created when appropriate. This method creates a new set of objects
            // and registers them as the default object.
            _TcpProvider = TestUtilities.CreateMockImplementation<ITcpListenerProvider>();
            _SerialProvider = TestUtilities.CreateMockImplementation<ISerialListenerProvider>();

            _Port30003Extractor = TestUtilities.CreateMockImplementation<IPort30003MessageBytesExtractor>();
            _Sbs3MessageBytesExtractor = TestUtilities.CreateMockImplementation<ISbs3MessageBytesExtractor>();
            _BeastMessageBytesExtractor = TestUtilities.CreateMockImplementation<IBeastMessageBytesExtractor>();

            _RawMessageTranslator = TestUtilities.CreateMockImplementation<IRawMessageTranslator>();
            _RawMessageTranslator.Object.ReceiverLocation = null;
        }
        #endregion

        #region Constructor and Properties
        [TestMethod]
        public void AutoConfigListener_Constructor_Initialises_To_Known_State()
        {
            Assert.IsNull(_AutoConfigListener.Listener);
        }

        [TestMethod]
        public void AutoConfigListener_Singleton_Returns_Same_Reference_For_Different_Instances()
        {
            var instance1 = Factory.Singleton.Resolve<IAutoConfigListener>();
            var instance2 = Factory.Singleton.Resolve<IAutoConfigListener>();

            Assert.AreNotSame(instance1, instance2);
            Assert.IsNotNull(instance1.Singleton);
            Assert.AreSame(instance1.Singleton, instance2.Singleton);
        }
        #endregion

        #region Initialise
        [TestMethod]
        public void AutoConfigListener_Initialise_Creates_Listener()
        {
            _AutoConfigListener.Initialise();
            Assert.AreSame(_Listener.Object, _AutoConfigListener.Listener);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AutoConfigListener_Initialise_Cannot_Be_Called_More_Than_Once()
        {
            _AutoConfigListener.Initialise();
            _AutoConfigListener.Initialise();
        }

        [TestMethod]
        public void AutoConfigListener_Initialise_Calls_ChangeSource_With_Correct_Parameters_For_DataSource_And_ConnectionType()
        {
            DoForAllSourcesAndConnectionTypes((dataSource, connectionType, failMessage) => {
                _AutoConfigListener.Initialise();

                switch(dataSource) {
                    case DataSource.Beast:      Assert.AreSame(_BeastMessageBytesExtractor.Object, _Listener.Object.BytesExtractor); break;
                    case DataSource.Port30003:  Assert.AreSame(_Port30003Extractor.Object, _Listener.Object.BytesExtractor); break;
                    case DataSource.Sbs3:       Assert.AreSame(_Sbs3MessageBytesExtractor.Object, _Listener.Object.BytesExtractor); break;
                    default:                    throw new NotImplementedException();
                }

                switch(connectionType) {
                    case ConnectionType.COM:    Assert.AreSame(_SerialProvider.Object, _Listener.Object.Provider); break;
                    case ConnectionType.TCP:    Assert.AreSame(_TcpProvider.Object, _Listener.Object.Provider); break;
                    default:                    throw new NotImplementedException();
                }

                Assert.AreSame(_RawMessageTranslator.Object, _Listener.Object.RawMessageTranslator);
            });
        }

        [TestMethod]
        public void AutoConfigListener_Initialise_Applies_Configuration_Settings_For_Connection_Type()
        {
            Do_Check_Configuration_Changes_Are_Applied(false, () => { _AutoConfigListener.Initialise(); });
        }

        private void Do_Check_Configuration_Changes_Are_Applied(bool initialiseFirst, Action action)
        {
            foreach(ConnectionType connectionType in Enum.GetValues(typeof(ConnectionType))) {
                TestCleanup();
                TestInitialise();

                if(initialiseFirst) _AutoConfigListener.Initialise();

                _Configuration.BaseStationSettings.ConnectionType = connectionType;

                _Configuration.BaseStationSettings.Address = "TCP Address";
                _Configuration.BaseStationSettings.Port = 12345;
                _Configuration.BaseStationSettings.IgnoreBadMessages = true;

                _Configuration.BaseStationSettings.ComPort = "Serial COM Port";
                _Configuration.BaseStationSettings.BaudRate = 10;
                _Configuration.BaseStationSettings.DataBits = 9;
                _Configuration.BaseStationSettings.StopBits = StopBits.Two;
                _Configuration.BaseStationSettings.Parity = Parity.Mark;
                _Configuration.BaseStationSettings.Handshake = Handshake.XOnXOff;
                _Configuration.BaseStationSettings.StartupText = "Up";
                _Configuration.BaseStationSettings.ShutdownText = "Down";

                action();

                Assert.AreEqual(true, _Listener.Object.IgnoreBadMessages);
                switch(connectionType) {
                    case ConnectionType.COM:
                        Assert.AreEqual("Serial COM Port", _SerialProvider.Object.ComPort);
                        Assert.AreEqual(10, _SerialProvider.Object.BaudRate);
                        Assert.AreEqual(9, _SerialProvider.Object.DataBits);
                        Assert.AreEqual(StopBits.Two, _SerialProvider.Object.StopBits);
                        Assert.AreEqual(Parity.Mark, _SerialProvider.Object.Parity);
                        Assert.AreEqual(Handshake.XOnXOff, _SerialProvider.Object.Handshake);
                        Assert.AreEqual("Up", _SerialProvider.Object.StartupText);
                        Assert.AreEqual("Down", _SerialProvider.Object.ShutdownText);
                        break;
                    case ConnectionType.TCP:
                        Assert.AreEqual("TCP Address", _TcpProvider.Object.Address);
                        Assert.AreEqual(12345, _TcpProvider.Object.Port);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        [TestMethod]
        public void AutoConfigListener_Initialise_Copies_Configuration_To_RawTranslator()
        {
            Do_Check_Configuration_Changes_Copied_To_RawTranslator(false, () => _AutoConfigListener.Initialise());
        }

        private void Do_Check_Configuration_Changes_Copied_To_RawTranslator(bool initialiseFirst, Action triggerAction)
        {
            if(initialiseFirst) _AutoConfigListener.Initialise();

            _Configuration.BaseStationSettings.DataSource = DataSource.Sbs3;
            _Configuration.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 12, Latitude = 1.2345, Longitude = -17.89 });

            _Configuration.RawDecodingSettings.AcceptableAirborneSpeed = 999.123;
            _Configuration.RawDecodingSettings.AcceptableAirSurfaceTransitionSpeed = 888.456;
            _Configuration.RawDecodingSettings.AcceptableSurfaceSpeed = 777.789;
            _Configuration.RawDecodingSettings.AirborneGlobalPositionLimit = 99;
            _Configuration.RawDecodingSettings.FastSurfaceGlobalPositionLimit = 88;
            _Configuration.RawDecodingSettings.SlowSurfaceGlobalPositionLimit = 77;
            _Configuration.RawDecodingSettings.IgnoreCallsignsInBds20 = true;
            _Configuration.RawDecodingSettings.IgnoreMilitaryExtendedSquitter = true;
            _Configuration.RawDecodingSettings.ReceiverLocationId = 12;
            _Configuration.RawDecodingSettings.ReceiverRange = 1234;
            _Configuration.RawDecodingSettings.SuppressReceiverRangeCheck = true;
            _Configuration.RawDecodingSettings.UseLocalDecodeForInitialPosition = true;
            _Configuration.BaseStationSettings.TrackingTimeoutSeconds = 100;
            _Configuration.RawDecodingSettings.AcceptIcaoInNonPICount = 8;
            _Configuration.RawDecodingSettings.AcceptIcaoInNonPISeconds = 16;
            _Configuration.RawDecodingSettings.AcceptIcaoInPI0Count = 24;
            _Configuration.RawDecodingSettings.AcceptIcaoInPI0Seconds = 32;
            triggerAction();

            Assert.AreEqual(1.2345, _RawMessageTranslator.Object.ReceiverLocation.Latitude);
            Assert.AreEqual(-17.89, _RawMessageTranslator.Object.ReceiverLocation.Longitude);
            Assert.AreEqual(99000, _RawMessageTranslator.Object.GlobalDecodeAirborneThresholdMilliseconds);
            Assert.AreEqual(88000, _RawMessageTranslator.Object.GlobalDecodeFastSurfaceThresholdMilliseconds);
            Assert.AreEqual(77000, _RawMessageTranslator.Object.GlobalDecodeSlowSurfaceThresholdMilliseconds);
            Assert.AreEqual(true, _RawMessageTranslator.Object.SuppressCallsignsFromBds20);
            Assert.AreEqual(true, _RawMessageTranslator.Object.IgnoreMilitaryExtendedSquitter);
            Assert.AreEqual(999.123, _RawMessageTranslator.Object.LocalDecodeMaxSpeedAirborne);
            Assert.AreEqual(888.456, _RawMessageTranslator.Object.LocalDecodeMaxSpeedTransition);
            Assert.AreEqual(777.789, _RawMessageTranslator.Object.LocalDecodeMaxSpeedSurface);
            Assert.AreEqual(1234, _RawMessageTranslator.Object.ReceiverRangeKilometres);
            Assert.AreEqual(true, _RawMessageTranslator.Object.SuppressReceiverRangeCheck);
            Assert.AreEqual(true, _RawMessageTranslator.Object.UseLocalDecodeForInitialPosition);
            Assert.AreEqual(100, _RawMessageTranslator.Object.TrackingTimeoutSeconds);
            Assert.AreEqual(8, _RawMessageTranslator.Object.AcceptIcaoInNonPICount);
            Assert.AreEqual(16000, _RawMessageTranslator.Object.AcceptIcaoInNonPIMilliseconds);
            Assert.AreEqual(24, _RawMessageTranslator.Object.AcceptIcaoInPI0Count);
            Assert.AreEqual(32000, _RawMessageTranslator.Object.AcceptIcaoInPI0Milliseconds);
        }

        [TestMethod]
        public void AutoConfigListener_Initialise_Does_Not_Connect_Listener()
        {
            _AutoConfigListener.Initialise();

            _Listener.Verify(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), false), Times.Once());
            _Listener.Verify(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), true), Times.Never());
            _Listener.Verify(r => r.Connect(It.IsAny<bool>()), Times.Never());
        }
        #endregion

        #region Configuration change
        [TestMethod]
        public void AutoConfigListener_Configuration_Changes_Ignored_If_Raised_Before_Initialise_Called()
        {
            _Configuration.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 1, Latitude = 1.0, Longitude = 2.0 });
            _Configuration.BaseStationSettings.Address = "abc";
            _Configuration.BaseStationSettings.ComPort = "xyz";
            _Configuration.BaseStationSettings.IgnoreBadMessages = true;

            _ConfigurationStorage.Raise(c => c.ConfigurationChanged += null, EventArgs.Empty);

            Assert.AreEqual(null, _TcpProvider.Object.Address);
            Assert.AreEqual(null, _SerialProvider.Object.ComPort);
            Assert.AreEqual(false, _Listener.Object.IgnoreBadMessages);
            Assert.AreEqual(null, _RawMessageTranslator.Object.ReceiverLocation);
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Changes_Copied_To_Listener()
        {
            Do_Check_Configuration_Changes_Are_Applied(true, () => { _ConfigurationStorage.Raise(c => c.ConfigurationChanged += null, EventArgs.Empty); });
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Changes_Copied_To_RawTranslator()
        {
            Do_Check_Configuration_Changes_Copied_To_RawTranslator(true, () => _ConfigurationStorage.Raise(c => c.ConfigurationChanged += null, EventArgs.Empty));
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Changes_Ignored_After_Dispose_Called()
        {
            _AutoConfigListener.Initialise();
            _AutoConfigListener.Dispose();

            _Configuration.BaseStationSettings.IgnoreBadMessages = false;
            _ConfigurationStorage.Raise(c => c.ConfigurationChanged += null, EventArgs.Empty);

            Assert.AreEqual(true, _Listener.Object.IgnoreBadMessages);
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Change_Only_Creates_New_BytesExtractor_When_DataSource_Changes()
        {
            foreach(DataSource initialDataSource in Enum.GetValues(typeof(DataSource))) {
                foreach(DataSource newDataSource in Enum.GetValues(typeof(DataSource))) {
                    TestCleanup();
                    TestInitialise();

                    _Configuration.BaseStationSettings.DataSource = initialDataSource;
                    _AutoConfigListener.Initialise();
                    var initialBytesExtractor = _Listener.Object.BytesExtractor;

                    CreateNewListenerChildObjectInstances();

                    _Configuration.BaseStationSettings.DataSource = newDataSource;
                    _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);

                    var failMessage = String.Format("Initial datasource is {0}, new datasource is {1}", initialDataSource, newDataSource);
                    if(initialDataSource == newDataSource) {
                        Assert.AreSame(initialBytesExtractor, _Listener.Object.BytesExtractor, failMessage);
                    } else {
                        Assert.AreNotSame(initialBytesExtractor, _Listener.Object.BytesExtractor, failMessage);
                    }
                }
            }
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Change_Resets_Statistics_When_DataSource_Changes()
        {
            foreach(DataSource initialDataSource in Enum.GetValues(typeof(DataSource))) {
                foreach(DataSource newDataSource in Enum.GetValues(typeof(DataSource))) {
                    TestCleanup();
                    TestInitialise();

                    _Configuration.BaseStationSettings.DataSource = initialDataSource;
                    _AutoConfigListener.Initialise();

                    CreateNewListenerChildObjectInstances();

                    _Configuration.BaseStationSettings.DataSource = newDataSource;
                    _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);

                    if(initialDataSource == newDataSource) {
                        _Statistics.Verify(r => r.ResetMessageCounters(), Times.Once());
                    } else {
                        _Statistics.Verify(r => r.ResetMessageCounters(), Times.Exactly(2));
                    }
                }
            }
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Change_Only_Creates_New_Provider_When_Connection_Properties_Change()
        {
            foreach(ConnectionType initialConnectionType in Enum.GetValues(typeof(ConnectionType))) {
                foreach(ConnectionType newConnectionType in Enum.GetValues(typeof(ConnectionType))) {
                    foreach(var connectionProperty in _ConnectionProperties) {
                        TestCleanup();
                        TestInitialise();

                        _Configuration.BaseStationSettings.ConnectionType = initialConnectionType;
                        _AutoConfigListener.Initialise();
                        var initialProvider = _Listener.Object.Provider;

                        CreateNewListenerChildObjectInstances();

                        _Configuration.BaseStationSettings.ConnectionType = newConnectionType;
                        connectionProperty.ChangeProperty(_Configuration);

                        _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);

                        var failMessage = String.Format("Initial connectionType is {0}, new connectionType is {1}, changed property {2}", initialConnectionType, newConnectionType, connectionProperty.Name);
                        if(initialConnectionType == newConnectionType && !connectionProperty.MatchesConnectionType(newConnectionType)) {
                            Assert.AreSame(initialProvider, _Listener.Object.Provider, failMessage);
                        } else {
                            Assert.AreNotSame(initialProvider, _Listener.Object.Provider, failMessage);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Change_Resets_Statistics_When_Connection_Properties_Change()
        {
            foreach(ConnectionType initialConnectionType in Enum.GetValues(typeof(ConnectionType))) {
                foreach(ConnectionType newConnectionType in Enum.GetValues(typeof(ConnectionType))) {
                    foreach(var connectionProperty in _ConnectionProperties) {
                        TestCleanup();
                        TestInitialise();

                        _Configuration.BaseStationSettings.ConnectionType = initialConnectionType;
                        _AutoConfigListener.Initialise();
                        var initialProvider = _Listener.Object.Provider;

                        CreateNewListenerChildObjectInstances();

                        _Configuration.BaseStationSettings.ConnectionType = newConnectionType;
                        connectionProperty.ChangeProperty(_Configuration);

                        _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);

                        if(initialConnectionType == newConnectionType && !connectionProperty.MatchesConnectionType(newConnectionType)) {
                            _Statistics.Verify(r => r.ResetMessageCounters(), Times.Once());
                        } else {
                            _Statistics.Verify(r => r.ResetMessageCounters(), Times.Exactly(2));
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Change_Only_Creates_New_RawMessageTranslator_When_ConnectionType_Or_DataSource_Changes()
        {
            var connectionTypes = new ConnectionType[] { ConnectionType.COM, ConnectionType.TCP };
            var dataSources = new DataSource[] { DataSource.Sbs3, DataSource.Beast };
            foreach(ConnectionType initialConnectionType in connectionTypes) {
                foreach(ConnectionType newConnectionType in connectionTypes) {
                    foreach(DataSource initialDataSource in dataSources) {
                        foreach(DataSource newDataSource in dataSources) {
                            foreach(var settingProperty in _RawMessageTranslatorProperties) {
                                TestCleanup();
                                TestInitialise();

                                _Configuration.BaseStationSettings.ConnectionType = initialConnectionType;
                                _Configuration.BaseStationSettings.DataSource = initialDataSource;
                                _AutoConfigListener.Initialise();
                                var initialTranslator = _Listener.Object.RawMessageTranslator;

                                CreateNewListenerChildObjectInstances();

                                _Configuration.BaseStationSettings.ConnectionType = newConnectionType;
                                _Configuration.BaseStationSettings.DataSource = newDataSource;
                                settingProperty.ChangeProperty(_Configuration);

                                _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);

                                var failMessage = String.Format("ConnectionType: from {0} to {1}, DataSource: from {2} to {3}, Changed Property: {4}", initialConnectionType, newConnectionType, initialDataSource, newDataSource, settingProperty.Name);
                                if(initialConnectionType == newConnectionType && initialDataSource == newDataSource) {
                                    Assert.AreSame(initialTranslator, _Listener.Object.RawMessageTranslator, failMessage);
                                } else {
                                    Assert.AreNotSame(initialTranslator, _Listener.Object.RawMessageTranslator, failMessage);
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void AutoConfigListener_Configuration_Change_Does_Not_Call_ChangeSource_If_DataSource_Or_ConnectionType_Has_Not_Changed()
        {
            _AutoConfigListener.Initialise();
            _Listener.Verify(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), It.IsAny<bool>()), Times.Once());

            _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);
            _Listener.Verify(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), It.IsAny<bool>()), Times.Once());

            _Configuration.BaseStationSettings.DataSource = DataSource.Sbs3;
            _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);
            _Listener.Verify(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), It.IsAny<bool>()), Times.Exactly(2));

            _Configuration.BaseStationSettings.ConnectionType = ConnectionType.COM;
            _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);
            _Listener.Verify(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), It.IsAny<bool>()), Times.Exactly(3));

            _Configuration.RawDecodingSettings.ReceiverRange = 700;
            _ConfigurationStorage.Raise(r => r.ConfigurationChanged += null, EventArgs.Empty);
            _Listener.Verify(r => r.ChangeSource(It.IsAny<IListenerProvider>(), It.IsAny<IMessageBytesExtractor>(), It.IsAny<IRawMessageTranslator>(), It.IsAny<bool>()), Times.Exactly(3));
        }
        #endregion

        #region Dispose
        [TestMethod]
        public void AutoConfigListener_Dispose_Passes_Through_To_Listener()
        {
            _AutoConfigListener.Initialise();
            _AutoConfigListener.Dispose();

            _Listener.Verify(r => r.Dispose(), Times.Once());
            // no need to test for RawMessageTranslator getting disposed of, it's disposed by the listener
        }
        #endregion

        #region ExceptionCaught
        [TestMethod]
        public void AutoConfigListener_ExceptionCaught_Passes_On_Exceptions_From_Listener()
        {
            var eventRecorder = new EventRecorder<EventArgs<Exception>>();
            _AutoConfigListener.ExceptionCaught += eventRecorder.Handler;

            _AutoConfigListener.Initialise();

            var exception = new InvalidOperationException();
            _Listener.Raise(r => r.ExceptionCaught += null, new EventArgs<Exception>(exception));

            Assert.AreEqual(1, eventRecorder.CallCount);
            Assert.AreSame(_AutoConfigListener, eventRecorder.Sender);
            Assert.AreSame(exception, eventRecorder.Args.Value);
        }

        [TestMethod]
        public void AutoConfigListener_ExceptionCaught_Does_Not_Pass_On_Exceptions_After_Disposal()
        {
            var eventRecorder = new EventRecorder<EventArgs<Exception>>();
            _AutoConfigListener.ExceptionCaught += eventRecorder.Handler;

            _AutoConfigListener.Initialise();
            _AutoConfigListener.Dispose();

            var exception = new InvalidOperationException();
            _Listener.Raise(r => r.ExceptionCaught += null, new EventArgs<Exception>(exception));

            Assert.AreEqual(0, eventRecorder.CallCount);
        }
        #endregion
    }
}
