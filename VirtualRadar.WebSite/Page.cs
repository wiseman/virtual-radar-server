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
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Interface.WebSite;
using System.IO;
using System.Web;
using System.Globalization;
using InterfaceFactory;

namespace VirtualRadar.WebSite
{
    /// <summary>
    /// The internal base class for all objects that can produce content in response to browser requests
    /// that are received by a server.
    /// </summary>
    abstract class Page
    {
        #region Providers
        /// <summary>
        /// Gets the responder that derivees can use to fill the response with content.
        /// </summary>
        protected IResponder Responder { get; private set; }

        /// <summary>
        /// Gets or sets the website's provider object - used to abstract away parts of the environment in tests.
        /// </summary>
        public IWebSiteProvider Provider { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public Page()
        {
            Responder = Factory.Singleton.Resolve<IResponder>();
        }
        #endregion

        #region Query string extraction - QueryString, QueryLong etc.
        /// <summary>
        /// Returns the string associated with the name, optionally converting to uppercase first.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <param name="toUpperCase"></param>
        /// <returns></returns>
        protected string QueryString(RequestReceivedEventArgs args, string name, bool toUpperCase)
        {
            var result = args.QueryString[name];
            if(result != null && toUpperCase) result = result.ToUpper();

            return result;
        }

        /// <summary>
        /// Returns the bool value associated with the name.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        protected bool QueryBool(RequestReceivedEventArgs args, string name, bool defaultValue)
        {
            bool result = defaultValue;

            int? value = QueryNInt(args, name);
            if(value != null) result = value == 0 ? false : true;

            return result;
        }

        /// <summary>
        /// Returns the date associated with the name, as parsed using the current region settings.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <param name="assumeUniversalTime"></param>
        /// <param name="stripTimePortion"></param>
        /// <returns></returns>
        protected DateTime QueryDate(RequestReceivedEventArgs args, string name, DateTime defaultValue, bool assumeUniversalTime, bool stripTimePortion)
        {
            DateTime result = defaultValue;
            DateTimeStyles styles = assumeUniversalTime ? DateTimeStyles.AssumeUniversal : DateTimeStyles.AssumeLocal;
            var queryValue = QueryString(args, name, false);
            if(!String.IsNullOrEmpty(queryValue) && !DateTime.TryParse(queryValue, CultureInfo.CurrentCulture, styles, out result)) result = defaultValue;

            return !stripTimePortion ? result : result.Date;
        }

        /// <summary>
        /// Returns the integer value associated with the name or a default value if missing.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <param name="missingValue"></param>
        /// <returns></returns>
        protected int QueryInt(RequestReceivedEventArgs args, string name, int missingValue)
        {
            int result = missingValue;
            var queryValue = QueryString(args, name, false);
            if(!String.IsNullOrEmpty(queryValue) && !int.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) result = missingValue;

            return result;
        }

        /// <summary>
        /// Returns the long value associated with the name or a default value if missing.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <param name="missingValue"></param>
        /// <returns></returns>
        protected long QueryLong(RequestReceivedEventArgs args, string name, long missingValue)
        {
            long result = missingValue;
            var queryValue = QueryString(args, name, false);
            if(!String.IsNullOrEmpty(queryValue) && !long.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) result = missingValue;

            return result;
        }

        /// <summary>
        /// Returns the bool? value associated with the name or null if the name is missing.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        protected bool? QueryNBool(RequestReceivedEventArgs args, string name)
        {
            bool? result = null;

            int? value = QueryNInt(args, name);
            if(value != null) result = value == 0 ? false : true;

            return result;
        }

        /// <summary>
        /// Returns the double? value associated with the name or null if there is no value.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        protected double? QueryNDouble(RequestReceivedEventArgs args, string name)
        {
            double? result = null;
            var text = QueryString(args, name, false);
            if(!String.IsNullOrEmpty(text)) {
                double value;
                if(double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) result = value;
            }

            return result;
        }

        /// <summary>
        /// Returns the T? value associated with the name or null if there was no value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        protected Nullable<T> QueryNEnum<T>(RequestReceivedEventArgs args, string name)
            where T: struct
        {
            Nullable<T> result = null;

            int? value = QueryNInt(args, name);
            if(value != null) {
                if(Enum.IsDefined(typeof(T), value.Value)) result = (Nullable<T>)Enum.ToObject(typeof(T), value.Value);
            }

            return result;
        }

        /// <summary>
        /// Returns the int? value associated with the name or null if there is no value.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        protected int? QueryNInt(RequestReceivedEventArgs args, string name)
        {
            int? result = null;
            var text = QueryString(args, name, false);
            if(!String.IsNullOrEmpty(text)) {
                int value;
                if(int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) result = value;
            }

            return result;
        }
        #endregion

        #region Javascript formatters
        /// <summary>
        /// Formats the nullable value as a string using the invariant culture and plain formatting (no thousands separators etc).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="nullValue"></param>
        /// <param name="decimalPlaces"></param>
        /// <returns></returns>
        protected virtual string FormatNullable<T>(T? value, string nullValue = null, int decimalPlaces = -1)
            where T: struct
        {
            string result = nullValue;

            if(value != null) {
                var formatString = decimalPlaces < 0 ? "{0}" : String.Format("{{0:F{0}}}", decimalPlaces);
                result = String.Format(CultureInfo.InvariantCulture, formatString, value);
            }

            return result;
        }
        #endregion

        #region Event handlers - HandleRequest, LoadConfiguration
        /// <summary>
        /// If the request represents an address that is known to the site then the response object in the
        /// arguments passed across is filled with the content for that address and args.Handled is set.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public void HandleRequest(object sender, RequestReceivedEventArgs args)
        {
            if(!args.Handled) args.Handled = DoHandleRequest((IWebServer)sender, args);
        }

        /// <summary>
        /// When overridden by a derivee this examines the args to see whether the request can be handled by the object and then,
        /// if it can, it supplies content for the request.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="args"></param>
        /// <returns>True if the request was handled by the object, false if it was not.</returns>
        protected abstract bool DoHandleRequest(IWebServer server, RequestReceivedEventArgs args);

        /// <summary>
        /// Called by the web site when the website is first constructed and whenever a change in configuration is detected.
        /// </summary>
        /// <param name="configuration"></param>
        public void LoadConfiguration(Configuration configuration)
        {
            DoLoadConfiguration(configuration);
        }

        /// <summary>
        /// Can be overridden by derivees to pick up changes to the configuration.
        /// </summary>
        /// <param name="configuration"></param>
        protected virtual void DoLoadConfiguration(Configuration configuration)
        {
            ;
        }
        #endregion
    }
}
