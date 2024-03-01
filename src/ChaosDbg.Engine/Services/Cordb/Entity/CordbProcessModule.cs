using ChaosLib.PortableExecutable;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a managed module that is backed by a <see cref="CorDebugProcess"/>.
    /// </summary>
    public class CordbProcessModule : CordbModule
    {
        //CorDebugProcess instances don't provide a way to get a CorDebugModule out of them, so we must unify CorDebugProcess
        //and CorDebugModule types together via our common CordbModule base type

        public CorDebugProcess CorDebugProcess { get; }

        /// <summary>
        /// Gets the native counterpart of this managed module. This value is only set when interop debugging.<para/>
        /// There is no reference from the <see cref="CordbNativeModule"/> back to this <see cref="CordbProcessModule"/>.
        /// </summary>
        public CordbNativeModule? NativeModule { get; set; }

        public CordbProcessModule(string name, CorDebugProcess corDebugProcess, CordbProcess process, IPEFile peFile) :
            base(name, peFile.OptionalHeader.ImageBase, peFile.OptionalHeader.SizeOfImage, process, peFile)
        {
            CorDebugProcess = corDebugProcess;
        }
    }
}
