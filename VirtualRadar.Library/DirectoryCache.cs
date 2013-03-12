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
using System.IO;
using InterfaceFactory;
using VirtualRadar.Interop;

namespace VirtualRadar.Library
{
    /// <summary>
    /// The default implementation of <see cref="IDirectoryCache"/>.
    /// </summary>
    /// <remarks><para>
    /// This uses a polling technique rather than FileSystemWatcher because FSW is unreliable with
    /// with Linux SMB file shares. The polling technique has some obvious drawbacks but they should
    /// not have much of an impact on the intended users of this class.
    /// </para></remarks>
    class DirectoryCache : IDirectoryCache
    {
        #region Private Class - DefaultProvider, DefaultDirectoryCacheProviderFileInfo
        /// <summary>
        /// The default implementation of <see cref="IDirectoryCacheProvider"/>.
        /// </summary>
        class DefaultProvider : IDirectoryCacheProvider
        {
            public DateTime UtcNow                      { get { return DateTime.UtcNow; } }
            public bool FolderExists(string folder)     { return Directory.Exists(folder); }

            public IDirectoryCacheProviderFileInfo GetFileInfo(string fileName)
            {
                return File.Exists(fileName) ? new DefaultDirectoryCacheProviderFileInfo(new FileInfo(fileName)) : null;
            }

            public IEnumerable<IDirectoryCacheProviderFileInfo>GetFilesInFolder(string folder)
            {
                FindFile findFile = new FindFile();  // <- interop workaround for slow DirectoryInfo.GetFiles(), we can replace this with DirectoryInfo in .NET 4
                return findFile.GetFiles(folder).Select(fi => (IDirectoryCacheProviderFileInfo)(new DefaultDirectoryCacheProviderFileInfo(fi)));
            }
        }

        /// <summary>
        /// The default implementation of <see cref="IDirectoryCacheProviderFileInfo"/>.
        /// </summary>
        class DefaultDirectoryCacheProviderFileInfo : IDirectoryCacheProviderFileInfo
        {
            public string Name                  { get; set; }
            public DateTime LastWriteTimeUtc    { get; set; }

            public DefaultDirectoryCacheProviderFileInfo(FindFileData findFileData)
            {
                Name = findFileData.Name;
                LastWriteTimeUtc = findFileData.LastWriteTimeUtc;
            }

            public DefaultDirectoryCacheProviderFileInfo(FileInfo fileInfo)
            {
                Name = fileInfo.Name;
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
            }
        }
        #endregion

        #region Fields
        /// <summary>
        /// The object that is used to restrict access to every other field.
        /// </summary>
        private object _SyncLock = new object();

        /// <summary>
        /// A collection of every file in the cache.
        /// </summary>
        private Dictionary<string, DateTime> _Files = new Dictionary<string, DateTime>();

        /// <summary>
        /// The folder prefix for the files in <see cref="_Files"/>.
        /// </summary>
        private string _FilesFolder;

        /// <summary>
        /// The date and time of the last fetch as at UTC.
        /// </summary>
        private DateTime _LastFetchTime;
        #endregion

        #region Properties
        /// <summary>
        /// See interface docs.
        /// </summary>
        public IDirectoryCacheProvider Provider { get; set; }

