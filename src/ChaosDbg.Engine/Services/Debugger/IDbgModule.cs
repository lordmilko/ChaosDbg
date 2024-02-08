using ChaosLib.PortableExecutable;

namespace ChaosDbg
{
    public interface IDbgModule
    {
        long BaseAddress { get; }

        int Size { get; }

        long EndAddress { get; }

        /// <summary>
        /// Gets the virtual PE File of this module.
        /// </summary>
        IPEFile PEFile { get; }
    }
}
