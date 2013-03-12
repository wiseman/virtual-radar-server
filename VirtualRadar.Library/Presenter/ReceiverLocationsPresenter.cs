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
using VirtualRadar.Interface.Presenter;
using VirtualRadar.Interface.View;
using VirtualRadar.Localisation;
using VirtualRadar.Interface.Settings;
using InterfaceFactory;
using VirtualRadar.Interface.Database;

namespace VirtualRadar.Library.Presenter
{
    /// <summary>
    /// The default implementation of <see cref="IReceiverLocationsPresenter"/>.
    /// </summary>
    class ReceiverLocationsPresenter : IReceiverLocationsPresenter
    {
        /// <summary>
        /// The view that the presenter is controlling.
        /// </summary>
        private IReceiverLocationsView _View;

        /// <summary>
        /// When true the event handle for the ValueChanged event is not allowed to run.
        /// </summary>
        private bool _SuppressValueChangedEventHandler;

        /// <summary>
        /// The next unique ID to assign to a new location.
        /// </summary>
        private int _NextUniqueId = 1;

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="view"></param>
        public void Initialise(IReceiverLocationsView view)
        {
            _View = view;

            if(_View.ReceiverLocations.Count > 0) _NextUniqueId = _View.ReceiverLocations.Max(r => r.UniqueId) + 1;

            _View.ResetClicked += View_ResetClicked;
            _View.SelectedLocationChanged += View_SelectedLocationChanged;
            _View.ValueChanged += View_ValueChanged;
            _View.NewLocationClicked += View_NewLocationClicked;
            _View.DeleteLocationClicked += View_DeleteLocationClicked;
            _View.UpdateFromBaseStationDatabaseClicked += View_UpdateFromBaseStationDatabaseClicked;
            _View.CloseClicked += View_CloseClicked;
        }

        /// <summary>
        /// Copies the currently selected location to the fields.
        /// </summary>
        private void CopySelectedLocationToFields()
        {
            var currentSuppressSetting = _SuppressValueChangedEventHandler;
            try {
                _SuppressValueChangedEventHandler = true;

                var location = _View.SelectedReceiverLocation;

                _View.Location = location != null ? location.Name : "";
                _View.Latitude = location != null ? location.Latitude.ToString() : "";
                _View.Longitude = location != null ? location.Longitude.ToString() : "";

                _View.ShowValidationResults(new ValidationResult[] { });
            } finally {
                _SuppressValueChangedEventHandler = currentSuppressSetting;
            }
        }

        /// <summary>
        /// Copies the content of the edit fields to the selected location.
        /// </summary>
        private void CopyFieldsToSelectedLocation()
        {
            var location = _View.SelectedReceiverLocation;
            if(location != null) {
                location.Name = _View.Location;
                location.Latitude = ParseDouble(_View.Latitude).Value;
                location.Longitude = ParseDouble(_View.Longitude).Value;
                _View.RefreshSelectedLocation();
            }
        }

        /// <summary>
        /// Validates the name, latitude and longitude fields.
        /// </summary>
        /// <returns>True if the fields pass validation.</returns>
        private bool DoValidation()
        {
            var results = new List<ValidationResult>();

            var selectedLocation = _View.SelectedReceiverLocation;
            double? parsedLatitude = ParseDouble(_View.Latitude);
            double? parsedLongitude = ParseDouble(_View.Longitude);

            if(String.IsNullOrEmpty(_View.Location)) results.Add(new ValidationResult(ValidationField.Location, Strings.PleaseEnterNameForLocation));
            else if(_View.ReceiverLocations.Any(r => r != selectedLocation && r.Name == _View.Location)) results.Add(new ValidationResult(ValidationField.Location, Strings.PleaseEnterUniqueNameForLocation));

            if(String.IsNullOrEmpty(_View.Latitude)) results.Add(new ValidationResult(ValidationField.Latitude, Strings.LatitudeOutOfBounds));
            else if(parsedLatitude == null || parsedLatitude.Value < -90.0 || parsedLatitude.Value > 90.0) {
                results.Add(new ValidationResult(ValidationField.Latitude, Strings.LatitudeOutOfBounds));
            } else if(parsedLatitude == 0.0) {
                results.Add(new ValidationResult(ValidationField.Latitude, Strings.LatitudeCannotBeZero));
            }

            if(String.IsNullOrEmpty(_View.Longitude)) results.Add(new ValidationResult(ValidationField.Longitude, Strings.LongitudeOutOfBounds));
            else if(parsedLongitude == null || parsedLongitude.Value < -180.0 || parsedLongitude.Value > 180.0) {
                results.Add(new ValidationResult(ValidationField.Longitude, Strings.LongitudeOutOfBounds));
            }

            _View.ShowValidationResults(results);

            return results.Count == 0;
        }

