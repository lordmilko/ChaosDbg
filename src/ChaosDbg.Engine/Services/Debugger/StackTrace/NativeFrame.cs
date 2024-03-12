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
                    return $"{Module}!{FrameIP:X}";

                return $"IP = {FrameIP:X}, SP = {FrameSP:X}, BP = {FrameBP:X}";
            }
        }

        public long FrameIP { get; }
        public long FrameSP { get; }
        public long FrameBP { get; }

        public long Return { get; }

        public string FunctionName => Symbol?.ToString();

        public ISymbolModule Module { get; }

        public IDisplacedSymbol Symbol { get; }

        public CrossPlatformContext Context { get; }

        public bool IsInline { get; }

        public NativeFrame(in STACKFRAME_EX stackFrame, IDisplacedSymbol symbol, ISymbolModule module, CrossPlatformContext context)
        {
            /* It is _not_ necessarily the case that the BP of the frame is the same as the BP of the context. This is particularly true on x64.
             * When this frame is at the top of the stack everything is all good, however if there's another frame above us, the BP of the second
             * frame on the stack will be different from the BP of that same frame's context. Locals are defined as being relative to the frame BP;
             * attempting to resolve locals based on the value of the context BP won't work */

            FrameIP = stackFrame.AddrPC.Offset;
            FrameSP = stackFrame.AddrStack.Offset;
            FrameBP = stackFrame.AddrFrame.Offset;

            //We currently make an assumption that the IP and SP of the frame's context will be the same as the IP and SP of the frame itself.
            //If this turns out to not be true, we'll need to revisit how we utilize IP/SP values everywhere
            Debug.Assert(context.IP == FrameIP);
            Debug.Assert(context.SP == FrameSP);

            Return = stackFrame.AddrReturn.Offset;
            IsInline = stackFrame.InlineFrameContext.FrameType.HasFlag(ClrDebug.DbgEng.STACK_FRAME_TYPE.STACK_FRAME_TYPE_INLINE);

            Symbol = symbol;
            Module = module;

            Context = context;
        }
    }
}
