using System;
using System.IO;
using ChaosDbg.Disasm;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Analysis
{
    public abstract class PEMetadataModule : IDisposable
    {
        public string FileName { get; }

        public abstract CORDB_ADDRESS Address { get; }

        /// <summary>
        /// Provides access to any symbol information that is available for this module.
        /// </summary>
        public ISymbolModule SymbolModule { get; }

        /// <summary>
        /// Gets a disassembler that reads instructions directly from the memory of a remote process (when
        /// the module type is <see cref="PEMetadataVirtualModule"/> or the physical file (when the module
        /// type is <see cref="PEMetadataPhysicalModule"/>).
        /// </summary>
        internal INativeDisassembler RemoteDisassembler { get; }

        protected PEMetadataModule(
            string fileName,
            ISymbolModule symbolModule,
            Func<Stream, INativeDisassembler> createDisassembler)
        {
            FileName = fileName;
            SymbolModule = symbolModule;
            RemoteDisassembler = createDisassembler(null);
        }

        ~PEMetadataModule()
        {
            RemoteDisassembler?.Dispose();
        }

        public IMetadataRange[] Ranges { get; private set; }

        public abstract bool ContainsAddress(long address);

#if DEBUG
        public virtual long GetPhysicalAddress(long address) => address;
#endif

        protected IMetadataRange FindByAddress(long address)
        {
            //If we're a PEMetadataVirtualModule, these addresses will be virtual. Otherwise, they'll be physical

            foreach (var item in Ranges)
            {
                if (address >= item.StartAddress)
                {
                    if (address <= item.EndAddress)
                        return item;
                }
                else
                    return null; //We've gone too far
            }

            return null;
        }

        internal void SetMetadata(IMetadataRange[] ranges)
        {
            Ranges = ranges;
        }

        public void Dispose()
        {
            RemoteDisassembler?.Dispose();
        }
    }
}
