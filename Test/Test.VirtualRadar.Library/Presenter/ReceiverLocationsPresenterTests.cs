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
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.View;
using Test.Framework;
using VirtualRadar.Localisation;
using VirtualRadar.Interface.Database;

namespace Test.VirtualRadar.Library.Presenter
{
    [TestClass]
    public class ReceiverLocationsPresenterTests
    {
        #region TestContext, Fields, TestInitialise, TestCleanup
        public TestContext TestContext { get; set; }

        private IClassFactory _OriginalClassFactory;
        private IReceiverLocationsPresenter _Presenter;
        private Mock<IReceiverLocationsView> _View;
        private Mock<IAutoConfigBaseStationDatabase> _AutoConfigBaseStationDatabase;
        private Mock<IBaseStationDatabase> _BaseStationDatabase;

        [TestInitialize]
        public void TestInitialise()
        {
            _OriginalClassFactory = Factory.TakeSnapshot();

            _AutoConfigBaseStationDatabase = TestUtilities.CreateMockSingleton<IAutoConfigBaseStationDatabase>();
            _BaseStationDatabase = new Mock<IBaseStationDatabase>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _AutoConfigBaseStationDatabase.Setup(r => r.Database).Returns(_BaseStationDatabase.Object);

            _Presenter = Factory.Singleton.Resolve<IReceiverLocationsPresenter>();
            _View = new Mock<IReceiverLocationsView>() { DefaultValue = DefaultValue.Mock} .SetupAllProperties();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Factory.RestoreSnapshot(_OriginalClassFactory);
        }
        #endregion

        #region Utility methods
        private ReceiverLocation SetupSelectedLocation()
        {
            var result = new ReceiverLocation() { UniqueId = 1, Name = "SELECTED", Latitude = 1, Longitude = 2 };
            return SetupSelectedLocation(result);
        }

