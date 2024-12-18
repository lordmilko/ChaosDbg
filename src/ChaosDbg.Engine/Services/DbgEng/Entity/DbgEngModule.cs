﻿using PESpy;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Represents a module that has been loaded into a DbgEng debugger.
    /// </summary>
    public class DbgEngModule : IDbgModule
    {
        public long BaseAddress { get; }

        public string FileName { get; }

        public string ModuleName { get; }

        public int Size { get; }

        public long EndAddress => BaseAddress + Size;

        public DbgEngProcess Process { get; }

        public PEFile PEFile { get; }

        public DbgEngModule(long baseAddress, string fileName, string moduleName, int moduleSize, DbgEngProcess process, PEFile peFile)
        {
            BaseAddress = baseAddress;
            FileName = fileName;
            ModuleName = moduleName;
            Size = moduleSize;
            Process = process;
            PEFile = peFile;
        }

        public override string ToString()
        {
            return ModuleName;
        }
    }
}
