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
using VirtualRadar.Interface;
using InterfaceFactory;
using System.IO;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.WebServer;
using System.Windows.Forms;

namespace VirtualRadar.Plugin.WebRequestLogger
{
    /// <summary>
    /// The plugin object that Virtual Radar Server uses to communicate with the library.
    /// </summary>
    public class Plugin : IPlugin
    {
        /// <summary>
        /// The object that serialises access to the log file.
        /// </summary>
        private object _SyncLock = new object();

        /// <summary>
        /// The key for the setting that enables or disables the plugin.
        /// </summary>
        private const string EnabledSettingsKey = "Enabled";

        /// <summary>
        /// True if the plugin is enabled, false otherwise.
        /// </summary>
        private bool _Enabled;

        /// <summary>
        /// The name of the log file.
        /// </summary>
        private string _FileName;

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Id { get { return "VirtualRadar.Plugin.WebRequestLogger"; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Name { get { return "Web Request Logger"; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Version { get { return "1.2.0"; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Status { get; private set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string StatusDescription { get; private set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool HasOptions { get { return true; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler StatusChanged;

        /// <summary>
        /// Raises <see cref="StatusChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnStatusChanged(EventArgs args)
        {
            if(StatusChanged != null) StatusChanged(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="classFactory"></param>
        public void RegisterImplementations(IClassFactory classFactory)
        {
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="parameters"></param>
        public void Startup(PluginStartupParameters parameters)
        {
            var pluginSettingsStorage = Factory.Singleton.Resolve<IPluginSettingsStorage>().Singleton;
            var pluginSettings = pluginSettingsStorage.Load();
            _Enabled = pluginSettings.ReadBool(this, EnabledSettingsKey, false);

            InitialiseFile();

            var webServer = Factory.Singleton.Resolve<IAutoConfigWebServer>().Singleton.WebServer;
            webServer.ResponseSent += WebServer_ResponseSent;
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void GuiThreadStartup()
        {
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void Shutdown()
        {
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void ShowWinFormsOptionsUI()
        {
            bool enabled = MessageBox.Show(
                "Enabling this plugin logs every web request in a CSV file. Do you want to enable it?",
                "Enable Plugin",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1
            ) == DialogResult.Yes;

            lock(_SyncLock) {
                var pluginSettingsStorage = Factory.Singleton.Resolve<IPluginSettingsStorage>().Singleton;
                var pluginSettings = pluginSettingsStorage.Load();
                pluginSettings.Write(this, EnabledSettingsKey, enabled);
                pluginSettingsStorage.Save(pluginSettings);

                _Enabled = enabled;
                InitialiseFile();
            }
        }

        /// <summary>
        /// Creates the file if necessary.
        /// </summary>
        private void InitialiseFile()
        {
            if(_Enabled) {
                var folder = Factory.Singleton.Resolve<IConfigurationStorage>().Folder;
                folder = Path.Combine(folder, "WebRequestLogger");
                if(!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                _FileName = Path.Combine(folder, "Log.csv");
                if(!File.Exists(_FileName)) File.Create(_FileName).Close();
                if(new FileInfo(_FileName).Length == 0) File.WriteAllLines(_FileName, new string[] { "DateTimeUTC,EndpointIPAddress,EndpointPort,UserAddress,RequestAddress,FullUrl,ResponseStatus,ResponseLength,Milliseconds" });
            }

            UpdateStatus();
        }

        /// <summary>
        /// Sets <see cref="Status"/> and <see cref="StatusDescription"/>.
        /// </summary>
        private void UpdateStatus()
        {
            if(!_Enabled) {
                Status = "Disabled";
                StatusDescription = null;
            } else {
                Status = "Logging requests";
                StatusDescription = _FileName;
            }

            OnStatusChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Called whenever the web server sends a response to a request.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void WebServer_ResponseSent(object sender, ResponseSentEventArgs args)
        {
            if(_Enabled) {
                lock(_SyncLock) {
                    using(StreamWriter writer = new StreamWriter(_FileName, true)) {
                        writer.WriteLine(@"{0:u},{1},{2},{3},""{4}"",""{5}"",{6},{7},{8}",
                            DateTime.UtcNow,
                            args.Request.RemoteEndPoint.Address,
                            args.Request.RemoteEndPoint.Port,
                            args.UserAddress,
                            args.UrlRequested.Replace("\"", "\"\"").Replace("\r", "").Replace("\n", ""),
                            args.Request.RawUrl.Replace("\"", "\"\"").Replace("\r", "").Replace("\n", ""),
                            args.HttpStatus,
                            args.BytesSent,
                            args.Milliseconds);
                    }
                }
            }
        }
    }
}
