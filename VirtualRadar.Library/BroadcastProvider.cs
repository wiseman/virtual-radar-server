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
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.IO;
using System.Threading;
using VirtualRadar.Interface.Settings;

namespace VirtualRadar.Library
{
    /// <summary>
    /// The default implementation of <see cref="IBroadcastProvider"/>.
    /// </summary>
    sealed class BroadcastProvider : IBroadcastProvider
    {
        #region Private class - Client
        /// <summary>
        /// A private class holding information about a connected client.
        /// </summary>
        class Client
        {
            public TcpClient TcpClient;
            public IPEndPoint IPEndPoint;
        }
        #endregion

        #region Fields
        /// <summary>
        /// The listener we're using.
        /// </summary>
        private TcpListener _Listener;

        /// <summary>
        /// A list of clients connected to us.
        /// </summary>
        private List<Client> _ConnectedClients = new List<Client>();

        /// <summary>
        /// The object that locks fields across different threads.
        /// </summary>
        private object _SyncLock = new object();

        /// <summary>
        /// The object that will send the bytes to all connected clients on a background thread.
        /// </summary>
        private BackgroundThreadQueue<byte[]> _SendQueue;

        /// <summary>
        /// An incrementing counter that records how many instances of the provider have been started.
        /// </summary>
        /// <remarks>
        /// Used to give unique names to each <see cref="_SendQueue"/>.
        /// </remarks>
        private static int _InstanceCounter;

        /// <summary>
        /// True if the object has been, or is in the process of being, disposed.
        /// </summary>
        private bool _Disposed;
        #endregion

        #region Properties
        /// <summary>
        /// See interface docs.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public RebroadcastFormat EventFormat { get; set; }
        #endregion

        #region Events exposed
        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler<EventArgs<Exception>> ExceptionCaught;

