﻿using System;
using System.Diagnostics;
using System.IO;

namespace ChaosDbg.SymStore
{
    /// <summary>
    /// Symbol store file.
    /// 
    /// Key generation: input file stream and file name/path.
    /// Symbol store: output file stream and the file name/path it came.
    /// </summary>
    public sealed class SymbolStoreFile : IDisposable
    {
        /// <summary>
        /// The input file stream to generate the key or the output file stream
        /// for the symbol stores to write.
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// The name of the input file for key generation or the name of where 
        /// the output file came for symbol stores i.e. cached file name, file.ptr
        /// UNC path or http request URL.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Create a symbol file instance
        /// </summary>
        /// <param name="stream">stream of the file contents</param>
        /// <param name="fileName">name of the file</param>
        public SymbolStoreFile(Stream stream, string fileName)
        {
            Debug.Assert(stream != null);
            Debug.Assert(stream.CanSeek);
            Debug.Assert(fileName != null);

            Stream = stream;
            FileName = fileName;
        }

        public void Dispose()
        {
            Stream.Dispose();
        }
    }
}
