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
using Moq;
using VirtualRadar.Library;
using Test.Framework;
using VirtualRadar.Interface.Settings;
using InterfaceFactory;

namespace Test.VirtualRadar.Library
{
    [TestClass]
    public class AircraftPictureManagerTests
    {
        #region TestContext, Fields, TestInitialise, TestCleanup
        public TestContext TestContext { get; set; }

        private IClassFactory _OriginalClassFactory;
        private IAircraftPictureManager _PictureManager;
        private Mock<IConfigurationStorage> _ConfigurationStorage;
        private Configuration _Configuration;
        private EventRecorder<EventArgs> _CacheCleared;
        private Mock<IDirectoryCache> _DirectoryCache;

        [TestInitialize]
        public void TestInitialise()
        {
            _OriginalClassFactory = Factory.TakeSnapshot();

            _ConfigurationStorage = TestUtilities.CreateMockSingleton<IConfigurationStorage>();
            _Configuration = new Configuration();
            _ConfigurationStorage.Setup(c => c.Load()).Returns(_Configuration);

            _DirectoryCache = new Mock<IDirectoryCache>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();

            _CacheCleared = new EventRecorder<EventArgs>();

            _PictureManager = Factory.Singleton.Resolve<IAircraftPictureManager>();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Factory.RestoreSnapshot(_OriginalClassFactory);
        }
        #endregion

        #region FindPicture
        [TestMethod]
        public void AircraftPictureManager_FindPicture_Returns_Full_Path_If_Picture_Exists()
        {
            _DirectoryCache.Object.Folder = @"c:\Whatever";
            _DirectoryCache.Setup(c => c.FileExists(@"c:\Whatever\ICAO.png")).Returns(true);

            Assert.AreEqual(@"c:\Whatever\Icao.png", _PictureManager.FindPicture(_DirectoryCache.Object, "ICAO", "REG"), true);
        }

        [TestMethod]
        public void AircraftPictureManager_FindPicture_Returns_Null_If_Picture_Does_Not_Exist()
        {
            Assert.AreEqual(null, _PictureManager.FindPicture(_DirectoryCache.Object, "ICAO", "REG"), false);
        }

        [TestMethod]
        public void AircraftPictureManager_FindPicture_Searches_For_Pictures_In_Correct_Order()
        {
            _DirectoryCache.Object.Folder = @"c:\";
            var existingFiles = new List<string>() {
                @"c:\ABC123.png", @"c:\ABC123.jpg", @"c:\ABC123.jpeg", @"c:\ABC123.bmp",
                @"c:\G-ABCD.png", @"c:\G-ABCD.jpg", @"c:\G-ABCD.jpeg", @"c:\G-ABCD.bmp",
            };
            _DirectoryCache.Setup(c => c.FileExists(It.IsAny<string>())).Returns((string fn) => { return existingFiles.Where(ef => ef.Equals(fn, StringComparison.OrdinalIgnoreCase)).Any(); });

            Assert.AreEqual(@"c:\ABC123.jpg", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));

            existingFiles.Remove(@"c:\ABC123.jpg");
            Assert.AreEqual(@"c:\ABC123.jpeg", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));

            existingFiles.Remove(@"c:\ABC123.jpeg");
            Assert.AreEqual(@"c:\ABC123.png", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));

            existingFiles.Remove(@"c:\ABC123.png");
            Assert.AreEqual(@"c:\ABC123.bmp", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));

            existingFiles.Remove(@"c:\ABC123.bmp");
            Assert.AreEqual(@"c:\G-ABCD.jpg", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));

            existingFiles.Remove(@"c:\G-ABCD.jpg");
            Assert.AreEqual(@"c:\G-ABCD.jpeg", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));

            existingFiles.Remove(@"c:\G-ABCD.jpeg");
            Assert.AreEqual(@"c:\G-ABCD.png", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));

            existingFiles.Remove(@"c:\G-ABCD.png");
            Assert.AreEqual(@"c:\G-ABCD.bmp", _PictureManager.FindPicture(_DirectoryCache.Object, "ABC123", "G-ABCD"));
        }

        [TestMethod]
        public void AircraftPictureManager_FindPicture_Copes_If_ICAO24_Is_Null()
        {
            _PictureManager.FindPicture(_DirectoryCache.Object, null, "A");

            _DirectoryCache.Verify(p => p.FileExists(It.IsAny<string>()), Times.Exactly(4));
        }

        [TestMethod]
        public void AircraftPictureManager_FindPicture_Copes_If_Registration_Is_Null()
        {
            _PictureManager.FindPicture(_DirectoryCache.Object, "A", null);

            _DirectoryCache.Verify(p => p.FileExists(It.IsAny<string>()), Times.Exactly(4));
        }

        [TestMethod]
        public void AircraftPictureManager_FindPicture_Only_Searches_For_Known_Extensions()
        {
            // Each aircraft should generate 8 lookups on filenames - see other tests for those lookups
            _PictureManager.FindPicture(_DirectoryCache.Object, "A", "B");

            _DirectoryCache.Verify(p => p.FileExists(It.IsAny<string>()), Times.Exactly(8));
        }

        [TestMethod]
        [DataSource("Data Source='AircraftTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "IcaoCompliantRegistration$")]
        public void AircraftPictureManager_FindPicture_Uses_ICAO_Compliant_Registration()
        {
            var worksheet = new ExcelWorksheetData(TestContext);
            var registration = worksheet.EString("Registration");
            var icaoCompliantRegistration = worksheet.EString("IcaoCompliantRegistration");

            if(registration != icaoCompliantRegistration && !String.IsNullOrEmpty(icaoCompliantRegistration)) {
                var registrationFileName = String.Format("{0}.jpg", registration);
                var icaoCompliantFileName = String.Format("{0}.jpg", icaoCompliantRegistration);

                _DirectoryCache.Object.Folder = "";
                _DirectoryCache.Setup(c => c.FileExists(registrationFileName)).Returns((string fn) => { Assert.Fail("FindPicture did not strip non-ICAO characters from the registration"); return false; });
                _DirectoryCache.Setup(c => c.FileExists(icaoCompliantFileName)).Returns((string fn) => { return true; });

                Assert.AreEqual(icaoCompliantFileName, _PictureManager.FindPicture(_DirectoryCache.Object, "ICAO", registration));
            }
        }
        #endregion
    }
}
