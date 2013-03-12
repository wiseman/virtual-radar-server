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
using VirtualRadar.Interface.Settings;
using VirtualRadar.Localisation;

namespace VirtualRadar.Library.Presenter
{
    /// <summary>
    /// The default implementation of <see cref="IRebroadcastOptionsPresenter"/>.
    /// </summary>
    class RebroadcastOptionsPresenter : IRebroadcastOptionsPresenter
    {
        /// <summary>
        /// The view that the presenter is controlling.
        /// </summary>
        private IRebroadcastOptionsView _View;

        /// <summary>
        /// The default port number to offer to the user.
        /// </summary>
        private const int DefaultPort = 33001;

        /// <summary>
        /// When true the event handle for the ValueChanged event is not allowed to run.
        /// </summary>
        private bool _SuppressValueChangedEventHandler;

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="view"></param>
        public void Initialise(IRebroadcastOptionsView view)
        {
            _View = view;

            _View.ResetClicked += View_ResetClicked;
            _View.SelectedServerChanged += View_SelectedServerChanged;
            _View.ValueChanged += View_ValueChanged;
            _View.NewServerClicked += View_NewServerClicked;
            _View.DeleteServerClicked += View_DeleteServerClicked;

            if(_View.RebroadcastSettings.Count > 0) _View.SelectedRebroadcastSettings = _View.RebroadcastSettings[0];
        }

        /// <summary>
        /// Copies the selected server to the edit fields.
        /// </summary>
        private void CopySelectedServerToFields()
        {
            var currentSuppressSetting = _SuppressValueChangedEventHandler;
            try {
                _SuppressValueChangedEventHandler = true;

                var server = _View.SelectedRebroadcastSettings;
                _View.ServerEnabled = server != null ? server.Enabled : false;
                _View.ServerFormat = server != null ? server.Format : RebroadcastFormat.Passthrough;
                _View.ServerName = server != null ? server.Name : "";
                _View.ServerPort = server != null ? server.Port : DefaultPort;

                _View.ShowValidationResults(new ValidationResult[] { });
            } finally {
                _SuppressValueChangedEventHandler = currentSuppressSetting;
            }
        }


        /// <summary>
        /// Validates the edit fields.
        /// </summary>
        /// <returns>True if the fields pass validation.</returns>
        private bool DoValidation()
        {
            var results = new List<ValidationResult>();

            var selectedServer = _View.SelectedRebroadcastSettings;

            if(String.IsNullOrEmpty(_View.ServerName)) results.Add(new ValidationResult(ValidationField.Name, Strings.NameRequired));
            else if(_View.RebroadcastSettings.Any(r => r != selectedServer && r.Name == _View.ServerName)) results.Add(new ValidationResult(ValidationField.Name, Strings.NameMustBeUnique));

            if(_View.ServerFormat == RebroadcastFormat.None) results.Add(new ValidationResult(ValidationField.Format, Strings.RebroadcastFormatRequired));

            if(_View.RebroadcastSettings.Any(r => r != selectedServer && r.Port == _View.ServerPort)) results.Add(new ValidationResult(ValidationField.BaseStationPort, Strings.PortMustBeUnique));

            _View.ShowValidationResults(results);

            return results.Count == 0;
        }

        /// <summary>
        /// Raised when the Reset button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_ResetClicked(object sender, EventArgs args)
        {
            CopySelectedServerToFields();
        }

        /// <summary>
        /// Called when the view indicates that the selected server has changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_SelectedServerChanged(object sender, EventArgs args)
        {
            CopySelectedServerToFields();
        }

        /// <summary>
        /// Raised when the edit fields are changed on the view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_ValueChanged(object sender, EventArgs args)
        {
            if(!_SuppressValueChangedEventHandler) {
                var selectedServer = _View.SelectedRebroadcastSettings;
                if(selectedServer != null && DoValidation()) {
                    selectedServer.Enabled = _View.ServerEnabled;
                    selectedServer.Name = _View.ServerName;
                    selectedServer.Format = _View.ServerFormat;
                    selectedServer.Port = _View.ServerPort;
                    _View.RefreshSelectedServer();
                }
            }
        }

        /// <summary>
        /// Raised when the user asks for a new server to be created.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_NewServerClicked(object sender, EventArgs args)
        {
            var server = new RebroadcastSettings() {
                Enabled = true,
                Name = SelectUniqueName("New Server"),
                Format = RebroadcastFormat.Passthrough,
                Port = SelectUniquePort(DefaultPort),
            };

            _View.RebroadcastSettings.Add(server);
            _View.RefreshServers();
            _View.SelectedRebroadcastSettings = server;

            CopySelectedServerToFields();
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
                if(!_View.RebroadcastSettings.Any(r => r.Name == name)) {
                    result = name;
                    break;
                }
            }
            if(result == null) throw new InvalidOperationException("Cannot determine a unique name for the server");

            return result;
        }

        /// <summary>
        /// Returns a unique port for a new entry.
        /// </summary>
        /// <param name="firstPort"></param>
        /// <returns></returns>
        private int SelectUniquePort(int firstPort)
        {
            int result = -1;
            for(var port = firstPort;port < 65536;++port) {
                if(!_View.RebroadcastSettings.Any(r => r.Port == port)) {
                    result = port;
                    break;
                }
            }
            if(result == -1) throw new InvalidOperationException("Cannot determine a unique port for the server");

            return result;
        }

        /// <summary>
        /// Raised when the user wants to delete the selected server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void View_DeleteServerClicked(object sender, EventArgs args)
        {
            var deleteServer = _View.SelectedRebroadcastSettings;
            if(deleteServer != null) {
                _View.RebroadcastSettings.Remove(deleteServer);
                _View.RefreshServers();
                _View.SelectedRebroadcastSettings = null;

                CopySelectedServerToFields();
            }
        }
    }
}
