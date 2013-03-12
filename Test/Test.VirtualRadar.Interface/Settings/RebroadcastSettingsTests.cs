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
using VirtualRadar.Interface.Settings;
using Test.Framework;

namespace Test.VirtualRadar.Interface.Settings
{
    [TestClass]
    public class RebroadcastSettingsTests
    {
        [TestMethod]
        public void RebroadcastSettings_Constructor_Initialises_To_Known_State_And_Properties_Work()
        {
            CheckProperties(new RebroadcastSettings());
        }

        public static void CheckProperties(RebroadcastSettings settings)
        {
            TestUtilities.TestProperty(settings, r => r.Enabled, false);
            TestUtilities.TestProperty(settings, r => r.Format, RebroadcastFormat.None, RebroadcastFormat.Passthrough);
            TestUtilities.TestProperty(settings, r => r.Name, null, "ABC");
            TestUtilities.TestProperty(settings, r => r.Port, 0, 19000);
        }

        [TestMethod]
        public void RebroadcastSettings_Equals_Returns_Correct_Value()
        {
            var item1 = new RebroadcastSettings();
            var item2 = new RebroadcastSettings();

            Assert.AreEqual(item1, item2);

            item2.Enabled = !item2.Enabled;
            Assert.AreNotEqual(item1, item2);

            item2 = new RebroadcastSettings();
            item2.Format = RebroadcastFormat.Port30003;
            Assert.AreNotEqual(item1, item2);

            item2 = new RebroadcastSettings();
            item2.Name = "Z";
            Assert.AreNotEqual(item1, item2);

            item2 = new RebroadcastSettings();
            item2.Port = 1001;
            Assert.AreNotEqual(item1, item2);
        }

        [TestMethod]
        public void RebroadcastSettings_Clone_Returns_Deep_Copy_Of_Original()
        {
            var original = new RebroadcastSettings() {
                Enabled = true,
                Format = RebroadcastFormat.Avr,
                Name = "The name",
                Port = 1234,
            };

            var copy = (RebroadcastSettings)original.Clone();
            Assert.AreNotSame(original, copy);

            foreach(var property in typeof(RebroadcastSettings).GetProperties()) {
                switch(property.Name) {
                    case "Enabled":     Assert.AreEqual(true, copy.Enabled); break;
                    case "Format":      Assert.AreEqual(RebroadcastFormat.Avr, copy.Format); break;
                    case "Name":        Assert.AreEqual("The name", copy.Name); break;
                    case "Port":        Assert.AreEqual(1234, copy.Port); break;
                    default:            throw new NotImplementedException();
                }
            }
        }
    }
}
