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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using VirtualRadar.Interface;
using VirtualRadar.Interface.WebServer;
using InterfaceFactory;

namespace VirtualRadar.WebServer
{
    /// <summary>
    /// The default implementation of <see cref="IResponder"/>.
    /// </summary>
    class Responder : IResponder
    {
        /// <summary>
        /// The object that locks access to the responder's fields, to allow multithreaded use.
        /// </summary>
        private object _SyncLock = new object();

        /// <summary>
        /// A map of types that the JSON serialiser knows about.
        /// </summary>
        private Dictionary<Type, JsonSerialiser> _JsonSerialiserMap = new Dictionary<Type,JsonSerialiser>();

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="text"></param>
        /// <param name="encoding"></param>
        /// <param name="mimeType"></param>
        public void SendText(IResponse response, string text, Encoding encoding, string mimeType)
        {
            if(response == null) throw new ArgumentNullException("response");

            if(encoding == null) encoding = Encoding.UTF8;
            var bytes = encoding.GetBytes(text ?? "");

            response.StatusCode = HttpStatusCode.OK;
            response.MimeType = mimeType ?? MimeType.Text;
            response.ContentLength = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="json"></param>
        /// <param name="jsonpCallbackFunction"></param>
        public void SendJson(IResponse response, object json, string jsonpCallbackFunction)
        {
            if(response == null) throw new ArgumentNullException("response");
            if(json == null) throw new ArgumentNullException("json");

            response.AddHeader("Cache-Control", "max-age=0, no-cache, no-store, must-revalidate");

            var type = json.GetType();
            JsonSerialiser serialiser;
            lock(_SyncLock) {
                if(!_JsonSerialiserMap.TryGetValue(type, out serialiser)) {
                    serialiser = new JsonSerialiser();
                    serialiser.Initialise(type);
                    _JsonSerialiserMap.Add(type, serialiser);
                }
            }

            string text;
            using(MemoryStream stream = new MemoryStream()) {
                serialiser.WriteObject(stream, json);
                text = Encoding.UTF8.GetString(stream.ToArray());
            }

            if(!String.IsNullOrEmpty(jsonpCallbackFunction)) text = String.Format("{0}({1})", jsonpCallbackFunction, text);

            SendText(response, text, Encoding.UTF8, MimeType.Json);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="image"></param>
        /// <param name="format"></param>
        public void SendImage(IResponse response, Image image, ImageFormat format)
        {
            if(response == null) throw new ArgumentNullException("response");
            if(image == null) throw new ArgumentNullException("image");
            if(format == null) throw new ArgumentNullException("format");
            if(format != ImageFormat.Bmp && format != ImageFormat.Gif && format != ImageFormat.Png) throw new NotSupportedException(String.Format("Responder does not support sending {0} images", format));

            response.AddHeader("Cache-Control", "max-age=21600");

            byte[] bytes;
            using(var stream = new MemoryStream()) {
                using(var copy = (Image)image.Clone()) {
                    copy.Save(stream, format);
                }

                bytes = stream.ToArray();
            }

            response.StatusCode = HttpStatusCode.OK;
            response.MimeType = ImageMimeType(format);
            response.ContentLength = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Returns the correct MIME type for an image format.
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        private static string ImageMimeType(ImageFormat format)
        {
            if(format == ImageFormat.Png)       return MimeType.PngImage;
            else if(format == ImageFormat.Gif)  return MimeType.GifImage;
            else if(format == ImageFormat.Bmp)  return MimeType.BitmapImage;
            else                                return "";
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="audio"></param>
        /// <param name="mimeType"></param>
        public void SendAudio(IResponse response, byte[] audio, string mimeType)
        {
            if(audio == null) throw new ArgumentNullException("audio");
            SendBinary(response, audio, mimeType);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="binary"></param>
        /// <param name="mimeType"></param>
        public void SendBinary(IResponse response, byte[] binary, string mimeType)
        {
            if(response == null) throw new ArgumentNullException("response");
            if(binary == null) throw new ArgumentNullException("binary");
            if(mimeType == null) throw new ArgumentNullException("mimeType");

            response.StatusCode = HttpStatusCode.OK;
            response.MimeType = mimeType;
            response.ContentLength = binary.Length;
            response.OutputStream.Write(binary, 0, binary.Length);
        }
    }
}
