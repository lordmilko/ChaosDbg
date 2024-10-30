using ClrDebug;
using PESpy;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a managed module that is backed by a <see cref="CorDebugProcess"/>.
    /// </summary>
    public class CordbManagedProcessPseudoModule : CordbModule
    {
        //CorDebugProcess instances don't provide a way to get a CorDebugModule out of them, so we must unify CorDebugProcess
        //and CorDebugModule types together via our common CordbModule base type

        public CorDebugProcess CorDebugProcess { get; }

        /// <summary>
        /// Gets the native counterpart of this managed module. This value is only set when interop debugging.<para/>
        /// There is no reference from the <see cref="CordbNativeModule"/> back to this <see cref="CordbManagedProcessPseudoModule"/>.
        /// </summary>
        public CordbNativeModule? NativeModule { get; set; }

        public CordbManagedProcessPseudoModule(string name, CorDebugProcess corDebugProcess, CordbProcess process, PEFile peFile) :
#pragma warning disable RS0030 //This object is only created in response to a create process event (and a managed one at that), so we know that the PEFile is not a random mapped image
            base(name, peFile.OptionalHeader.ImageBase, peFile.OptionalHeader.SizeOfImage, process, peFile)
#pragma warning restore RS0030
        {
            CorDebugProcess = corDebugProcess;
        }
    }
}