        private ReceiverLocation SetupSelectedLocation(ReceiverLocation location)
        {
            _View.Setup(v => v.SelectedReceiverLocation).Returns(location);
            return location;
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

        #region SelectedLocationChanged, ResetClicked
        [TestMethod]
        public void ReceiverLocationsPresenter_SelectedLocationChanged_Copies_Selected_Location_To_Fields_And_Resets_Validation()
        {
            Check_Selected_Location_Copied_To_Fields_And_Resets_Validation(() => _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_SelectedLocationChanged_Copies_Empty_Values_To_Fields_If_No_Location_Is_Selected()
        {
            Check_Empty_Values_Copied_To_Fields_If_No_Location_Is_Selected(() => _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_SelectedLocationChanged_Suppresses_ValueChanged_Event_Handling_While_Values_Are_Copied()
        {
            Check_ValueChanged_Suppressed_During_Copy(() => _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ResetClicked_Copies_Selected_Location_To_Fields_And_Resets_Validation()
        {
            Check_Selected_Location_Copied_To_Fields_And_Resets_Validation(() => _View.Raise(v => v.ResetClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ResetClicked_Copies_Empty_Values_To_Fields_If_No_Location_Is_Selected()
        {
            Check_Empty_Values_Copied_To_Fields_If_No_Location_Is_Selected(() => _View.Raise(v => v.ResetClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ResetClicked_Suppresses_ValueChanged_Event_Handling_While_Values_Are_Copied()
        {
            Check_ValueChanged_Suppressed_During_Copy(() => _View.Raise(v => v.ResetClicked += null, EventArgs.Empty));
        }

        private void Check_Selected_Location_Copied_To_Fields_And_Resets_Validation(Action raiseEvent)
        {
            foreach(var culture in new string[] { "en-GB", "en-US", "de-DE", "fr-FR", "ru-RU" }) {
                TestCleanup();
                TestInitialise();

                using(var cultureSwitcher = new CultureSwitcher(culture)) {
                    _Presenter.Initialise(_View.Object);
                    var selectedLocation = SetupSelectedLocation(new ReceiverLocation() { Name = "A", Latitude = 1.123, Longitude = -2.678 });
                    SetupExpectedValidationFields(new ValidationResult[] { });

                    raiseEvent();

                    Assert.AreEqual("A", _View.Object.Location);
                    Assert.AreEqual(1.123, double.Parse(_View.Object.Latitude));
                    Assert.AreEqual(-2.678, double.Parse(_View.Object.Longitude));

                    _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
                }
            }
        }

        private void Check_Empty_Values_Copied_To_Fields_If_No_Location_Is_Selected(Action raiseEvent)
        {
            SetupSelectedLocation(null);
            _Presenter.Initialise(_View.Object);
            SetupExpectedValidationFields(new ValidationResult[] { });

            raiseEvent();

            Assert.AreEqual("", _View.Object.Location);
            Assert.AreEqual("", _View.Object.Latitude);
            Assert.AreEqual("", _View.Object.Longitude);
            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
        }

        private void Check_ValueChanged_Suppressed_During_Copy(Action raiseEvent)
        {
            _View.SetupSet(v => v.Location = It.IsAny<string>()).Raises(v => v.ValueChanged += null, EventArgs.Empty);
            _View.SetupSet(v => v.Latitude = It.IsAny<string>()).Raises(v => v.ValueChanged += null, EventArgs.Empty);
            _View.SetupSet(v => v.Longitude = It.IsAny<string>()).Raises(v => v.ValueChanged += null, EventArgs.Empty);

            _Presenter.Initialise(_View.Object);
            var selectedLocation = SetupSelectedLocation();
            SetupExpectedValidationFields(new ValidationResult[]{});

            raiseEvent();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
        }
        #endregion

        #region ValueChanged
        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Displays_Validation_Message_When_Any_Field_Is_Empty()
        {
            CheckValidation_Displays_Validation_Message_When_Any_Field_Is_Empty(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Displays_Validation_Message_When_Any_Field_Is_Empty(Action trigger)
        {
            for(var emptyFieldNumber = 0;emptyFieldNumber < 3;++emptyFieldNumber) {
                TestCleanup();
                TestInitialise();

                ValidationResult expectedValidationResult;
                switch(emptyFieldNumber) {
                    case 0:     expectedValidationResult = new ValidationResult(ValidationField.Location, Strings.PleaseEnterNameForLocation); break;
                    case 1:     expectedValidationResult = new ValidationResult(ValidationField.Latitude, Strings.LatitudeOutOfBounds); break;
                    case 2:     expectedValidationResult = new ValidationResult(ValidationField.Longitude, Strings.LongitudeOutOfBounds); break;
                    default:    throw new NotImplementedException();
                }

                _Presenter.Initialise(_View.Object);
                var selectedLocation = SetupSelectedLocation(new ReceiverLocation() { Name = "A", Latitude = 7, Longitude = 8 });
                SetupExpectedValidationFields(new ValidationResult[] { expectedValidationResult });

                _View.Object.Location = emptyFieldNumber == 0 ? "" : "ABC";
                _View.Object.Latitude = emptyFieldNumber == 1 ? "" : (1.0).ToString();
                _View.Object.Longitude = emptyFieldNumber == 2 ? "" : (2.0).ToString();

                trigger();

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Once());
                _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
                Assert.AreEqual("A", selectedLocation.Name);
                Assert.AreEqual(7.0, selectedLocation.Latitude);
                Assert.AreEqual(8.0, selectedLocation.Longitude);
            }
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Displays_Validation_Message_When_Name_Duplicates_Existing_Name()
        {
            CheckValidation_Displays_Validation_Message_When_Name_Duplicates_Existing_Name(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Displays_Validation_Message_When_Name_Duplicates_Existing_Name(Action trigger)
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC" };
            var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ" };
            _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
            SetupSelectedLocation(line2);
            _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.Location, Strings.PleaseEnterUniqueNameForLocation) });

            _View.Object.Location = "ABC";
            _View.Object.Latitude = (0.01).ToString();
            trigger();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
            Assert.AreEqual("XYZ", line2.Name);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Does_Not_Display_Validation_Message_When_Name_Duplicates_Own_Name()
        {
            CheckValidation_Does_Not_Display_Validation_Message_When_Name_Duplicates_Own_Name(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Does_Not_Display_Validation_Message_When_Name_Duplicates_Own_Name(Action trigger)
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC" };
            var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ" };
            _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
            SetupSelectedLocation(line2);
            _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { });

            _View.Object.Location = "XYZ";
            _View.Object.Latitude = (0.01).ToString();
            trigger();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Displays_Validation_Message_When_Latitude_Cannot_Be_Parsed()
        {
            CheckValidation_Displays_Validation_Message_When_Latitude_Cannot_Be_Parsed(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Displays_Validation_Message_When_Latitude_Cannot_Be_Parsed(Action trigger)
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
            var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
            _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
            SetupSelectedLocation(line2);
            _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.Latitude, Strings.LatitudeOutOfBounds) });

            _View.Object.Latitude = "Gibberish";
            trigger();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
            Assert.AreEqual(1.0, line2.Latitude);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Displays_Validation_Message_When_Longitude_Cannot_Be_Parsed()
        {
            CheckValidation_Displays_Validation_Message_When_Longitude_Cannot_Be_Parsed(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Displays_Validation_Message_When_Longitude_Cannot_Be_Parsed(Action trigger)
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
            var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
            _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
            SetupSelectedLocation(line2);
            _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.Longitude, Strings.LongitudeOutOfBounds) });

            _View.Object.Longitude = "Gibberish";
            trigger();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
            Assert.AreEqual(2.0, line2.Longitude);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Displays_Validation_Message_When_Latitude_Is_Out_Of_Range()
        {
            CheckValidation_Displays_Validation_Message_When_Latitude_Is_Out_Of_Range(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Displays_Validation_Message_When_Latitude_Is_Out_Of_Range(Action trigger)
        {
            foreach(var badLatitude in new double[] { -92.0, -91.0, -90.00001, 90.00001, 91.0, 92.0 }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
                var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
                _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
                SetupSelectedLocation(line2);
                _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

                SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.Latitude, Strings.LatitudeOutOfBounds) });

                _View.Object.Latitude = badLatitude.ToString();
                trigger();

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
                _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
                Assert.AreEqual(1.0, line2.Latitude);
            }
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Displays_Validation_Message_When_Latitude_Is_Zero()
        {
            CheckValidation_Displays_Validation_Message_When_Latitude_Is_Zero(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Displays_Validation_Message_When_Latitude_Is_Zero(Action trigger)
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
            var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
            _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
            SetupSelectedLocation(line2);
            _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.Latitude, Strings.LatitudeCannotBeZero) });

            _View.Object.Latitude = (0.0).ToString("N1");
            trigger();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
            Assert.AreEqual(1.0, line2.Latitude);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Displays_Validation_Message_When_Longitude_Is_Out_Of_Range()
        {
            CheckValidation_Displays_Validation_Message_When_Longitude_Is_Out_Of_Range(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Displays_Validation_Message_When_Longitude_Is_Out_Of_Range(Action trigger)
        {
            foreach(var badLongitude in new double[] { -182.0, -181.0, -180.00001, 180.00001, 181.0, 182.0 }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
                var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
                _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
                SetupSelectedLocation(line2);
                _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

                SetupExpectedValidationFields(new ValidationResult[] { new ValidationResult(ValidationField.Longitude, Strings.LongitudeOutOfBounds) });

                _View.Object.Longitude = badLongitude.ToString();
                trigger();

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
                _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
                Assert.AreEqual(2.0, line2.Longitude);
            }
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Updates_Selected_Line_With_Name()
        {
            CheckValidation_Updates_Selected_Line_With_Name(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Updates_Selected_Line_With_Name(Action trigger)
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
            var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
            _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
            SetupSelectedLocation(line2);
            _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

            SetupExpectedValidationFields(new ValidationResult[] { });

            _View.Object.Location = "New name";
            trigger();

            _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
            Assert.AreEqual("New name", line2.Name);
            _View.Verify(v => v.RefreshSelectedLocation(), Times.Once());
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Updates_Selected_Line_With_New_Latitude()
        {
            CheckValidation_Updates_Selected_Line_With_New_Latitude(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Updates_Selected_Line_With_New_Latitude(Action trigger)
        {
            foreach(var latitude in new double[] { -90.0, -89.9999, -88, -1, -0.01, 0.01, 1, 88, 89.9999, 90.0 }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
                var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
                _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
                SetupSelectedLocation(line2);
                _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

                SetupExpectedValidationFields(new ValidationResult[] { });

                _View.Object.Latitude = latitude.ToString();
                trigger();

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
                Assert.AreEqual(latitude, line2.Latitude);
                _View.Verify(v => v.RefreshSelectedLocation(), Times.Once());
            }
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Updates_Selected_Line_With_New_Longitude()
        {
            CheckValidation_Updates_Selected_Line_With_New_Longitude(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Updates_Selected_Line_With_New_Longitude(Action trigger)
        {
            foreach(var longitude in new double[] { -180.0, -179.9999, -178, -1, 0, 1, 178, 179.9999, 180.0 }) {
                TestCleanup();
                TestInitialise();

                _Presenter.Initialise(_View.Object);

                var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
                var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
                _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
                SetupSelectedLocation(line2);
                _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

                SetupExpectedValidationFields(new ValidationResult[] { });

                _View.Object.Longitude = longitude.ToString();
                trigger();

                _View.Verify(v => v.ShowValidationResults(It.IsAny<IEnumerable<ValidationResult>>()), Times.Exactly(2));
                Assert.AreEqual(longitude, line2.Longitude);
                _View.Verify(v => v.RefreshSelectedLocation(), Times.Once());
            }
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_ValueChanged_Does_Nothing_If_No_Location_Is_Selected()
        {
            CheckValidation_Does_Nothing_If_No_Location_Is_Selected(() => _View.Raise(v => v.ValueChanged += null, EventArgs.Empty));
        }

        private void CheckValidation_Does_Nothing_If_No_Location_Is_Selected(Action trigger)
        {
            _Presenter.Initialise(_View.Object);

            var line1 = new ReceiverLocation() { UniqueId = 1, Name = "ABC", Latitude = 1.0, Longitude = 2.0 };
            var line2 = new ReceiverLocation() { UniqueId = 2, Name = "XYZ", Latitude = 1.0, Longitude = 2.0 };
            _View.Object.ReceiverLocations.AddRange(new ReceiverLocation[] { line1, line2 });
            SetupSelectedLocation(null);
            _View.Raise(v => v.SelectedLocationChanged += null, EventArgs.Empty);

            _View.Object.Location = "New name";
            _View.Object.Latitude = (4.0).ToString();
            _View.Object.Longitude = (5.0).ToString();

            trigger();

            _View.Verify(v => v.RefreshSelectedLocation(), Times.Never());
        }
        #endregion

        #region NewLocationClicked
        [TestMethod]
        public void ReceiverLocationsPresenter_NewLocationClicked_Adds_New_Location_And_Selects_It()
        {
            _Presenter.Initialise(_View.Object);

            _View.Raise(v => v.NewLocationClicked += null, EventArgs.Empty);

            Assert.AreEqual(1, _View.Object.ReceiverLocations.Count);
            var location = _View.Object.ReceiverLocations[0];
            Assert.AreEqual("New Location", location.Name);
            Assert.AreEqual(false, location.IsBaseStationLocation);
            Assert.AreEqual(1, location.UniqueId);
            Assert.AreEqual(0.0, location.Latitude);
            Assert.AreEqual(0.0, location.Longitude);

            _View.Verify(v => v.RefreshLocations(), Times.Once());
            _View.Verify(v => v.FocusOnEditFields(), Times.Once());

            Assert.AreSame(location, _View.Object.SelectedReceiverLocation);

            Assert.AreEqual("New Location", _View.Object.Location);
            Assert.AreEqual((0.0).ToString(), _View.Object.Latitude);
            Assert.AreEqual((0.0).ToString(), _View.Object.Longitude);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_NewLocationClicked_Chooses_A_Name_That_Is_Not_In_Use()
        {
            _Presenter.Initialise(_View.Object);
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { Name = "New Location" });

            _View.Raise(v => v.NewLocationClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.ReceiverLocations.Count);
            Assert.AreEqual("New Location(1)", _View.Object.Location);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_NewLocationClicked_Chooses_A_Unique_Id_That_Is_Not_In_Use()
        {
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 1 });
            _Presenter.Initialise(_View.Object);

            _View.Raise(v => v.NewLocationClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.SelectedReceiverLocation.UniqueId);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_NewLocationClicked_Chooses_A_Unique_Id_Higher_Than_Any_Used_By_Deleted_Rows()
        {
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 1 });
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 2 });

            _Presenter.Initialise(_View.Object);
            _View.Object.ReceiverLocations.Clear();
            _View.Raise(v => v.NewLocationClicked += null, EventArgs.Empty);

            Assert.AreEqual(1, _View.Object.ReceiverLocations.Count);
            Assert.IsTrue(_View.Object.ReceiverLocations[0].UniqueId > 2);
        }
        #endregion

        #region DeleteLocationClicked
        [TestMethod]
        public void ReceiverLocationsPresenter_DeleteLocationClicked_Deletes_Selected_Row()
        {
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 1, Name = "A" });
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 2, Name = "B" });
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 3, Name = "C" });

            _Presenter.Initialise(_View.Object);
            _View.Object.SelectedReceiverLocation = _View.Object.ReceiverLocations[1];
            _View.Raise(v => v.DeleteLocationClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.ReceiverLocations.Count);
            Assert.IsFalse(_View.Object.ReceiverLocations.Any(r => r.Name == "B"));
            _View.Verify(v => v.RefreshLocations(), Times.Once());
            Assert.IsNull(_View.Object.SelectedReceiverLocation);
            Assert.AreEqual("", _View.Object.Location);
            Assert.AreEqual("", _View.Object.Latitude);
            Assert.AreEqual("", _View.Object.Longitude);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_DeleteLocationCLicked_Does_Nothing_If_No_Row_Selected()
        {
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 1, Name = "A" });
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 2, Name = "B" });
            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { UniqueId = 3, Name = "C" });

            _Presenter.Initialise(_View.Object);
            _View.Object.SelectedReceiverLocation = null;
            _View.Raise(v => v.DeleteLocationClicked += null, EventArgs.Empty);

            Assert.AreEqual(3, _View.Object.ReceiverLocations.Count);
            Assert.IsTrue(_View.Object.ReceiverLocations.Any(r => r.Name == "B"));
        }
        #endregion

        #region UpdateFromBaseStationDatabaseClicked
        [TestMethod]
        public void ReceiverLocationsPresenter_UpdateFromBaseStationDatabaseClicked_Adds_Entries_From_BaseStation_Database()
        {
            var location1 = new BaseStationLocation() { LocationName = "A", Latitude = 1.2, Longitude = 3.4 };
            var location2 = new BaseStationLocation() { LocationName = "B", Latitude = 5.6, Longitude = 7.8 };
            _BaseStationDatabase.Setup(r => r.GetLocations()).Returns(new BaseStationLocation[] { location1, location2 });

            _Presenter.Initialise(_View.Object);
            _View.Raise(v => v.UpdateFromBaseStationDatabaseClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.ReceiverLocations.Count);
            var locationA = _View.Object.ReceiverLocations.Where(r => r.Name == "A").Single();
            var locationB = _View.Object.ReceiverLocations.Where(r => r.Name == "B").Single();
            Assert.AreEqual(1.2, locationA.Latitude);
            Assert.AreEqual(3.4, locationA.Longitude);
            Assert.AreEqual(5.6, locationB.Latitude);
            Assert.AreEqual(7.8, locationB.Longitude);
            Assert.IsTrue(locationA.IsBaseStationLocation);
            Assert.IsTrue(locationB.IsBaseStationLocation);
            Assert.AreEqual(1, locationA.UniqueId);
            Assert.AreEqual(2, locationB.UniqueId);

            _View.Verify(v => v.RefreshLocations());
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_UpdateFromBaseStationDatabaseClicked_Reuses_UniqueId_If_Name_Previously_Read()
        {
            var location1 = new BaseStationLocation() { LocationName = "A", Latitude = 1.2, Longitude = 3.4 };
            _BaseStationDatabase.Setup(r => r.GetLocations()).Returns(new BaseStationLocation[] { location1, });

            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { Name = "A", IsBaseStationLocation = true, UniqueId = 12 });
            _Presenter.Initialise(_View.Object);
            _View.Raise(v => v.UpdateFromBaseStationDatabaseClicked += null, EventArgs.Empty);

            Assert.AreEqual(1, _View.Object.ReceiverLocations.Count);
            var locationA = _View.Object.ReceiverLocations[0];
            Assert.AreEqual("A", locationA.Name);
            Assert.AreEqual(1.2, locationA.Latitude);
            Assert.AreEqual(3.4, locationA.Longitude);
            Assert.AreEqual(true, locationA.IsBaseStationLocation);
            Assert.AreEqual(12, locationA.UniqueId);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_UpdateFromBaseStationDatabaseClicked_Qualifies_Name_If_Non_BaseStationDatabase_Entry_With_Same_Name_Alredy_On_View()
        {
            var location1 = new BaseStationLocation() { LocationName = "A", Latitude = 1.2, Longitude = 3.4 };
            _BaseStationDatabase.Setup(r => r.GetLocations()).Returns(new BaseStationLocation[] { location1, });

            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { Name = "A", IsBaseStationLocation = false, UniqueId = 12 });
            _Presenter.Initialise(_View.Object);
            _View.Raise(v => v.UpdateFromBaseStationDatabaseClicked += null, EventArgs.Empty);

            Assert.AreEqual(2, _View.Object.ReceiverLocations.Count);
            var locationA = _View.Object.ReceiverLocations.Where(r => r.Name == "A").Single();
            var locationA1 = _View.Object.ReceiverLocations.Where(r => r.Name == "A(1)").Single();

            Assert.AreEqual(0.0, locationA.Latitude);
            Assert.AreEqual(0.0, locationA.Longitude);
            Assert.AreEqual(false, locationA.IsBaseStationLocation);
            Assert.AreEqual(12, locationA.UniqueId);

            Assert.AreEqual("A(1)", locationA1.Name);
            Assert.AreEqual(1.2, locationA1.Latitude);
            Assert.AreEqual(3.4, locationA1.Longitude);
            Assert.AreEqual(true, locationA1.IsBaseStationLocation);
            Assert.AreEqual(13, locationA1.UniqueId);
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_UpdateFromBaseStationDatabaseClicked_Removes_Previous_BaseStationDatabase_Reads_If_No_Longer_In_BaseStation()
        {
            var location1 = new BaseStationLocation() { LocationName = "A", Latitude = 1.2, Longitude = 3.4 };
            _BaseStationDatabase.Setup(r => r.GetLocations()).Returns(new BaseStationLocation[] { location1, });

            _View.Object.ReceiverLocations.Add(new ReceiverLocation() { Name = "B", IsBaseStationLocation = true, UniqueId = 12 });
            _Presenter.Initialise(_View.Object);
            _View.Raise(v => v.UpdateFromBaseStationDatabaseClicked += null, EventArgs.Empty);

            Assert.AreEqual(1, _View.Object.ReceiverLocations.Count);
            Assert.AreEqual("A", _View.Object.ReceiverLocations[0].Name);
        }
        #endregion

        #region CloseClicked
        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Displays_Validation_Message_When_Any_Field_Is_Empty()
        {
            CheckValidation_Displays_Validation_Message_When_Any_Field_Is_Empty(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Displays_Validation_Message_When_Name_Duplicates_Existing_Name()
        {
            CheckValidation_Displays_Validation_Message_When_Name_Duplicates_Existing_Name(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Does_Not_Display_Validation_Message_When_Name_Duplicates_Own_Name()
        {
            CheckValidation_Does_Not_Display_Validation_Message_When_Name_Duplicates_Own_Name(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Displays_Validation_Message_When_Latitude_Cannot_Be_Parsed()
        {
            CheckValidation_Displays_Validation_Message_When_Latitude_Cannot_Be_Parsed(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Displays_Validation_Message_When_Longitude_Cannot_Be_Parsed()
        {
            CheckValidation_Displays_Validation_Message_When_Longitude_Cannot_Be_Parsed(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Displays_Validation_Message_When_Latitude_Is_Out_Of_Range()
        {
            CheckValidation_Displays_Validation_Message_When_Latitude_Is_Out_Of_Range(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Displays_Validation_Message_When_Latitude_Is_Zero()
        {
            CheckValidation_Displays_Validation_Message_When_Latitude_Is_Zero(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Displays_Validation_Message_When_Longitude_Is_Out_Of_Range()
        {
            CheckValidation_Displays_Validation_Message_When_Longitude_Is_Out_Of_Range(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Updates_Selected_Line_With_Name()
        {
            CheckValidation_Updates_Selected_Line_With_Name(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Updates_Selected_Line_With_New_Latitude()
        {
            CheckValidation_Updates_Selected_Line_With_New_Latitude(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Updates_Selected_Line_With_New_Longitude()
        {
            CheckValidation_Updates_Selected_Line_With_New_Longitude(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }

        [TestMethod]
        public void ReceiverLocationsPresenter_CloseClicked_Does_Nothing_If_No_Location_Is_Selected()
        {
            CheckValidation_Does_Nothing_If_No_Location_Is_Selected(() => _View.Raise(v => v.CloseClicked += null, EventArgs.Empty));
        }
        #endregion
    }
}