        /// <summary>
        /// Raises <see cref="ExceptionCaught"/>.
        /// </summary>
        /// <param name="args"></param>
        private void OnExceptionCaught(EventArgs<Exception> args)
        {
            if(ExceptionCaught != null) ExceptionCaught(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler<BroadcastEventArgs> ClientConnected;

        /// <summary>
        /// Raises <see cref="ClientConnected"/>.
        /// </summary>
        /// <param name="args"></param>
        private void OnClientConnected(BroadcastEventArgs args)
        {
            if(ClientConnected != null) ClientConnected(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler<BroadcastEventArgs> ClientDisconnected;

        /// <summary>
        /// Raises <see cref="ClientDisconnected"/>.
        /// </summary>
        /// <param name="args"></param>
        private void OnClientDisconnected(BroadcastEventArgs args)
        {
            if(ClientDisconnected != null) ClientDisconnected(this, args);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler<BroadcastEventArgs> BroadcastSent;

        /// <summary>
        /// Raises <see cref="BroadcastSent"/>.
        /// </summary>
        /// <param name="args"></param>
        private void OnBroadcastSent(BroadcastEventArgs args)
        {
            if(BroadcastSent != null) BroadcastSent(this, args);
        }
        #endregion

        #region Constructor and finaliser
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public BroadcastProvider()
        {
            _SendQueue = new BackgroundThreadQueue<byte[]>(String.Format("BroadcastProvider{0}", ++_InstanceCounter));
            _SendQueue.StartBackgroundThread(SendQueueHandler, SendQueueExceptionHandler);
        }

        /// <summary>
        /// Finalises the object.
        /// </summary>
        ~BroadcastProvider()
        {
            Dispose(false);
        }
        #endregion

        #region Dispose
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of or finalises the object.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if(disposing) {
                lock(_SyncLock) _Disposed = true;

                foreach(var client in _ConnectedClients.ToArray()) {
                    try {
                        DisconnectClient(client);
                    } catch {
                    }
                }
                _ConnectedClients.Clear();

                if(_SendQueue != null) {
                    _SendQueue.Dispose();
                    _SendQueue = null;
                }

                if(_Listener != null) {
                    try {
                        _Listener.Stop();
                        _Listener = null;
                    } catch {
                    }
                }
            }
        }
        #endregion

        #region BeginListening, IncomingTcpClient
        /// <summary>
        /// See interface docs.
        /// </summary>
        public void BeginListening()
        {
            if(Port < 0 || Port > 65535) throw new InvalidOperationException("The port is out of range");

            if(_Listener == null) {
                _Listener = new TcpListener(IPAddress.Any, Port);
                _Listener.Start();
                _Listener.BeginAcceptTcpClient(IncomingTcpClient, null);
            }
        }

        /// <summary>
        /// Called on a background thread when a client connects.
        /// </summary>
        /// <param name="result"></param>
        private void IncomingTcpClient(IAsyncResult result)
        {
            TcpClient tcpClient = null;

            try {
                lock(_SyncLock) {
                    tcpClient = _Listener == null || _Disposed ? null : _Listener.EndAcceptTcpClient(result);
                }
            } catch(SocketException) {
                // Exception spam
            } catch(ObjectDisposedException) {
                // Exception spam
            } catch(InvalidOperationException) {
                // Exception spam
            } catch(ThreadAbortException) {
                // We can ignore this, it'll get re-thrown automatically on the end of the try. We don't want to report it.
            } catch(Exception ex) {
                OnExceptionCaught(new EventArgs<Exception>(ex));
            }

            if(tcpClient != null) {
                try {
                    Client client = null;
                    lock(_SyncLock) {
                        if(_Disposed) client = null;
                        else {
                            client = new Client() {
                                TcpClient = tcpClient,
                                IPEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint,
                            };
                            if(client.IPEndPoint == null) {
                                ((IDisposable)tcpClient).Dispose();
                                client = null;
                            } else {
                                tcpClient.NoDelay = IPEndPointHelper.IsLocalOrLanAddress(client.IPEndPoint);
                                _ConnectedClients.Add(client);
                            }
                        }
                    }

                    if(client != null) {
                        OnClientConnected(new BroadcastEventArgs(client.IPEndPoint, 0, Port, EventFormat));
                    }
                } catch(ThreadAbortException) {
                    // We can ignore this, it'll get re-thrown automatically on the end of the try. We don't want to report it.
                } catch(Exception ex) {
                    OnExceptionCaught(new EventArgs<Exception>(ex));
                }
            }

            try {
                lock(_SyncLock) {
                    if(_Listener != null && !_Disposed) _Listener.BeginAcceptTcpClient(IncomingTcpClient, null);
                }
            } catch(ThreadAbortException) {
                // We can ignore this, it'll get re-thrown automatically on the end of the try. We don't want to report it.
            } catch(Exception ex) {
                OnExceptionCaught(new EventArgs<Exception>(ex));
            }
        }
        #endregion

        #region DisconnectClient
        /// <summary>
        /// Disconnects the client, preventing any further transmissions to it.
        /// </summary>
        /// <param name="client"></param>
        /// <remarks>
        /// In principle this could be called more than once on a client so exceptions about disposed objects etc.
        /// are just discarded.
        /// </remarks>
        private void DisconnectClient(Client client)
        {
            if(client != null) {
                int index = -1;
                lock(_SyncLock) {
                    index = _ConnectedClients.IndexOf(client);
                    if(index != -1) _ConnectedClients.RemoveAt(index);
                }

                if(index != -1) {
                    try {
                        ((IDisposable)client.TcpClient).Dispose();
                    } catch {
                    }

                    try {
                        OnClientDisconnected(new BroadcastEventArgs(client.IPEndPoint, 0, Port, EventFormat));
                    } catch(ThreadAbortException) {
                        // This will be rethrown - we just want to avoid reporting it
                    } catch(Exception ex) {
                        OnExceptionCaught(new EventArgs<Exception>(ex));
                    }
                }
            }
        }
        #endregion

        #region Send, SendQueueHandler, SendQueueExceptionHandler
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="bytes"></param>
        public void Send(byte[] bytes)
        {
            if(_SendQueue != null) _SendQueue.Enqueue(bytes);
        }

        /// <summary>
        /// Sends the bytes to each client on a background thread.
        /// </summary>
        /// <param name="bytes"></param>
        private void SendQueueHandler(byte[] bytes)
        {
            List<Client> clients = new List<Client>();
            lock(_SyncLock) {
                if(!_Disposed) clients.AddRange(_ConnectedClients);
            }

            foreach(var client in clients) {
                try {
                    client.TcpClient.GetStream().BeginWrite(bytes, 0, bytes.Length, SendQueueEndWrite, client);
                    OnBroadcastSent(new BroadcastEventArgs(client.IPEndPoint, bytes.Length, Port, EventFormat));
                // Not sure if the exception catches here are necessary as in principle BeginWrite doesn't do anything but invoke a method,
                // but better safe than sorry. The important catches are around the EndInvoke in SendQueueEndWrite.
                } catch(IOException) {
                    DisconnectClient(client);
                } catch(SocketException) {
                    DisconnectClient(client);
                } catch(ObjectDisposedException) {
                    DisconnectClient(client);
                } catch(InvalidOperationException) {
                    DisconnectClient(client);
                } catch(ThreadAbortException) {
                    // We can ignore this, it'll get re-thrown automatically on the end of the try. We don't want to report it.
                } catch(Exception ex) {
                    OnExceptionCaught(new EventArgs<Exception>(ex));
                }
            }
        }

        /// <summary>
        /// Called when a write on a stream has completed.
        /// </summary>
        /// <param name="ar"></param>
        private void SendQueueEndWrite(IAsyncResult ar)
        {
            var client = (Client)ar.AsyncState;
            try {
                client.TcpClient.GetStream().EndWrite(ar);
                if(!client.TcpClient.Connected) DisconnectClient(client);
            } catch(IOException) {
                DisconnectClient(client);
            } catch(SocketException) {
                DisconnectClient(client);
            } catch(ObjectDisposedException) {
                DisconnectClient(client);
            } catch(InvalidOperationException) {
                DisconnectClient(client);
            } catch(ThreadAbortException) {
                // We can ignore this, it'll get re-thrown automatically on the end of the try. We don't want to report it.
            } catch(Exception ex) {
                OnExceptionCaught(new EventArgs<Exception>(ex));
            }
        }

        /// <summary>
        /// Handles exceptions raised during the processing of <see cref="SendQueueHandler"/>.
        /// </summary>
        /// <param name="ex"></param>
        private void SendQueueExceptionHandler(Exception ex)
        {
            OnExceptionCaught(new EventArgs<Exception>(ex));
        }
        #endregion
    }
}
