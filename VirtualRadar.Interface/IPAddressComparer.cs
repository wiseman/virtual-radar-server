﻿// Copyright © 2010 onwards, Andrew Whewell
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
using System.Net;

namespace VirtualRadar.Interface
{
    /// <summary>
    /// A class that can compare two IPAddresses for relative order.
    /// </summary>
    public class IPAddressComparer : IComparer<IPAddress>
    {
        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare(IPAddress x, IPAddress y)
        {
            int result = Object.ReferenceEquals(x, y) ? 0 : -1;
            if(result != 0) {
                if(x == null && y == null) result = 0;
                else if(x == null) result = -1;
                else if(y == null) result = 1;
                else {
                    byte[] lhsBytes = x.GetAddressBytes();
                    byte[] rhsBytes = y.GetAddressBytes();
                    result = lhsBytes.Length - rhsBytes.Length;
                    if(result == 0) {
                        for(int c = 0;c < lhsBytes.Length && result == 0;++c) {
                            result = (int)lhsBytes[c] - (int)rhsBytes[c];
                        }
                    }
                }
            }

            return result;
        }
    }
}
