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
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using InterfaceFactory;
using VirtualRadar.Interface.Presenter;
using Moq;
using VirtualRadar.Interface.View;
using VirtualRadar.Interface.Settings;
using Test.Framework;
using VirtualRadar.Localisation;

namespace Test.VirtualRadar.Library.Presenter
{
    [TestClass]
    public class RebroadcastOptionsPresenterTests
    {
        #region TestContext, Fields, TestInitialise, TestCleanup
        public TestContext TestContext { get; set; }

        private IRebroadcastOptionsPresenter _Presenter;
        private Mock<IRebroadcastOptionsView> _View;

        [TestInitialize]
        public void TestInitialise()
        {
            _Presenter = Factory.Singleton.Resolve<IRebroadcastOptionsPresenter>();
            _View = new Mock<IRebroadcastOptionsView>() { DefaultValue = DefaultValue.Mock} .SetupAllProperties();
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }
        #endregion

        #region Utility methods
        private RebroadcastSettings SetupSelectedRebroadcastSettings()
        {
            var result = new RebroadcastSettings() { Enabled = true, Name = "Server", Port = 12001, Format = RebroadcastFormat.Port30003 };
            return SetupSelectedRebroadcastSettings(result);
        }

        private RebroadcastSettings SetupSelectedRebroadcastSettings(RebroadcastSettings settings)
        {
            _View.Setup(v => v.SelectedRebroadcastSettings).Returns(settings);
            return settings;
        }

        private void SetupExpectedValidationFields(IEnumerable<ValidationResult> expectedResults)
        {
            _View.Setup(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>())).Callback((IEnumerable<ValidationResult> r) => {
                Assert.AreEqual(expectedResults.Count(), r.Count());
                foreach(var expectedResult in expectedResults) {
                    var matchingResult = r.Where(i => i.Field == expectedResult.Field).Single();
                    Assert.AreEqual(expectedResult.IsWarning, matchingResult.IsWarning);
                    Assert.AreEqual(expectedResult.Message, matchingResult.Message);
                }
            });
        }
        #endregion

