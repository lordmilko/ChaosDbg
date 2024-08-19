﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ChaosDbg.SymStore
{
    /// <summary>
    /// Symbol store key information
    /// </summary>
    public sealed class SymbolStoreKey
    {
        /// <summary>
        /// Symbol server index
        /// </summary>
        public string Index { get; }

        /// <summary>
        /// Full path name
        /// </summary>
        public string FullPathName { get; }

        /// <summary>
        /// If true, this file is one of the clr special files like the DAC or SOS, but
        /// the key is the normal identity key for this file.
        /// </summary>
        public bool IsClrSpecialFile { get; }

        /// <summary>
        /// Create key instance.
        /// </summary>
        /// <param name="index">index to lookup on symbol server</param>
        /// <param name="fullPathName">the full path name of the file</param>
        /// <param name="clrSpecialFile">if true, the file is one the clr special files</param>
        public SymbolStoreKey(string index, string fullPathName, bool clrSpecialFile = false)
        {
            Debug.Assert(index != null && fullPathName != null);
            Index = index;
            FullPathName = fullPathName;
            IsClrSpecialFile = clrSpecialFile;
        }

        /// <summary>
        /// Returns the hash of the index.
        /// </summary>
        public override int GetHashCode() => Index.GetHashCode();

        /// <summary>
        /// Only the index is compared or hashed. The FileName is already
        /// part of the index.
        /// </summary>
        public override bool Equals(object obj)
        {
            SymbolStoreKey right = (SymbolStoreKey)obj;
            return string.Equals(Index, right.Index);
        }

        private static HashSet<char> s_invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());

        /// <summary>
        /// Validates a symbol index.
        ///
        /// SSQP theoretically supports a broader set of keys, but in order to ensure that all the keys
        /// play well with the caching scheme we enforce additional requirements (that all current key
        /// conventions also meet).
        /// </summary>
        /// <param name="index">symbol key index</param>
        /// <returns>true if valid</returns>
        public static bool IsKeyValid(string index)
        {
            string[] parts = index.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return false;
            }
            for (int i = 0; i < 3; i++)
            {
                foreach (char c in parts[i])
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        continue;
                    }
                    if (!s_invalidChars.Contains(c))
                    {
                        continue;
                    }
                    return false;
                }
                // We need to support files with . in the name, but we don't want identifiers that 
                // are meaningful to the filesystem
                if (parts[i] == "." || parts[i] == "..")
                {
                    return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            return Index;
        }
    }
}
