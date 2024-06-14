using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosLib.PortableExecutable;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a module in a target process.<para/>
    /// Concrete implementations include <see cref="CordbModule"/> and <see cref="DbgEngModule"/>.
    /// </summary>
    public interface IDbgModule
    {
        long BaseAddress { get; }

        int Size { get; }

        long EndAddress { get; }

        /// <summary>
        /// Gets the virtual PE File of this module.
        /// </summary>
        PEFile PEFile { get; }
    }
}