        #region Initialise
        [TestMethod]
        public void RebroadcastOptionsPresenter_Initialise_Selects_First_RebroadcastSettings()
        {
            var rebroadcastSettings = new RebroadcastSettings();
            _View.Object.RebroadcastSettings.Add(rebroadcastSettings);

            _Presenter.Initialise(_View.Object);

            Assert.AreSame(_View.Object.SelectedRebroadcastSettings, rebroadcastSettings);
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_Initialise_Does_Not_Select_First_RebroadcastSettings_If_There_Are_None()
        {
            _View.Object.SelectedRebroadcastSettings = null;
            _Presenter.Initialise(_View.Object);

            Assert.IsNull(_View.Object.SelectedRebroadcastSettings);
        }
        #endregion

        #region SelectedServerChanged, ResetClicked
        [TestMethod]
        public void RebroadcastOptionsPresenter_SelectedServerChanged_Copies_Selected_Location_To_Fields_And_Resets_Validation()
        {
            Check_Selected_Server_Copied_To_Fields_And_Resets_Validation(() => _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty));
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_SelectedServerChanged_Copies_Empty_Values_To_Fields_If_No_Server_Is_Selected()
        {
            Check_Empty_Values_Copied_To_Fields_If_No_Server_Is_Selected(() => _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty));
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_SelectedServerChanged_Suppresses_ValueChanged_Event_Handling_While_Values_Are_Copied()
        {
            Check_ValueChanged_Suppressed_During_Copy(() => _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty));
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ResetClicked_Copies_Selected_Server_To_Fields_And_Resets_Validation()
        {
            Check_Selected_Server_Copied_To_Fields_And_Resets_Validation(() => _View.Raise(v => v.ResetClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ResetClicked_Copies_Empty_Values_To_Fields_If_No_Server_Is_Selected()
        {
            Check_Empty_Values_Copied_To_Fields_If_No_Server_Is_Selected(() => _View.Raise(v => v.ResetClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ResetClicked_Suppresses_ValueChanged_Event_Handling_While_Values_Are_Copied()
        {
            Check_ValueChanged_Suppressed_During_Copy(() => _View.Raise(v => v.ResetClicked += null, EventArgs.Empty));
        }

        private void Check_Selected_Server_Copied_To_Fields_And_Resets_Validation(Action raiseEvent)
        {
            foreach(var culture in new string[] { "en-GB", "en-US", "de-DE", "fr-FR", "ru-RU" }) {
                TestCleanup();
                TestInitialise();

                using(var cultureSwitcher = new CultureSwitcher(culture)) {
                    _Presenter.Initialise(_View.Object);
                    var selectedLocation = SetupSelectedRebroadcastSettings(new RebroadcastSettings() { Enabled = true, Format = RebroadcastFormat.Port30003, Name = "AW", Port = 8000 });
                    SetupExpectedValidationFields(new ValidationResult[] { });

                    raiseEvent();

                    Assert.AreEqual(true, _View.Object.ServerEnabled);
                    Assert.AreEqual(RebroadcastFormat.Port30003, _View.Object.ServerFormat);
                    Assert.AreEqual("AW", _View.Object.ServerName);
                    Assert.AreEqual(8000, _View.Object.ServerPort);

                    _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
                }
            }
        }

        private void Check_Empty_Values_Copied_To_Fields_If_No_Server_Is_Selected(Action raiseEvent)
        {
            SetupSelectedRebroadcastSettings(null);
            _Presenter.Initialise(_View.Object);
            SetupExpectedValidationFields(new ValidationResult[] { });

            raiseEvent();

            Assert.AreEqual(false, _View.Object.ServerEnabled);
            Assert.AreEqual(RebroadcastFormat.Passthrough, _View.Object.ServerFormat);
            Assert.AreEqual("", _View.Object.ServerName);
            Assert.AreEqual(33001, _View.Object.ServerPort);

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
        }

        private void Check_ValueChanged_Suppressed_During_Copy(Action raiseEvent)
        {
            _View.SetupSet(v => v.ServerEnabled = It.IsAny<bool>()).Raises(v => v.ValueChanged += null, EventArgs.Empty);
            _View.SetupSet(v => v.ServerFormat = It.IsAny<RebroadcastFormat>()).Raises(v => v.ValueChanged += null, EventArgs.Empty);
            _View.SetupSet(v => v.ServerName = It.IsAny<string>()).Raises(v => v.ValueChanged += null, EventArgs.Empty);
            _View.SetupSet(v => v.ServerPort = It.IsAny<int>()).Raises(v => v.ValueChanged += null, EventArgs.Empty);

            _Presenter.Initialise(_View.Object);
            var selectedServer = SetupSelectedRebroadcastSettings();
            SetupExpectedValidationFields(new ValidationResult[]{});

            raiseEvent();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
        }
        #endregion

        #region ValueChanged
        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Displays_Validation_Message_When_Any_Field_Is_Empty()
        {
            for(var emptyFieldNumber = 0;emptyFieldNumber < 2;++emptyFieldNumber) {
                TestCleanup();
                TestInitialise();

                ValidationResult expectedValidationResult;
                switch(emptyFieldNumber) {
                    case 0:     expectedValidationResult = new ValidationResult(ValidationField.Name, Strings.NameRequired); break;
                    case 1:     expectedValidationResult = new ValidationResult(ValidationField.Format, Strings.RebroadcastFormatRequired); break;
                    default:    throw new NotImplementedException();
                }

                _Presenter.Initialise(_View.Object);
                var selectedServer = SetupSelectedRebroadcastSettings(new RebroadcastSettings() { Enabled = true, Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 100 });
                SetupExpectedValidationFields(new ValidationResult[] { expectedValidationResult });

                _View.Object.ServerName = emptyFieldNumber == 0 ? "" : "XYZ";
                _View.Object.ServerFormat = emptyFieldNumber == 1 ? RebroadcastFormat.None : RebroadcastFormat.Passthrough;

                _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
                _View.Verify(v => v.RefreshSelectedServer(), Times.Never());
                Assert.AreEqual(true, selectedServer.Enabled);
                Assert.AreEqual("ABC", selectedServer.Name);
                Assert.AreEqual(RebroadcastFormat.Port30003, selectedServer.Format);
                Assert.AreEqual(100, selectedServer.Port);
            }
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Displays_Validation_Message_When_Name_Duplicates_Existing_Name()
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001 };
            var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002 };
            _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
            SetupSelectedRebroadcastSettings(line2);
            _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.Name, Strings.NameMustBeUnique) });

            _View.Object.ServerName = "ABC";
            _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            _View.Verify(v => v.RefreshSelectedServer(), Times.Never());
            Assert.AreEqual("XYZ", line2.Name);
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Does_Not_Display_Validation_Message_When_Name_Duplicates_Own_Name()
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001 };
            var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002 };
            _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
            SetupSelectedRebroadcastSettings(line2);
            _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { });

