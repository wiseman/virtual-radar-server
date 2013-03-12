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
using System.Text;
using System.Net;
using System.IO.IsolatedStorage;
using System.IO;
using System.Runtime.Serialization;
using System.Reflection;
using VirtualRadar.Interface.Settings;
using System.Xml.Serialization;
using InterfaceFactory;

namespace VirtualRadar.Library.Settings
{
    /// <summary>
    /// The default implementation of <see cref="IConfigurationStorage"/>.
    /// </summary>
    sealed class ConfigurationStorage : IConfigurationStorage
    {
        /// <summary>
        /// A private class that supplies the default implementation of <see cref="IConfigurationStorageProvider"/>.
        /// </summary>
        class DefaultProvider : IConfigurationStorageProvider
        {
            /// <summary>
            /// The folder where the files are held.
            /// </summary>
            private static string _Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualRadar");

            /// <summary>
            /// See interface docs.
            /// </summary>
            public string Folder
            {
                get { return _Folder; }
                set { _Folder = value; }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IConfigurationStorageProvider Provider { get; set; }

        private static readonly IConfigurationStorage _Singleton = new ConfigurationStorage();
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IConfigurationStorage Singleton { get { return _Singleton; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Folder
        {
            get { return Provider.Folder; }
            set { Provider.Folder = value; }
        }

        /// <summary>
        /// Gets the full path to the configuration file.
        /// </summary>
        private string FileName { get { return Path.Combine(Provider.Folder, "Configuration.xml"); } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler ConfigurationChanged;

        /// <summary>
        /// Raises <see cref="ConfigurationChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        private void OnConfigurationChanged(EventArgs args)
        {
            if(ConfigurationChanged != null) ConfigurationChanged(this, args);
        }

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public ConfigurationStorage()
        {
            Provider = new DefaultProvider();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void Erase()
        {
            if(File.Exists(FileName)) {
                File.Delete(FileName);
                OnConfigurationChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <returns></returns>
        public Configuration Load()
        {
            var result = new Configuration();

            if(File.Exists(FileName)) {
                using(StreamReader stream = new StreamReader(FileName, Encoding.UTF8)) {
                    XmlSerializer serialiser = new XmlSerializer(typeof(Configuration));
                    result = (Configuration)serialiser.Deserialize(stream);
                }
            }

            // Force retired settings to their expected values
            result.BaseStationSettings.IgnoreBadMessages = true;

            return result;
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="configuration"></param>
        public void Save(Configuration configuration)
        {
            if(!Directory.Exists(Provider.Folder)) Directory.CreateDirectory(Provider.Folder);

            using(StreamWriter stream = new StreamWriter(FileName, false, Encoding.UTF8)) {
                XmlSerializer serialiser = new XmlSerializer(typeof(Configuration));
                serialiser.Serialize(stream, configuration);
            }

            OnConfigurationChanged(EventArgs.Empty);
        }
    }
}