        private volatile string _Folder;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Folder
        {
            get { return _Folder; }

            set
            {
                lock(_SyncLock) {
                    if(_Folder != value) {
                        _Folder = value;
                        BeginRefresh();
                    }
                }
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler CacheChanged;

        /// <summary>
        /// Raises <see cref="CacheChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnCacheChanged(EventArgs args)
        {
            if(CacheChanged != null) CacheChanged(this, args);
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public DirectoryCache()
        {
            Provider = new DefaultProvider();
            Factory.Singleton.Resolve<IHeartbeatService>().Singleton.SlowTick += HeartbeatService_SlowTick;
        }
        #endregion

        #region FileExists, Add, Remove, BeginRefresh
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool FileExists(string fileName)
        {
            bool result = false;

            if(!String.IsNullOrEmpty(fileName)) {
                lock(_SyncLock) {
                    var path = Path.GetDirectoryName(fileName);
                    if(path != null) {
                        if(!path.EndsWith("\\")) path += '\\';
                        if(path.ToUpper() == _FilesFolder) {
                            fileName = Path.GetFileName(fileName).ToUpper();
                            result = _Files.ContainsKey(fileName);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="fileName"></param>
        public void Add(string fileName)
        {
            AddRemoveFile(fileName, true);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="fileName"></param>
        public void Remove(string fileName)
        {
            AddRemoveFile(fileName, false);
        }

        /// <summary>
        /// Does the work of adding or removing a file from the cache.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="adding"></param>
        private void AddRemoveFile(string fileName, bool adding)
        {
            lock(_SyncLock) {
                var folder = String.IsNullOrEmpty(fileName) ? null : Path.GetDirectoryName(fileName);
                if(folder != null) {
                    folder = folder.ToUpper();
                    if(!folder.EndsWith("\\")) folder += '\\';
                    if(_FilesFolder == folder) {
                        var fileInfo = Provider.GetFileInfo(fileName);
                        if(adding ? fileInfo != null : fileInfo == null) {
                            fileName = Path.GetFileName(fileName).ToUpper();

                            int countBefore = _Files.Count;
                            if(adding) {
                                if(!_Files.ContainsKey(fileName))   _Files.Add(fileName, fileInfo.LastWriteTimeUtc);
                            } else {
                                if(_Files.ContainsKey(fileName))    _Files.Remove(fileName);
                            }

                            if(_Files.Count != countBefore) OnCacheChanged(EventArgs.Empty);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void BeginRefresh()
        {
            string folder;
            lock(_SyncLock) folder = _Folder;

            var backgroundWorker = Factory.Singleton.Resolve<IBackgroundWorker>();
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.StartWork(folder);
        }
        #endregion

        #region LoadFiles
        /// <summary>
        /// Reloads the cache. Assumes that the caller has locked <see cref="_SyncLock"/>.
        /// </summary>
        private void LoadFiles(string folder)
        {
            try {
                bool raiseCacheCleared = false;

                if(folder == null || !Provider.FolderExists(folder)) {
                    raiseCacheCleared = _FilesFolder != null;
                    _FilesFolder = null;
                    _Files.Clear();
                } else {
                    var filesFolder = folder.ToUpper();
                    if(!filesFolder.EndsWith("\\")) filesFolder += '\\';

                    raiseCacheCleared = filesFolder != _FilesFolder;
                    _FilesFolder = filesFolder;

                    Dictionary<string, DateTime> oldFiles = null;
                    if(!raiseCacheCleared) {
                        oldFiles = new Dictionary<string, DateTime>();
                        foreach(var oldFile in _Files.OrderBy(kvp => kvp.Key)) {
                            oldFiles.Add(oldFile.Key, oldFile.Value);
                        }
                    }

                    _Files.Clear();
                    var directoryEntries = Provider.GetFilesInFolder(folder).OrderBy(f => f.Name).ToList();
                    foreach(var cachedFile in directoryEntries) {
                        _Files.Add(cachedFile.Name.ToUpper(), cachedFile.LastWriteTimeUtc);
                    }

                    if(!raiseCacheCleared && !oldFiles.SequenceEqual(_Files)) raiseCacheCleared = true;
                }

                if(raiseCacheCleared) OnCacheChanged(EventArgs.Empty);

                _LastFetchTime = Provider.UtcNow;
            } catch(Exception ex) {
                Factory.Singleton.Resolve<ILog>().Singleton.WriteLine("Caught exception while reading filenames from {0}: {1}", folder, ex.ToString());
            }
        }
        #endregion

        #region Events consumed
        /// <summary>
        /// Called on a background thread by the background worker service.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void BackgroundWorker_DoWork(object sender, EventArgs args)
        {
            var folder = (string)((IBackgroundWorker)sender).State;
            lock(_SyncLock) {
                if(_Folder == folder) LoadFiles(folder);
            }
        }

        /// <summary>
        /// Called on a background thread by the heartbeat service.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HeartbeatService_SlowTick(object sender, EventArgs args)
        {
            if(_LastFetchTime.AddSeconds(60) <= Provider.UtcNow) {
                lock(_SyncLock) {
                    LoadFiles(Folder);
                }
            }
        }
        #endregion
    }
}
