using ClrDebug;
using SymHelp.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a native frame that exists inside of the runtime (e.g. P/Invoke transition stubs).<para/>
    /// In the V2 stack walking API these frames most likely would have been represented as <see cref="CorDebugInternalFrame"/> types.<para/>
    /// "True" native frames are modelled using the <see cref="CordbNativeFrame"/> type.
    /// </summary>
    public class CordbRuntimeNativeFrame : CordbFrame<CorDebugNativeFrame>
    {
        protected override string DebuggerDisplay => "[Runtime] " + base.DebuggerDisplay;

        /// <summary>
        /// Gets the display name of this frame.
        /// </summary>
        public override string Name { get; }

        /// <summary>
        /// Gets the symbol that is associated with this frame, if one exists.
        /// </summary>
        public IDisplacedSymbol Symbol { get; }

        public override CordbVariable[] Variables => throw new System.NotImplementedException();

        internal CordbRuntimeNativeFrame(CorDebugNativeFrame corDebugFrame, CordbThread thread, CordbModule module, CrossPlatformContext context) : base(corDebugFrame, thread, module, context)
        {
            if (thread.Process.Symbols.TryGetSymbolFromAddress(FrameIP, out var symbol))
            {
                if (symbol.Module?.Name == "System")
                    Name = symbol.Name;
                else
                    Name = symbol.ToString();

                Symbol = symbol;
            }
        }
    }
}
