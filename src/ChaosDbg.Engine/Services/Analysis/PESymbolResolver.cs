using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;

namespace ChaosDbg.Analysis
{
    class PESymbolResolver : IPESymbolResolver
    {
        private static ByteSequence gsHandlerCheck = new ByteSequence("0x4883EC284D8B4138488BCA498BD1E8........B8010000004883C428C3......");

        private static ByteSequenceTreeNode tree;

        private static Dictionary<ByteSequence, PEExceptionHandlerKind> sequenceToKindMap;

        static PESymbolResolver()
        {
            sequenceToKindMap = new Dictionary<ByteSequence, PEExceptionHandlerKind>
            {
                {gsHandlerCheck, PEExceptionHandlerKind.GSHandlerCheck},
            };

            tree = ByteSequenceTreeNode.BuildTree(sequenceToKindMap.Keys.ToArray());
        }

        private Dictionary<int, PEExceptionHandlerKind> cache = new Dictionary<int, PEExceptionHandlerKind>();
        private ISymbolModule module;

        public PESymbolResolver(ISymbolModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            this.module = module;
        }

        public bool TryResolve(Stream stream, int exceptionHandler, out PEExceptionHandlerKind exceptionHandlerKind)
        {
            if (cache.TryGetValue(exceptionHandler, out var knownKind))
            {
                exceptionHandlerKind = knownKind;
                return true;
            }

            //No need to seek back to the start. After this is read we'll always seek to something else
            var absoluteAddress = module.Address + exceptionHandler;

            var symbol = module.GetSymbolFromAddress(absoluteAddress);

            if (symbol != null)
            {
                //The symbol name can have one or two leading underscores
                switch (symbol.Name.TrimStart('_'))
                {
                    case "GSHandlerCheck":
                        exceptionHandlerKind = PEExceptionHandlerKind.GSHandlerCheck;
                        break;

                    case "GSHandlerCheck_SEH":
                        exceptionHandlerKind = PEExceptionHandlerKind.GSHandlerCheckSEH;
                        break;

                    case "GSHandlerCheck_SEH_noexcept":
                        exceptionHandlerKind = PEExceptionHandlerKind.GSHandlerCheckSEHNoExcept;
                        break;

                    case "GSHandlerCheck_EH":
                        exceptionHandlerKind = PEExceptionHandlerKind.GSHandlerCheckEH;
                        break;

                    case "GSHandlerCheck_EH4":
                        exceptionHandlerKind = PEExceptionHandlerKind.GSHandlerCheckEH4;
                        break;

                    case "C_specific_handler":
                        exceptionHandlerKind = PEExceptionHandlerKind.CSpecificHandler;
                        break;

                    case "C_specific_handler_noexcept":
                        exceptionHandlerKind = PEExceptionHandlerKind.CSpecificHandlerNoExcept;
                        break;

                    case "CxxFrameHandler3":
                        exceptionHandlerKind = PEExceptionHandlerKind.CxxFrameHandler3;
                        break;

                    case "CxxFrameHandler4":
                        exceptionHandlerKind = PEExceptionHandlerKind.CxxFrameHandler4;
                        break;

                    default:
                        if (symbol.Name.StartsWith("_"))
                            throw new NotImplementedException($"Don't know how to handle symbol '{symbol.Name}'");

                        exceptionHandlerKind = PEExceptionHandlerKind.Custom;
                        break;
                }

                cache[exceptionHandler] = exceptionHandlerKind;

                return true;
            }

            var position = stream.Position;

            stream.Seek(exceptionHandler, SeekOrigin.Begin);

            var match = tree.GetMatches(stream).FirstOrDefault();

            if (match == null)
            {
                exceptionHandlerKind = default;
                return false;
            }

            exceptionHandlerKind = sequenceToKindMap[match.Sequence];
            return true;
        }
    }
}
