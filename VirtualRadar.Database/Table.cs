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
using System.Data;
using System.IO;

namespace VirtualRadar.Database
{
    /// <summary>
    /// A base class for full read-write tables in an SQLite database.
    /// </summary>
    abstract class Table : IDisposable
    {
        /// <summary>
        /// A map of command names to prepared commands.
        /// </summary>
        private Dictionary<string, SqlPreparedCommand> _Commands = new Dictionary<string,SqlPreparedCommand>();

        /// <summary>
        /// The name of the table in the database.
        /// </summary>
        protected abstract string TableName { get; }

        /// <summary>
        /// Finalises the object.
        /// </summary>
        ~Table()
        {
            Dispose(false);
        }

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
        protected virtual void Dispose(bool disposing)
        {
            if(disposing) {
                foreach(SqlPreparedCommand command in _Commands.Values) {
                    command.Dispose();
                }
                _Commands.Clear();
            }
        }

        /// <summary>
        /// Creates the table if it's missing.
        /// </summary>
        public virtual void CreateTable(IDbConnection connection)
        {
        }

        /// <summary>
        /// Creates the table if it's missing.
        /// </summary>
        public virtual void CreateTable(IDbConnection connection, TextWriter log)
        {
        }

        /// <summary>
        /// Returns a prepared command by the name given by the derived class.
        /// </summary>
        /// <param name="commandName"></param>
        /// <returns></returns>
        private SqlPreparedCommand FetchExistingPreparedCommand(string commandName)
        {
            SqlPreparedCommand existing = null;
            _Commands.TryGetValue(commandName, out existing);

            return existing;
        }

        /// <summary>
        /// Saves a prepared command against the name given by the derived class.
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="existing"></param>
        /// <param name="result"></param>
        private void RecordPreparedCommand(string commandName, SqlPreparedCommand existing, SqlPreparedCommand result)
        {
            if (existing == null) _Commands.Add(commandName, result);
            else if (!Object.ReferenceEquals(existing, result)) _Commands[commandName] = result;
        }

        /// <summary>
        /// Retrieves or creates a prepared command.
        /// </summary>
        /// <returns></returns>
        protected SqlPreparedCommand PrepareCommand(IDbConnection connection, IDbTransaction transaction, string commandName, string commandText, int paramCount)
        {
            SqlPreparedCommand existing = FetchExistingPreparedCommand(commandName);
            SqlPreparedCommand result = Sql.PrepareCommand(existing, connection, transaction, commandText, paramCount);
            RecordPreparedCommand(commandName, existing, result);

            return result;
        }

        /// <summary>
        /// Prepares an insert command.
        /// </summary>
        protected SqlPreparedCommand PrepareInsert(IDbConnection connection, IDbTransaction transaction, string commandName, string uniqueIdColumnName, params string[] columnNames)
        {
            SqlPreparedCommand existing = FetchExistingPreparedCommand(commandName);
            SqlPreparedCommand result = Sql.PrepareInsert(existing, connection, transaction, TableName, uniqueIdColumnName, columnNames);
            RecordPreparedCommand(commandName, existing, result);

            return result;
        }
    }
}
