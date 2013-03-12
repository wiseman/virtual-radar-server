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
using VirtualRadar.Interface;
using Moq;
using VirtualRadar.Interface.Settings;
using Test.Framework;
using InterfaceFactory;

namespace Test.VirtualRadar.Library
{
    [TestClass]
    public class AutoConfigPictureFolderCacheTests
    {
        public TestContext TestContext { get; set; }

        private IClassFactory _ClassFactorySnapshot;
        private IAutoConfigPictureFolderCache _AutoConfig;
        private Mock<IDirectoryCache> _DirectoryCache;
        private Configuration _Configuration;
        private Mock<IConfigurationStorage> _ConfigurationStorage;

        [TestInitialize]
        public void TestInitialise()
        {
            _ClassFactorySnapshot = Factory.TakeSnapshot();

            _DirectoryCache = TestUtilities.CreateMockImplementation<IDirectoryCache>();
            _Configuration = new Configuration();
            _ConfigurationStorage = TestUtilities.CreateMockSingleton<IConfigurationStorage>();
            _ConfigurationStorage.Setup(s => s.Load()).Returns(_Configuration);

            _AutoConfig = Factory.Singleton.Resolve<IAutoConfigPictureFolderCache>();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Factory.RestoreSnapshot(_ClassFactorySnapshot);
        }

        [TestMethod]
        public void AutoConfigPictureFolderCache_Constructor_Initialises_To_Known_State()
        {
            Assert.AreEqual(null, _AutoConfig.DirectoryCache);
        }

        [TestMethod]
        public void AutoConfigPictureFolderCache_Singleton_Returns_Same_Reference_For_All_Instances()
        {
            var instance1 = Factory.Singleton.Resolve<IAutoConfigPictureFolderCache>();
            var instance2 = Factory.Singleton.Resolve<IAutoConfigPictureFolderCache>();

            Assert.AreNotSame(instance1, instance2);
            Assert.IsNotNull(instance1.Singleton);
            Assert.AreSame(instance1.Singleton, instance2.Singleton);
        }

        [TestMethod]
        public void AutoConfigPictureFolderCache_Initialise_Sets_DirectoryCache_Property()
        {
            _AutoConfig.Initialise();

            Assert.AreSame(_DirectoryCache.Object, _AutoConfig.DirectoryCache);
        }

        [TestMethod]
        public void AutoConfigPictureFolderCache_Initialise_Does_Not_Create_Two_Caches_If_Called_Twice()
        {
            _AutoConfig.Initialise();

            var badObject = TestUtilities.CreateMockImplementation<IDirectoryCache>();
            _AutoConfig.Initialise();

            Assert.AreSame(_DirectoryCache.Object, _AutoConfig.DirectoryCache);
        }

        [TestMethod]
        public void AutoConfigPictureFolderCache_Initialise_Applies_Picture_Folder_Property()
        {
            _Configuration.BaseStationSettings.PicturesFolder = "Abc";

            _AutoConfig.Initialise();

            Assert.AreSame("Abc", _DirectoryCache.Object.Folder);
        }

        [TestMethod]
        public void AutoConfigPictureFolderCache_Initialise_Hooks_Configuration_Change_Event()
        {
            _AutoConfig.Initialise();

            _Configuration.BaseStationSettings.PicturesFolder = "new";
            _ConfigurationStorage.Raise(s => s.ConfigurationChanged += null, EventArgs.Empty);

            Assert.AreEqual("new", _DirectoryCache.Object.Folder);
        }
    }
}
