using System.Collections.Generic;
﻿using System.IO;
using System.Linq;
using System.Reflection.Emit;
using ChaosDbg.Decompiler;
using ChaosDbg.IL;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;
using ClrDebug;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class ILTests : BaseTest
    {
        class MatchInfo
        {
            public string FileName { get; }

            public string CSharp { get; }

            public ILInstruction[] Instructions { get; }

            public MatchInfo(string fileName, string csharp, ILInstruction[] instructions)
            {
                FileName = fileName;
                CSharp = csharp;
                Instructions = instructions;
            }
        }

        [TestMethod]
        public void IL_StressTest()
        {
            var disp = new MetaDataDispenserEx();
            var dir = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319";
            var ilProvider = GetService<ILDisassemblerProvider>();

            var dlls = Directory.EnumerateFiles(dir, "System.*.dll");

            foreach (var dll in dlls)
            {
                using (var fs = File.Open(dll, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (disp.TryOpenScope<MetaDataImport>(dll, CorOpenFlags.ofReadOnly, out var mdi) != HRESULT.S_OK)
                        continue;

                    var metadataProvider = new MetaDataProvider(mdi);

                    var pe = new PEFile(fs, false, PEFileDirectoryFlags.All);
                    var peReader = new PEBinaryReader(fs);
                    fs.Seek(0, SeekOrigin.Begin);

                    var typeDefs = mdi.EnumTypeDefs();

                    foreach (var typeDef in typeDefs)
                    {
                        var methodDefs = mdi.EnumMethods(typeDef);

                        foreach (var methodDef in methodDefs)
                        {
                            var props = mdi.GetMethodProps(methodDef);

                            if (props.pulCodeRVA == 0 || props.pdwAttr.HasFlag(CorMethodAttr.mdPinvokeImpl))
                                continue;

                            if (pe.TryReadCorILMethod(props.pulCodeRVA, ref peReader, out var result))
                            {
                                var dis = ilProvider.CreateDisassembler(result.ILBytes, module);

                                var instrs = dis.EnumerateInstructions().ToArray();
                            }
                        }
                    }
                }
            }
        }
    }
}
