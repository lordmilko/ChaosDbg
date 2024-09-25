using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an managed/IL frame in a stack trace.<para/>
    /// Encapsulates the <see cref="CorDebugILFrame"/> type.
    /// </summary>
    public class CordbILFrame : CordbFrame<CorDebugILFrame>
    {
        public new CordbManagedModule Module => (CordbManagedModule) base.Module;

        public CordbILFunction Function { get; }

        internal CordbILFrame(CorDebugILFrame corDebugFrame, CordbThread thread, CordbManagedModule module, CrossPlatformContext context) : base(corDebugFrame, thread, module, context)
        {
            Function = new CordbILFunction(corDebugFrame.Function, module);
        }

        /// <summary>
        /// Gets the arguments and local variables contained in this frame.
        /// </summary>
        public override CordbVariable[] Variables
        {
            get
            {
                //Each CordbVariableHome represents either an argument passed to a function, or a local variable declared within the function. This is determined
                //by the m_isLocal member. When a variable is an argument, you should ask for the ArgumentIndex. Otherwise, you should ask for the SlotIndex.

                var nativeCode = CorDebugFrame.Function.NativeCode;

                var homes = nativeCode.VariableHomes;

                var results = new CordbVariable[homes.Length];

                for (var i = 0; i < homes.Length; i++)
                {
                    var home = homes[i];

                    if (home.TryGetArgumentIndex(out _) == HRESULT.S_OK)
                        results[i] = new CordbManagedParameterVariable(home, this, Module);
                    else
                        results[i] = new CordbManagedLocalVariable(home, this, Module);
                }

                //Each variable knows its relative offset within memory. In order to get its absolute address, we need to take into consideration
                //which code chunk its in

                var chunks = nativeCode.CodeChunks;

                if (chunks.Length > 1)
                    throw new System.NotImplementedException("Calculating the addresses of variables split across multiple code chunks is not implemented.");

                var startAddr = chunks.Single().startAddr;

                foreach (var variable in results.Cast<CordbManagedVariable>())
                {
                    var range = variable.CorDebugVariableHome.LiveRange;

                    variable.StartAddress = range.pStartOffset + startAddr;
                    variable.EndAddress = range.pEndOffset + startAddr;
                }

                return results;
            }
        }
    }
}
