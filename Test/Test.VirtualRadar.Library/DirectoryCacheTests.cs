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
using InterfaceFactory;
using Test.Framework;
using System.IO;

namespace Test.VirtualRadar.Library
{
    [TestClass]
    public class DirectoryCacheTests
    {
        #region TestFileInfo
        class TestFileInfo : IDirectoryCacheProviderFileInfo
        {
            public string Name { get; set; }
            public DateTime LastWriteTimeUtc { get; set; }

            public TestFileInfo(string name) : this(name, new DateTime(2001, 2, 3, 4, 5, 6, 789))
            {
            }

            public TestFileInfo(string name, DateTime lastModified) : base()
            {
                Name = name;
                LastWriteTimeUtc = lastModified;
            }
        }
        #endregion

        #region TestContext, Fields, TestIntialise, TestCleanup
        public TestContext TestContext { get; set; }

        private IClassFactory _ClassFactorySnapshot;
        private IDirectoryCache _DirectoryCache;
        private Mock<IDirectoryCacheProvider> _Provider;
        private Mock<IBackgroundWorker> _BackgroundWorker;
        private Mock<IHeartbeatService> _HeartbeatService;
        private const int SecondsBetweenRefreshes = 60;
        private DateTime _Now;
        private DateTime _LastModifiedUtc;
        private List<TestFileInfo> _Files;

        private EventRecorder<EventArgs> _CacheChangedEvent;
        private Mock<ILog> _Log;

        [TestInitialize]
        public void TestInitialise()
        {
            _ClassFactorySnapshot = Factory.TakeSnapshot();

            CreateBackgroundWorkerMock();
            _HeartbeatService = TestUtilities.CreateMockSingleton<IHeartbeatService>();
            _Log = TestUtilities.CreateMockSingleton<ILog>();
            _Log.Setup(g => g.WriteLine(It.IsAny<string>(), It.IsAny<string>(),  It.IsAny<string>())).Callback(() => { throw new InvalidOperationException("Log was unexpectedly written"); });

            _CacheChangedEvent = new EventRecorder<EventArgs>();

            _Now = new DateTime(2001, 2, 3, 4, 5, 6, 789);
            _LastModifiedUtc = new DateTime(2009, 8, 7, 6, 5, 4, 321);
            _Files = new List<TestFileInfo>();

            _Provider = new Mock<IDirectoryCacheProvider>() { DefaultValue = DefaultValue.Mock }.SetupAllProperties();
            _Provider.Setup(p => p.FolderExists(It.IsAny<string>())).Returns(true);
            _Provider.Setup(p => p.UtcNow).Returns(() => { return _Now; });
            _Provider.Setup(p => p.GetFilesInFolder(It.IsAny<string>())).Returns(new List<TestFileInfo>());

            _DirectoryCache = Factory.Singleton.Resolve<IDirectoryCache>();
            _DirectoryCache.Provider = _Provider.Object;
        }

        private void CreateBackgroundWorkerMock()
        {
            _BackgroundWorker = TestUtilities.CreateMockImplementation<IBackgroundWorker>();
            _BackgroundWorker.Setup(w => w.StartWork(It.IsAny<object>())).Callback((object obj) => {
                _BackgroundWorker.Setup(w => w.State).Returns(obj);
                _BackgroundWorker.Raise(w => w.DoWork += null, EventArgs.Empty);
            });
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Factory.RestoreSnapshot(_ClassFactorySnapshot);
        }
        #endregion

        #region Constructor and Properties
        [TestMethod]
        public void DirectoryCache_Constructor_Initialises_To_Known_State_And_Properties_Work()
        {
            var cache = Factory.Singleton.Resolve<IDirectoryCache>();
            Assert.IsNotNull(cache.Provider);
            TestUtilities.TestProperty(cache, "Provider", cache.Provider, _Provider.Object);
            TestUtilities.TestProperty(cache, "Folder", null, "Abc");
        }

        [TestMethod]
        public void DirectoryCache_Folder_Change_Triggers_Background_File_Load()
        {
            _DirectoryCache.Folder = "XyZ";

            _BackgroundWorker.Verify(w => w.StartWork(It.IsAny<object>()), Times.Once());
            _Provider.Verify(p => p.GetFilesInFolder("XyZ"), Times.Once());
            _Provider.Verify(p => p.GetFilesInFolder(It.IsAny<string>()), Times.Once());
        }

