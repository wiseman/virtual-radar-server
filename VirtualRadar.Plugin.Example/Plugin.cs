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
using System.Windows.Forms;
using InterfaceFactory;
using VirtualRadar.Interface;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Interface.Database;

namespace VirtualRadar.Plugin.Example
{
    /// <summary>
    /// An example plugin. Comments have been kept to a minimum to avoid clutter in the Wiki.
    /// </summary>
    public class Plugin : IPlugin
    {
        // Constant fields
        private const string EnabledSettingsKey = "Enabled";


        // Fields
        public bool _Enabled;

        private long _CountRequests;

        private long _CountBytesSent;


        // Properties
        public string Id                    { get { return "VirtualRadar.Plugin.Example"; } }

        public string Name                  { get { return "A Working Example"; } }

        public string Version               { get { return "1.0.0"; } }

        public string Status                { get; private set; }

        public string StatusDescription     { get; private set; }

        public bool HasOptions              { get { return true; } }


        // Events
        public event EventHandler StatusChanged;

        protected virtual void OnStatusChanged(EventArgs args)
        {
            if(StatusChanged != null) StatusChanged(this, args);
        }


        // Updates the status and raises StatusChanged
        private void UpdateStatus()
        {
            if(!_Enabled) {
                Status = "Disabled";
                StatusDescription = null;
            } else {
                Status = String.Format("Enabled");
                StatusDescription = String.Format("Sent {0:N0} bytes in response to {1:N0} requests", _CountBytesSent, _CountRequests);
            }

            OnStatusChanged(EventArgs.Empty);
        }


        // IPlugin methods
        public void RegisterImplementations(IClassFactory classFactory)
        {
            // If the following two lines are uncommented then the plugin will replace the usual classes that read and write the SQLite databases
            // with its own versions. Its own versions don't do anything - so you don't get any registrations for the aircraft and you don't get
            // any sessions logged while this plugin is loaded.

            //classFactory.Register<IBaseStationDatabase, BaseStationDatabaseStub>();
            //classFactory.Register<ILogDatabase, LogDatabaseStub>();
        }

        public void Startup(PluginStartupParameters parameters)
        {
            var pluginSettingsStorage = Factory.Singleton.Resolve<IPluginSettingsStorage>().Singleton;
            var pluginSettings = pluginSettingsStorage.Load();

            _Enabled = pluginSettings.ReadBool(this, EnabledSettingsKey, false);

            var webServer = Factory.Singleton.Resolve<IAutoConfigWebServer>().Singleton.WebServer;
            webServer.BeforeRequestReceived += WebServer_BeforeRequestReceived;
            webServer.ResponseSent += WebServer_ResponseSent;

            UpdateStatus();
        }

        public void GuiThreadStartup()
        {
        }

        public void ShowWinFormsOptionsUI()
        {
            _Enabled = MessageBox.Show(
                "Enabling this plugin logs web requests with the Windows system debug log. Do you want to enable it?",
                "Enable Plugin",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1
            ) == DialogResult.Yes;

            var pluginSettingsStorage = Factory.Singleton.Resolve<IPluginSettingsStorage>().Singleton;
            var pluginSettings = pluginSettingsStorage.Load();
            pluginSettings.Write(this, EnabledSettingsKey, _Enabled);
            pluginSettingsStorage.Save(pluginSettings);

            UpdateStatus();
        }

        public void Shutdown()
        {
        }


        // Events subscribed
        private void WebServer_BeforeRequestReceived(object sender, RequestReceivedEventArgs args)
        {
            if(_Enabled) {
                System.Diagnostics.Debug.WriteLine(String.Format("Browser requested {0}", args.Request.RawUrl));
                ++_CountRequests;
                UpdateStatus();
            }
        }

        private void WebServer_ResponseSent(object sender, ResponseSentEventArgs args)
        {
            if(_Enabled) {
                _CountBytesSent += args.BytesSent;
                UpdateStatus();
            }
        }
    }
}
