using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a collection of <see cref="NativeCodeRegion"/> items that ostensibly comprise a function. Further processing against
    /// the instructions discovered as part of these regions may be required in order to actually allocate instructions to the actual function
    /// that should own them.
    /// </summary>
    public class NativeCodeRegionCollection
    {
        /// <summary>
        /// Gets the address of the initial instruction that ostensibly represented the start of a function.
        /// </summary>
        public long Address { get; }

        /// <summary>
        /// Gets the instruction regions that were discovered.
        /// </summary>
        public IList<NativeCodeRegion> Regions { get; }

        public INativeInstruction[] Instructions => Regions.SelectMany(v => v.Instructions).OrderBy(v => v.Address).ToArray();

        /// <summary>
        /// Gets the reason that the discovery failed, or <see cref="NativeCodeDiscoveryError.None"/> if disassembly completed successfully.
        /// </summary>
        public NativeCodeDiscoveryError Error { get; }

        public bool IsSuccess => Error == NativeCodeDiscoveryError.None;

        internal NativeCodeRegionCollection(long address, IList<NativeCodeRegion> regions, NativeCodeDiscoveryError error)
        {
            Address = address;
            Regions = regions;
            Error = error;
        }

        public bool Contains(long address) => Regions.Any(r => r.Contains(address));
    }
}