        [TestMethod]
        public void DirectoryCache_Folder_Change_Checks_For_Folders_Existence_Before_Load()
        {
            _Provider.Setup(p => p.GetFilesInFolder("abc")).Callback(() => {
                _Provider.Verify(p => p.FolderExists("abc"), Times.Once());
            }).Returns(_Files);

            _DirectoryCache.Folder = "abc";
            _Provider.Verify(p => p.GetFilesInFolder("abc"), Times.Once());
        }

        [TestMethod]
        public void DirectoryCache_Folder_Change_Does_Not_Load_Files_If_Folder_Does_Not_Exist()
        {
            _Provider.Setup(p => p.FolderExists(It.IsAny<string>())).Returns(false);
            _DirectoryCache.Folder = "XyZ";

            Assert.AreEqual("XyZ", _DirectoryCache.Folder);
            _Provider.Verify(p => p.GetFilesInFolder("XyZ"), Times.Never());
        }

        [TestMethod]
        public void DirectoryCache_Folder_Change_To_Missing_Folder_Clears_Cache()
        {
            _Files.Add(new TestFileInfo("b"));
            _Provider.Setup(p => p.GetFilesInFolder(@"c:\a")).Returns(_Files);
            _Provider.Setup(p => p.FolderExists(@"c:\b")).Returns(false);

            _DirectoryCache.Folder = @"c:\a";
            _DirectoryCache.Folder = @"c:\b";

            Assert.IsFalse(_DirectoryCache.FileExists(@"c:\a\b"));
        }

        [TestMethod]
        public void DirectoryCache_Folder_Set_To_Null_Does_Not_Search_For_Files()
        {
            _DirectoryCache.Folder = "xyz";
            _DirectoryCache.Folder = null;

            Assert.AreEqual(null, _DirectoryCache.Folder);
            _Provider.Verify(p => p.GetFilesInFolder("xyz"), Times.Once());
            _Provider.Verify(p => p.GetFilesInFolder(It.IsAny<string>()), Times.Once());
        }

        [TestMethod]
        public void DirectoryCache_Folder_Set_To_Null_Does_Check_For_Existence_Of_Folder()
        {
            _DirectoryCache.Folder = "xyz";
            _DirectoryCache.Folder = null;

            _Provider.Verify(p => p.FolderExists("xyz"), Times.Once());
            _Provider.Verify(p => p.FolderExists(null), Times.Never());
        }

        [TestMethod]
        public void DirectoryCache_Folder_Set_To_Null_Clears_Cache()
        {
            _Files.Add(new TestFileInfo("b"));
            _Provider.Setup(p => p.GetFilesInFolder(@"c:\a")).Returns(_Files);

            _DirectoryCache.Folder = @"c:\a";
            _DirectoryCache.Folder = null;

            Assert.IsFalse(_DirectoryCache.FileExists(@"c:\a\b"));
        }

        [TestMethod]
        public void DirectoryCache_Folder_Set_To_Self_Does_Not_Search_For_Files_Again()
        {
            _DirectoryCache.Folder = "xyz";
            _DirectoryCache.Folder = "xyz";

            _Provider.Verify(p => p.GetFilesInFolder("xyz"), Times.Once());
        }
        #endregion

        #region BeginRefresh
        [TestMethod]
        public void DirectoryCache_BeginRefresh_Loads_Files_On_Background_Thread()
        {
            _DirectoryCache.Folder = "XyZ";
            CreateBackgroundWorkerMock();
            _DirectoryCache.BeginRefresh();

            _BackgroundWorker.Verify(w => w.StartWork(It.IsAny<object>()), Times.Once());
        }

        [TestMethod]
        public void DirectoryCache_BeginRefresh_Checks_That_Folder_Exists()
        {
            _DirectoryCache.Folder = "Xyz";
            CreateBackgroundWorkerMock();
            _DirectoryCache.BeginRefresh();

            _Provider.Verify(p => p.FolderExists("Xyz"), Times.Exactly(2));
        }