            _View.Object.ServerName = "XYZ";
            _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Displays_Validation_Message_When_Port_Duplicates_Existing_Port()
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001, Enabled = true };
            var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002, Enabled = true };
            _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
            SetupSelectedRebroadcastSettings(line2);
            _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.BaseStationPort, Strings.PortMustBeUnique) });

            _View.Object.ServerName = "XYZ";
            _View.Object.ServerPort = 10001;
            _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            _View.Verify(v => v.RefreshSelectedServer(), Times.Never());
            Assert.AreEqual(10002, line2.Port);
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Does_Not_Display_Validation_Message_When_Port_Duplicates_Own_Port()
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001, Enabled = true };
            var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002, Enabled = true };
            _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
            SetupSelectedRebroadcastSettings(line2);
            _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { });

            _View.Object.ServerName = "XYZ";
            _View.Object.ServerPort = 10002;
            _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Updates_Selected_Line_With_Name()
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001, Enabled = true };
            var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002, Enabled = true };
            _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
            SetupSelectedRebroadcastSettings(line2);
            _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { });

            _View.Object.ServerName = "New name";
            _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            Assert.AreEqual("New name", line2.Name);
            _View.Verify(v => v.RefreshSelectedServer(), Times.Once());
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Updates_Selected_Line_With_New_Enabled_Value()
        {
            foreach(var enabled in new bool[] { true, false }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001, Enabled = true };
                var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002, Enabled = !enabled };
                _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
                SetupSelectedRebroadcastSettings(line2);
                _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

                SetupExpectedValidationFields(new ValidationResult[] { });

                _View.Object.ServerEnabled = enabled;
                _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
                Assert.AreEqual(enabled, line2.Enabled);
                _View.Verify(v => v.RefreshSelectedServer(), Times.Once());
            }
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Updates_Selected_Line_With_New_Format()
        {
            foreach(var format in new RebroadcastFormat[] { RebroadcastFormat.Passthrough, RebroadcastFormat.Port30003 }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001, Enabled = true };
                var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.None, Port = 10002, Enabled = true };
                _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
                SetupSelectedRebroadcastSettings(line2);
                _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

                SetupExpectedValidationFields(new ValidationResult[] { });

                _View.Object.ServerFormat = format;
                _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
                Assert.AreEqual(format, line2.Format);
                _View.Verify(v => v.RefreshSelectedServer(), Times.Once());
            }
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Updates_Selected_Line_With_Port()
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001, Enabled = true };
            var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002, Enabled = true };
            _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
            SetupSelectedRebroadcastSettings(line2);
            _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { });

            _View.Object.ServerPort = 8080;
            _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            Assert.AreEqual(8080, line2.Port);
            _View.Verify(v => v.RefreshSelectedServer(), Times.Once());
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_ValueChanged_Does_Nothing_If_No_Server_Is_Selected()
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new RebroadcastSettings() { Name = "ABC", Format = RebroadcastFormat.Port30003, Port = 10001, Enabled = true };
            var line2 = new RebroadcastSettings() { Name = "XYZ", Format = RebroadcastFormat.Port30003, Port = 10002, Enabled = true };
            _View.Object.RebroadcastSettings.AddRange(new RebroadcastSettings[] { line1, line2 });
            SetupSelectedRebroadcastSettings(null);
            _View.Raise(v => v.SelectedServerChanged += null, EventArgs.Empty);

            _View.Object.ServerEnabled = false;
            _View.Object.ServerName = "New";
            _View.Object.ServerFormat = RebroadcastFormat.Passthrough;
            _View.Object.ServerPort = 8080;

            _View.Raise(v => v.ValueChanged += null, EventArgs.Empty);

            _View.Verify(v => v.RefreshSelectedServer(), Times.Never());
        }
        #endregion

        #region NewServerClicked
        [TestMethod]
        public void RebroadcastOptionsPresenter_NewServerClicked_Adds_New_Server_And_Selects_It()
        {
            _Presenter.Initialise(_View.Object);

            _View.Raise(v => v.NewServerClicked += null, EventArgs.Empty);

            Assert.AreEqual(1, _View.Object.RebroadcastSettings.Count);
            var location = _View.Object.RebroadcastSettings[0];
            Assert.AreEqual(true, location.Enabled);
            Assert.AreEqual("New Server", location.Name);
            Assert.AreEqual(RebroadcastFormat.Passthrough, location.Format);
            Assert.AreEqual(33001, location.Port);

            _View.Verify(v => v.RefreshServers(), Times.Once());
            _View.Verify(v => v.FocusOnEditFields(), Times.Once());

            Assert.AreSame(location, _View.Object.SelectedRebroadcastSettings);

            Assert.AreEqual(true, _View.Object.ServerEnabled);
            Assert.AreEqual("New Server", _View.Object.ServerName);
            Assert.AreEqual(RebroadcastFormat.Passthrough, _View.Object.ServerFormat);
            Assert.AreEqual(33001, _View.Object.ServerPort);
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_NewServerClicked_Chooses_A_Name_That_Is_Not_In_Use()
        {
            _Presenter.Initialise(_View.Object);
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Name = "New Server" });

            _View.Raise(v => v.NewServerClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.RebroadcastSettings.Count);
            Assert.AreEqual("New Server(1)", _View.Object.ServerName);
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_NewServerClicked_Chooses_A_Port_That_Is_Not_In_Use()
        {
            _Presenter.Initialise(_View.Object);
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Port = 33001 });

            _View.Raise(v => v.NewServerClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.RebroadcastSettings.Count);
            Assert.AreEqual(33002, _View.Object.ServerPort);
        }
        #endregion

        #region DeleteServerClicked
        [TestMethod]
        public void RebroadcastOptionsPresenter_DeleteLocationClicked_Deletes_Selected_Row()
        {
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Name = "A" });
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Name = "B" });
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Name = "C" });

            _Presenter.Initialise(_View.Object);
            _View.Object.SelectedRebroadcastSettings = _View.Object.RebroadcastSettings[1];
            _View.Raise(v => v.DeleteServerClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.RebroadcastSettings.Count);
            Assert.IsFalse(_View.Object.RebroadcastSettings.Any(r => r.Name == "B"));
            _View.Verify(v => v.RefreshServers(), Times.Once());
            Assert.IsNull(_View.Object.SelectedRebroadcastSettings);
            Assert.AreEqual("", _View.Object.ServerName);
        }

        [TestMethod]
        public void RebroadcastOptionsPresenter_DeleteLocationClicked_Does_Nothing_If_No_Row_Selected()
        {
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Name = "A" });
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Name = "B" });
            _View.Object.RebroadcastSettings.Add(new RebroadcastSettings() { Name = "C" });

            _Presenter.Initialise(_View.Object);
            _View.Object.SelectedRebroadcastSettings = null;
            _View.Raise(v => v.DeleteServerClicked += null, EventArgs.Empty);

            Assert.AreEqual(3, _View.Object.RebroadcastSettings.Count);
            _View.Verify(v => v.RefreshServers(), Times.Never());
        }
        #endregion
    }
}
