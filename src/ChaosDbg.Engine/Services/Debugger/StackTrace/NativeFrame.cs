using System.Diagnostics;
using ChaosLib;
using ChaosLib.Metadata;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a stack frame that was found by a <see cref="NativeStackWalker"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    class NativeFrame
    {
        private string DebuggerDisplay
        {
            get
            {
                if (FunctionName != null)
                    return FunctionName;

                if (Module != null)
                    return $"{Module}!{IP:X}";

                return $"IP = {IP:X}, SP = {SP:X}, BP = {BP:X}";
            }
        }

        public long IP { get; }
        public long SP { get; }
        public long BP { get; }
        public long Return { get; }

        public string FunctionName => Symbol?.ToString();

        public ISymbolModule Module { get; }

        public IDisplacedSymbol Symbol { get; }

        public CrossPlatformContext Context { get; }

        public bool IsInline { get; }

        public NativeFrame(in STACKFRAME_EX stackFrame, IDisplacedSymbol symbol, ISymbolModule module, CrossPlatformContext context)
        {
            IP = stackFrame.AddrPC.Offset;
            SP = stackFrame.AddrStack.Offset;
            BP = stackFrame.AddrFrame.Offset;
            Return = stackFrame.AddrReturn.Offset;
            IsInline = stackFrame.InlineFrameContext.FrameType.HasFlag(ClrDebug.DbgEng.STACK_FRAME_TYPE.STACK_FRAME_TYPE_INLINE);

            Symbol = symbol;
            Module = module;

            Context = context;

            //Not all parts of the context seem to get updated by StackWalkEx, such as the base pointer. This is a bit of an issue,
            //because we're going to lose our "correct" IP/SP/BP when our NativeFrame gets converted to a CordbFrame. Thus, we'll force
            //overwrite these values so that the CONTEXT we store in the CordbFrame is correct
            Context.IP = IP;
            Context.SP = SP;

            //I don't think we're meant to modify BP. When I do a .frame /r 0 vs .frame /r 1 in WinDbg, rbp is the same between both frames
            //Context.BP = BP;
        }
    }
}
