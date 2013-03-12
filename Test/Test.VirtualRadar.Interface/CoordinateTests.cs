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
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualRadar.Interface;

namespace Test.VirtualRadar.Interface
{
    [TestClass]
    public class CoordinateTests
    {
        [TestMethod]
        public void Coordinate_Constructor_Initialises_To_Known_State_And_Properties_Work()
        {
            var coordinate1 = new Coordinate(1.1, 2.2);
            Assert.AreEqual(0L, coordinate1.DataVersion);
            Assert.AreEqual(0L, coordinate1.Tick);
            Assert.AreEqual(1.1, coordinate1.Latitude);
            Assert.AreEqual(2.2, coordinate1.Longitude);
            Assert.AreEqual(null, coordinate1.Heading);

            var coordinate2 = new Coordinate(1L, 2L, 3.1, 4.1, 5f);
            Assert.AreEqual(1L, coordinate2.DataVersion);
            Assert.AreEqual(2L, coordinate2.Tick);
            Assert.AreEqual(3.1, coordinate2.Latitude);
            Assert.AreEqual(4.1, coordinate2.Longitude);
            Assert.AreEqual(5f, coordinate2.Heading);
        }

        [TestMethod]
        public void Coordinate_Equals_Compares_Two_Coordinates_As_Equal_If_Their_Latitude_And_Longitude_Match()
        {
            // Other properties are ignored during comparison
            var c1 = new Coordinate(1, 2, 99, 100, 37.2f);
            var c2 = new Coordinate(5, 6, 99, 100, 99.5f);
            var c3 = new Coordinate(1, 2, 98, 100, 37.2f);
            var c4 = new Coordinate(1, 2, 99, 101, 37.2f);

            Assert.AreEqual(c1, c2);
            Assert.AreNotEqual(c1, c3);
            Assert.AreNotEqual(c1, c4);
        }

        [TestMethod]
        public void Coordinate_GetHashCode_Returns_Same_Value_For_Two_Objects_That_Compare_As_Equal()
        {
            var c1 = new Coordinate(1, 2, 99, 100, 37.2f);
            var c2 = new Coordinate(5, 6, 99, 100, 99.5f);

            Assert.AreEqual(c1.GetHashCode(), c2.GetHashCode());
        }
    }
}