        [TestMethod]
        public void DirectoryCache_BeginRefresh_Does_Not_Check_For_Folder_Existence_If_Null()
        {
            _DirectoryCache.BeginRefresh();

            _Provider.Verify(p => p.FolderExists(It.IsAny<string>()), Times.Never());
        }

        [TestMethod]
        public void DirectoryCache_BeginRefresh_Loads_Files_If_Folder_Exists()
        {
            _DirectoryCache.Folder = "Xyz";
            CreateBackgroundWorkerMock();
            _DirectoryCache.BeginRefresh();

            _Provider.Verify(p => p.GetFilesInFolder("Xyz"), Times.Exactly(2));
        }

        [TestMethod]
        public void DirectoryCache_BeginRefresh_Does_Not_Load_Files_If_Folder_Does_Not_Exist()
        {
            _Provider.Setup(p => p.FolderExists("Xyz")).Returns(false);

            _DirectoryCache.Folder = "Xyz";
            CreateBackgroundWorkerMock();
            _DirectoryCache.BeginRefresh();

            _Provider.Verify(p => p.GetFilesInFolder("Xyz"), Times.Never());
        }
        #endregion

        #region Heartbeat Tick
        [TestMethod]
        public void DirectoryCache_Heartbeat_Triggers_Refresh()
        {
            _DirectoryCache.Folder = "Xyz";

            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            _Provider.Verify(p => p.FolderExists("Xyz"), Times.Exactly(2));
        }

        [TestMethod]
        public void DirectoryCache_Heartbeat_Does_Not_Trigger_Refresh_If_Timeout_Has_Not_Elapsed()
        {
            _DirectoryCache.Folder = "Xyz";

            _Now = _Now.AddSeconds(SecondsBetweenRefreshes).AddMilliseconds(-1);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            _Provider.Verify(p => p.FolderExists("Xyz"), Times.Exactly(1));
        }
        #endregion