        /// <summary>
        /// Parses the text into a double, returning null if unparseable.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private double? ParseDouble(string text)
        {
            double result;
            return double.TryParse(text, out result) ? result : (double?)null;
        }

        /// <summary>
        /// Raised when the Reset button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_ResetClicked(object sender, EventArgs args)
        {
            CopySelectedLocationToFields();
        }

        /// <summary>
        /// Raised when the selected location is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_SelectedLocationChanged(object sender, EventArgs args)
        {
            CopySelectedLocationToFields();
        }

        /// <summary>
        /// Raised when the location name, latitude or longitude changes on the view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_ValueChanged(object sender, EventArgs args)
        {
            if(!_SuppressValueChangedEventHandler) {
                var selectedLocation = _View.SelectedReceiverLocation;
                if(selectedLocation != null && DoValidation()) {
                    CopyFieldsToSelectedLocation();
                }
            }
        }

        /// <summary>
        /// Raised when the user closes the form and accepts the currently selected location.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_CloseClicked(object sender, EventArgs args)
        {
            var selectedLocation = _View.SelectedReceiverLocation;
            if(selectedLocation != null && DoValidation()) {
                CopyFieldsToSelectedLocation();
            }
        }

        /// <summary>
        /// Raised when the user asks for a new location to be created.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_NewLocationClicked(object sender, EventArgs args)
        {
            var location = new ReceiverLocation() {
                Name = SelectUniqueName("New Location"),
                UniqueId = _NextUniqueId++,
            };

            _View.ReceiverLocations.Add(location);
            _View.RefreshLocations();
            _View.SelectedReceiverLocation = location;

            CopySelectedLocationToFields();
            _View.FocusOnEditFields();
        }

        /// <summary>
        /// Returns a unique name for a new entry.
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        private string SelectUniqueName(string prefix)
        {
            string result = null;

            for(var nameSuffix = 0;nameSuffix < int.MaxValue;++nameSuffix) {
                var suffix = nameSuffix == 0 ? "" : String.Format("({0})", nameSuffix);
                var name = String.Format("{0}{1}", prefix, suffix);
                if(!_View.ReceiverLocations.Any(r => r.Name == name)) {
                    result = name;
                    break;
                }
            }
            if(result == null) throw new InvalidOperationException("Cannot determine a unique name for the location");

            return result;
        }

        /// <summary>
        /// Raised when the user wants to delete the selected location.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_DeleteLocationClicked(object sender, EventArgs args)
        {
            var deleteLocation = _View.SelectedReceiverLocation;
            if(deleteLocation != null) {
                _View.ReceiverLocations.Remove(deleteLocation);
                _View.RefreshLocations();
                _View.SelectedReceiverLocation = null;

                CopySelectedLocationToFields();
            }
        }

        /// <summary>
        /// Raised when the user wants to copy locations from the BaseStation database.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_UpdateFromBaseStationDatabaseClicked(object sender, EventArgs args)
        {
            var entriesPreviouslyCopiedFromBaseStationDatabase = _View.ReceiverLocations.Where(r => r.IsBaseStationLocation).ToList();
            _View.ReceiverLocations.RemoveAll(r => r.IsBaseStationLocation);

            var database = Factory.Singleton.Resolve<IAutoConfigBaseStationDatabase>().Singleton.Database;
            foreach(var location in database.GetLocations()) {
                var previousEntry = entriesPreviouslyCopiedFromBaseStationDatabase.Where(r => r.Name == location.LocationName).FirstOrDefault();
                var newLocation = new ReceiverLocation() {
                    Name = SelectUniqueName(location.LocationName),
                    UniqueId = previousEntry == null ? _NextUniqueId++ : previousEntry.UniqueId,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    IsBaseStationLocation = true,
                };
                _View.ReceiverLocations.Add(newLocation);
            }

            _View.RefreshLocations();
        }
    }
}