        #region FileExists, Add, Remove spreadsheet tests
        [TestMethod]
        [DataSource("Data Source='LibraryTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "DirectoryCacheExists$")]
        public void DirectoryCache_FileExists_Returns_Correct_Value()
        {
            SpreadsheetTests(null);
        }

        [TestMethod]
        [DataSource("Data Source='LibraryTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "DirectoryCacheAdd$")]
        public void DirectoryCache_Add_Behaves_Correctly()
        {
            SpreadsheetTests((w) => { _DirectoryCache.Add(w.EString("FileName")); });
        }

        [TestMethod]
        [DataSource("Data Source='LibraryTests.xls';Provider=Microsoft.Jet.OLEDB.4.0;Persist Security Info=False;Extended Properties='Excel 8.0'",
                    "DirectoryCacheRemove$")]
        public void DirectoryCache_Remove_Behaves_Correctly()
        {
            SpreadsheetTests((w) => { _DirectoryCache.Remove(w.EString("FileName")); });
        }

        private void SpreadsheetTests(Action<ExcelWorksheetData> afterSetFolder)
        {
            var worksheet = new ExcelWorksheetData(TestContext);
            bool checkCacheChanged = worksheet.ColumnExists("CacheChanged");

            for(int i = 1;i <= 2;++i) {
                string nameColumn = String.Format("File{0}", i);
                string lastModifiedColumn = String.Format("Time{0}", i);

                if(!worksheet.ColumnExists(nameColumn)) continue;

                var name = worksheet.String(nameColumn);
                var time = worksheet.ColumnExists(lastModifiedColumn) ? worksheet.DateTime(lastModifiedColumn) : new DateTime(2999, 12, 31);
                if(name != null) _Files.Add(new TestFileInfo(name, time));
            }
            _Provider.Setup(p => p.GetFilesInFolder(It.IsAny<string>())).Returns(_Files);

            var folder = worksheet.String("Folder");
            if(folder != null) _DirectoryCache.Folder = folder;

            if(worksheet.ColumnExists("LastModified")) {
                var fileInfo = worksheet.String("LastModified") == null ? null : new TestFileInfo(worksheet.String("FileName"), worksheet.DateTime("LastModified"));
                _Provider.Setup(p => p.GetFileInfo(worksheet.EString("FileName"))).Returns(fileInfo);
            }

            if(checkCacheChanged) _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            if(afterSetFolder != null) afterSetFolder(worksheet);

            Assert.AreEqual(folder, _DirectoryCache.Folder);
            Assert.AreEqual(worksheet.Bool("Exists"), _DirectoryCache.FileExists(worksheet.EString("SearchFor")));

            if(checkCacheChanged) Assert.AreEqual(worksheet.Bool("CacheChanged") ? 1 : 0, _CacheChangedEvent.CallCount);
        }
        #endregion

        #region CacheChanged
        [TestMethod]
        public void DirectoryCache_CacheChanged_Raised_When_Folder_Set()
        {
            _Files.Add(new TestFileInfo("a"));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _CacheChangedEvent.EventRaised += (s, a) => { Assert.IsTrue(_DirectoryCache.FileExists(@"x\a")); };

            _DirectoryCache.Folder = "x";

            Assert.AreEqual(1, _CacheChangedEvent.CallCount);
            Assert.AreSame(_DirectoryCache, _CacheChangedEvent.Sender);
            Assert.IsNotNull(_CacheChangedEvent.Args);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Raised_When_BeginRefresh_Finished()
        {
            _DirectoryCache.Folder = "x";

            _Files.Add(new TestFileInfo("a"));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _CacheChangedEvent.EventRaised += (s, a) => { Assert.IsTrue(_DirectoryCache.FileExists(@"x\a")); };

            CreateBackgroundWorkerMock();
            _DirectoryCache.BeginRefresh();

            Assert.AreEqual(1, _CacheChangedEvent.CallCount);
            Assert.AreSame(_DirectoryCache, _CacheChangedEvent.Sender);
            Assert.IsNotNull(_CacheChangedEvent.Args);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Raised_When_Heartbeat_Tick_Raised()
        {
            _DirectoryCache.Folder = "x";

            _Files.Add(new TestFileInfo("a"));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _CacheChangedEvent.EventRaised += (s, a) => { Assert.IsTrue(_DirectoryCache.FileExists(@"x\a")); };

            CreateBackgroundWorkerMock();
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(1, _CacheChangedEvent.CallCount);
            Assert.AreSame(_DirectoryCache, _CacheChangedEvent.Sender);
            Assert.IsNotNull(_CacheChangedEvent.Args);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Raised_If_Modified_Date_Changes()
        {
            _Files.Add(new TestFileInfo("a", new DateTime(2001, 2, 3)));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);

            _DirectoryCache.Folder = "x";

            _Files[0].LastWriteTimeUtc = new DateTime(2008, 7, 6);
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;

            CreateBackgroundWorkerMock();
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(1, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_If_Added_Entry_Seen_In_Subsequent_Refresh()
        {
            _Files.Add(new TestFileInfo("c", new DateTime(2001, 2, 3)));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);

            _DirectoryCache.Folder = "x";

            _Files.Insert(0, new TestFileInfo("b", new DateTime(2002, 3, 4)));
            _Provider.Setup(p => p.GetFileInfo(@"x\b")).Returns(_Files[0]);
            _DirectoryCache.Add(@"x\b");

            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            CreateBackgroundWorkerMock();
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_If_Removed_Entry_Still_Missing_In_Subsequent_Refresh()
        {
            _Files.Add(new TestFileInfo("c", new DateTime(2001, 2, 3)));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);

            _DirectoryCache.Folder = "x";

            _Files.Clear();
            _Provider.Setup(p => p.GetFileInfo(@"x\c")).Returns((TestFileInfo)null);
            _DirectoryCache.Remove(@"x\c");

            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            CreateBackgroundWorkerMock();
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_If_Folder_Does_Not_Change()
        {
            _DirectoryCache.Folder = "x";

            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _DirectoryCache.Folder = "x";

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_If_Content_Does_Not_Change()
        {
            _Files.Add(new TestFileInfo("a"));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);
            _DirectoryCache.Folder = "x";

            CreateBackgroundWorkerMock();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_If_Content_Does_Not_Change_But_Order_Does()
        {
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);

            _Files.AddRange(new TestFileInfo[] { new TestFileInfo("a"), new TestFileInfo("b") });
            _DirectoryCache.Folder = "x";

            _Files.Clear();
            _Files.AddRange(new TestFileInfo[] { new TestFileInfo("b"), new TestFileInfo("a") });

            CreateBackgroundWorkerMock();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_If_Content_Does_Not_Change_But_Case_Does()
        {
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);

            _Files.AddRange(new TestFileInfo[] { new TestFileInfo("a"), new TestFileInfo("b") });
            _DirectoryCache.Folder = "x";

            _Files.Clear();
            _Files.AddRange(new TestFileInfo[] { new TestFileInfo("A"), new TestFileInfo("B") });

            CreateBackgroundWorkerMock();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_If_BeginRefresh_Sees_No_Change()
        {
            _Files.Add(new TestFileInfo("a"));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);
            _DirectoryCache.Folder = "x";

            CreateBackgroundWorkerMock();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _DirectoryCache.BeginRefresh();

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Raised_If_Folder_Changes_But_File_Names_Are_Same()
        {
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(new TestFileInfo[] { new TestFileInfo("a") });
            _Provider.Setup(p => p.GetFilesInFolder("y")).Returns(new TestFileInfo[] { new TestFileInfo("a") });
            _DirectoryCache.Folder = "x";

            CreateBackgroundWorkerMock();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _DirectoryCache.Folder = "y";

            Assert.AreEqual(1, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Raised_If_Cache_Cleared()
        {
            _Files.Add(new TestFileInfo("a"));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);
            _DirectoryCache.Folder = "x";

            CreateBackgroundWorkerMock();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _DirectoryCache.Folder = null;

            Assert.AreEqual(1, _CacheChangedEvent.CallCount);
        }

        [TestMethod]
        public void DirectoryCache_CacheChanged_Not_Raised_For_Heartbeats_After_Cache_Cleared()
        {
            _Files.Add(new TestFileInfo("a"));
            _Provider.Setup(p => p.GetFilesInFolder("x")).Returns(_Files);
            _DirectoryCache.Folder = "x";
            _DirectoryCache.Folder = null;

            CreateBackgroundWorkerMock();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _Now = _Now.AddSeconds(SecondsBetweenRefreshes);
            _HeartbeatService.Raise(s => s.SlowTick += null, EventArgs.Empty);

            Assert.AreEqual(0, _CacheChangedEvent.CallCount);
        }
        #endregion

        #region Exception Handling
        [TestMethod]
        public void DirectoryCache_Logs_Exceptions_Thrown_By_Provider_FolderExists()
        {
            var exception = new Exception();
            _Provider.Setup(p => p.FolderExists(It.IsAny<string>())).Throws(exception);
            _Log.Setup(g => g.WriteLine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Callback(() => { ; });

            _DirectoryCache.Folder = "x";

            _Log.Verify(g => g.WriteLine(It.IsAny<string>(), "x", exception.ToString()), Times.Once());
        }

        [TestMethod]
        public void DirectoryCache_Logs_Exceptions_Thrown_By_Provider_GetFilesInFolder()
        {
            var exception = new Exception();
            _Provider.Setup(p => p.GetFilesInFolder(It.IsAny<string>())).Throws(exception);
            _Log.Setup(g => g.WriteLine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Callback(() => { ; });

            _DirectoryCache.Folder = "x";

            _Log.Verify(g => g.WriteLine(It.IsAny<string>(), "x", exception.ToString()), Times.Once());
        }

        [TestMethod]
        public void DirectoryCache_Logs_Exceptions_Thrown_By_CacheCleared_Event_Handlers()
        {
            var exception = new Exception();
            _DirectoryCache.CacheChanged += _CacheChangedEvent.Handler;
            _CacheChangedEvent.EventRaised += (s, a) => { throw exception; };
            _Log.Setup(g => g.WriteLine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Callback(() => { ; });

            _DirectoryCache.Folder = "x";

            _Log.Verify(g => g.WriteLine(It.IsAny<string>(), "x", exception.ToString()), Times.Once());
        }
        #endregion
    }
}
